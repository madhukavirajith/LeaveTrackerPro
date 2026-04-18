// ============================================================
// FILE: Services/LeaveService.cs
// All business logic for leave requests:
//   - Submit with validation (overlap, balance, dates)
//   - Approve / Reject / Cancel
//   - Balance tracking per employee per year
//   - Queries with filtering/paging
// ============================================================

using LeaveTrackerPro.Data;
using LeaveTrackerPro.Models;
using LeaveTrackerPro.Helpers;
using Microsoft.EntityFrameworkCore;

namespace LeaveTrackerPro.Services
{
    public class LeaveService
    {
        private readonly AuditService _audit;
        private readonly EmailService _email;

        // Leave types and their annual entitlements (days)
        public static readonly Dictionary<string, int> LeaveEntitlements = new()
        {
            { "Annual Leave",             14 },
            { "Sick Leave",               10 },
            { "Maternity / Paternity",    84 },
            { "Unpaid Leave",             30 },
            { "Emergency Leave",           3 },
            { "Study Leave",               5 }
        };

        public LeaveService(AuditService audit, EmailService email)
        {
            _audit = audit;
            _email = email;
        }

        // -------------------------------------------------------
        // SUBMIT a new leave request
        // Returns (success, error message, created request)
        // -------------------------------------------------------
        public (bool Success, string Error, LeaveRequest? Request) Submit(
            int userId, string leaveType, DateTime startDate, DateTime endDate, string reason)
        {
            // --- Business rule validation ---
            if (endDate < startDate)
                return (false, "End date cannot be before start date.", null);

            if (startDate.Date < DateTime.Today)
                return (false, "Cannot submit a leave request for a past date.", null);

            int days = CalculateWorkingDays(startDate, endDate);
            if (days <= 0)
                return (false, "The selected dates contain no working days.", null);

            using var db = new AppDbContext();

            // Check for overlapping approved/pending requests
            bool overlap = db.LeaveRequests.Any(r =>
                r.UserId == userId &&
                r.Status != LeaveStatus.Rejected &&
                r.Status != LeaveStatus.Cancelled &&
                r.StartDate <= endDate &&
                r.EndDate >= startDate);

            if (overlap)
                return (false, "You already have a leave request that overlaps with these dates.", null);

            // Check leave balance
            var balance = GetOrCreateBalance(db, userId, leaveType, startDate.Year);
            if (balance.DaysRemaining < days)
                return (false, $"Insufficient leave balance. You have {balance.DaysRemaining} day(s) remaining for {leaveType}.", null);

            // --- Create the request ---
            var request = new LeaveRequest
            {
                UserId = userId,
                LeaveType = leaveType,
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                Reason = reason.Trim(),
                Status = LeaveStatus.Pending,
                SubmittedAt = DateTime.UtcNow,
                TotalDays = days
            };

            db.LeaveRequests.Add(request);

            // Deduct from pending balance immediately
            balance.DaysPending += days;
            db.SaveChanges();

            _audit.Log(request.Id, userId, "Submitted",
                $"Submitted {days}-day {leaveType} request ({startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy})");

            // Email manager
            NotifyManagersAsync(request, db).GetAwaiter().GetResult();

            return (true, string.Empty, request);
        }

        // -------------------------------------------------------
        // APPROVE a request
        // -------------------------------------------------------
        public (bool Success, string Error) Approve(int requestId, string managerNote)
        {
            if (!Session.IsManager)
                return (false, "Only managers can approve requests.");

            using var db = new AppDbContext();
            var request = db.LeaveRequests
                .Include(r => r.User)
                .FirstOrDefault(r => r.Id == requestId);

            if (request == null) return (false, "Request not found.");
            if (request.Status != LeaveStatus.Pending)
                return (false, $"This request is already {request.Status}.");

            request.Status = LeaveStatus.Approved;
            request.ManagerNote = managerNote.Trim();
            request.ReviewedByUserId = Session.CurrentUser!.Id;
            request.ReviewedAt = DateTime.UtcNow;

            // Move days from Pending → Used in the balance
            var balance = GetOrCreateBalance(db, request.UserId, request.LeaveType, request.StartDate.Year);
            balance.DaysPending = Math.Max(0, balance.DaysPending - request.TotalDays);
            balance.DaysUsed += request.TotalDays;

            db.SaveChanges();

            _audit.Log(requestId, Session.CurrentUser.Id, "Approved",
                $"Request #{requestId} approved by {Session.CurrentUser.FullName}. Note: {managerNote}");

            // Email employee
            _email.SendApprovalNotificationAsync(request).GetAwaiter().GetResult();

            return (true, string.Empty);
        }

        // -------------------------------------------------------
        // REJECT a request
        // -------------------------------------------------------
        public (bool Success, string Error) Reject(int requestId, string managerNote)
        {
            if (!Session.IsManager)
                return (false, "Only managers can reject requests.");

            if (string.IsNullOrWhiteSpace(managerNote))
                return (false, "A reason is required when rejecting a request.");

            using var db = new AppDbContext();
            var request = db.LeaveRequests
                .Include(r => r.User)
                .FirstOrDefault(r => r.Id == requestId);

            if (request == null) return (false, "Request not found.");
            if (request.Status != LeaveStatus.Pending)
                return (false, $"This request is already {request.Status}.");

            request.Status = LeaveStatus.Rejected;
            request.ManagerNote = managerNote.Trim();
            request.ReviewedByUserId = Session.CurrentUser!.Id;
            request.ReviewedAt = DateTime.UtcNow;

            // Restore the pending days back to available
            var balance = GetOrCreateBalance(db, request.UserId, request.LeaveType, request.StartDate.Year);
            balance.DaysPending = Math.Max(0, balance.DaysPending - request.TotalDays);

            db.SaveChanges();

            _audit.Log(requestId, Session.CurrentUser.Id, "Rejected",
                $"Request #{requestId} rejected. Reason: {managerNote}");

            _email.SendRejectionNotificationAsync(request).GetAwaiter().GetResult();

            return (true, string.Empty);
        }

        // -------------------------------------------------------
        // CANCEL own request (employee can cancel if still Pending)
        // -------------------------------------------------------
        public (bool Success, string Error) Cancel(int requestId)
        {
            using var db = new AppDbContext();
            var request = db.LeaveRequests.Find(requestId);
            if (request == null) return (false, "Request not found.");

            // Employees can only cancel their own; managers can cancel any
            if (!Session.IsManager && request.UserId != Session.CurrentUser!.Id)
                return (false, "You can only cancel your own requests.");

            if (request.Status != LeaveStatus.Pending)
                return (false, "Only pending requests can be cancelled.");

            request.Status = LeaveStatus.Cancelled;

            var balance = GetOrCreateBalance(db, request.UserId, request.LeaveType, request.StartDate.Year);
            balance.DaysPending = Math.Max(0, balance.DaysPending - request.TotalDays);

            db.SaveChanges();

            _audit.Log(requestId, Session.CurrentUser!.Id, "Cancelled", $"Request #{requestId} cancelled");
            return (true, string.Empty);
        }

        // -------------------------------------------------------
        // QUERY: get requests visible to the current user
        // Employees see only their own; managers see their dept;
        // admins see everything
        // -------------------------------------------------------
        public List<LeaveRequest> GetRequests(
            string? statusFilter = null,
            string? departmentFilter = null,
            string? searchName = null,
            int year = 0)
        {
            using var db = new AppDbContext();

            var query = db.LeaveRequests
                .Include(r => r.User)
                .Include(r => r.ReviewedByUser)
                .AsQueryable();

            // Role-based data scoping
            if (Session.IsEmployee)
                query = query.Where(r => r.UserId == Session.CurrentUser!.Id);
            else if (Session.IsManager && !Session.IsAdmin)
                query = query.Where(r => r.User!.Department == Session.CurrentUser!.Department);

            // Optional filters
            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<LeaveStatus>(statusFilter, out var s))
                query = query.Where(r => r.Status == s);

            if (!string.IsNullOrEmpty(departmentFilter))
                query = query.Where(r => r.User!.Department == departmentFilter);

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(r => r.User!.FullName.Contains(searchName));

            if (year > 0)
                query = query.Where(r => r.StartDate.Year == year);

            return query.OrderByDescending(r => r.SubmittedAt).ToList();
        }

        // -------------------------------------------------------
        // GET leave balances for a user
        // -------------------------------------------------------
        public List<LeaveBalance> GetBalances(int userId, int year)
        {
            using var db = new AppDbContext();

            // Ensure all leave types have a balance row
            foreach (var lt in LeaveEntitlements.Keys)
                GetOrCreateBalance(db, userId, lt, year);

            return db.LeaveBalances
                .Where(b => b.UserId == userId && b.Year == year)
                .ToList();
        }

        // -------------------------------------------------------
        // HELPER: Get or create a balance row
        // -------------------------------------------------------
        private static LeaveBalance GetOrCreateBalance(
            AppDbContext db, int userId, string leaveType, int year)
        {
            var balance = db.LeaveBalances.FirstOrDefault(b =>
                b.UserId == userId && b.Year == year && b.LeaveType == leaveType);

            if (balance == null)
            {
                balance = new LeaveBalance
                {
                    UserId = userId,
                    Year = year,
                    LeaveType = leaveType,
                    TotalEntitlement = LeaveEntitlements.GetValueOrDefault(leaveType, 14)
                };
                db.LeaveBalances.Add(balance);
                db.SaveChanges();
            }

            return balance;
        }

        // -------------------------------------------------------
        // HELPER: Count working days (Mon-Fri only)
        // -------------------------------------------------------
        public static int CalculateWorkingDays(DateTime start, DateTime end)
        {
            int count = 0;
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                    count++;
            return count;
        }

        // -------------------------------------------------------
        // NOTIFY managers by email when a request is submitted
        // -------------------------------------------------------
        private async Task NotifyManagersAsync(LeaveRequest request, AppDbContext db)
        {
            // Reload the request with the User navigation property loaded
            var fullRequest = db.LeaveRequests
                .Include(r => r.User)
                .FirstOrDefault(r => r.Id == request.Id);

            if (fullRequest?.User == null)
                return; // No user found, skip email

            var managers = db.Users
                .Where(u => (u.Role == UserRole.Manager || u.Role == UserRole.Admin)
                         && u.Department == fullRequest.User.Department
                         && u.IsActive)
                .ToList();

            foreach (var mgr in managers)
                await _email.SendNewRequestNotificationAsync(fullRequest, mgr);
        }

        // -------------------------------------------------------
        // STATISTICS for dashboard
        // -------------------------------------------------------
        public (int Total, int Pending, int Approved, int Rejected) GetStats()
        {
            using var db = new AppDbContext();
            var q = db.LeaveRequests.AsQueryable();

            if (Session.IsEmployee)
                q = q.Where(r => r.UserId == Session.CurrentUser!.Id);
            else if (Session.IsManager && !Session.IsAdmin)
                q = q.Where(r => r.User!.Department == Session.CurrentUser!.Department);

            return (
                q.Count(),
                q.Count(r => r.Status == LeaveStatus.Pending),
                q.Count(r => r.Status == LeaveStatus.Approved),
                q.Count(r => r.Status == LeaveStatus.Rejected)
            );
        }
    }
}