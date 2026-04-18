// ============================================================
// FILE: Services/UserService.cs
// Manages user accounts: create, update, deactivate.
// Only admins can create/deactivate users.
// ============================================================

using ClosedXML.Excel;
using LeaveTrackerPro.Data;
using LeaveTrackerPro.Helpers;
using LeaveTrackerPro.Models;

namespace LeaveTrackerPro.Services
{
    public class UserService
    {
        private readonly AuditService _audit;

        public UserService(AuditService audit)
        {
            _audit = audit;
        }

        public (bool Success, string Error) CreateUser(
            string fullName, string email, string username,
            string password, string department, UserRole role)
        {
            if (!Session.IsAdmin)
                return (false, "Only administrators can create users.");

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username))
                return (false, "Full name and username are required.");

            var passCheck = AuthService.ValidatePasswordStrength(password);
            if (!passCheck.Valid) return (false, passCheck.Message);

            using var db = new AppDbContext();

            if (db.Users.Any(u => u.Username.ToLower() == username.ToLower()))
                return (false, "Username already exists.");
            if (db.Users.Any(u => u.Email.ToLower() == email.ToLower()))
                return (false, "Email address already exists.");

            var user = new User
            {
                FullName = fullName.Trim(),
                Email = email.Trim().ToLower(),
                Username = username.Trim().ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Department = department,
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            db.SaveChanges();

            _audit.Log(null, Session.CurrentUser!.Id, "UserCreated",
                $"Created user '{username}' with role {role}");

            return (true, string.Empty);
        }

        public List<User> GetAll(bool includeInactive = false)
        {
            using var db = new AppDbContext();
            var q = db.Users.AsQueryable();
            if (!includeInactive) q = q.Where(u => u.IsActive);
            return q.OrderBy(u => u.FullName).ToList();
        }

        public (bool Success, string Error) SetActiveStatus(int userId, bool active)
        {
            if (!Session.IsAdmin)
                return (false, "Only administrators can activate/deactivate users.");

            if (userId == Session.CurrentUser!.Id)
                return (false, "You cannot deactivate your own account.");

            using var db = new AppDbContext();
            var user = db.Users.Find(userId);
            if (user == null) return (false, "User not found.");

            user.IsActive = active;
            db.SaveChanges();

            _audit.Log(null, Session.CurrentUser.Id, active ? "UserActivated" : "UserDeactivated",
                $"User '{user.Username}' {(active ? "activated" : "deactivated")}");

            return (true, string.Empty);
        }

        public (bool Success, string Error) UpdateProfile(
            int userId, string fullName, string email, string department)
        {
            using var db = new AppDbContext();
            var user = db.Users.Find(userId);
            if (user == null) return (false, "User not found.");

            // Non-admins can only edit themselves
            if (!Session.IsAdmin && userId != Session.CurrentUser!.Id)
                return (false, "You can only edit your own profile.");

            if (db.Users.Any(u => u.Email.ToLower() == email.ToLower() && u.Id != userId))
                return (false, "Email address already in use.");

            user.FullName = fullName.Trim();
            user.Email = email.Trim().ToLower();
            user.Department = department;
            db.SaveChanges();

            _audit.Log(null, Session.CurrentUser!.Id, "ProfileUpdated",
                $"Updated profile for user '{user.Username}'");

            return (true, string.Empty);
        }
    }
}


// ============================================================
// FILE: Services/ExportService.cs
// Exports leave requests to Excel (.xlsx) using ClosedXML.
// ============================================================

namespace LeaveTrackerPro.Services
{
    public class ExportService
    {
        // -------------------------------------------------------
        // Export a list of leave requests to Excel
        // Returns the file path written
        // -------------------------------------------------------
        public string ExportToExcel(List<LeaveRequest> requests, string title = "Leave Requests")
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Leave Requests");

            // ---- Title row ----
            ws.Cell(1, 1).Value = title;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(2, 1).Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

            // ---- Header row (row 4) ----
            string[] headers = { "ID", "Employee", "Department", "Leave Type",
                                  "Start Date", "End Date", "Days", "Status",
                                  "Submitted On", "Reviewed By", "Manager Note" };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(4, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E50A0");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // ---- Data rows ----
            for (int r = 0; r < requests.Count; r++)
            {
                var req = requests[r];
                int row = r + 5;
                bool alt = r % 2 == 1;

                ws.Cell(row, 1).Value = req.Id;
                ws.Cell(row, 2).Value = req.User?.FullName ?? "";
                ws.Cell(row, 3).Value = req.User?.Department ?? "";
                ws.Cell(row, 4).Value = req.LeaveType;
                ws.Cell(row, 5).Value = req.StartDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 6).Value = req.EndDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 7).Value = req.TotalDays;
                ws.Cell(row, 8).Value = req.Status.ToString();
                ws.Cell(row, 9).Value = req.SubmittedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                ws.Cell(row, 10).Value = req.ReviewedByUser?.FullName ?? "";
                ws.Cell(row, 11).Value = req.ManagerNote;

                // Alternate row color
                if (alt)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F4FF");

                // Color-code status column
                var statusCell = ws.Cell(row, 8);
                statusCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                statusCell.Style.Font.Bold = true;
                statusCell.Style.Font.FontColor = req.Status switch
                {
                    LeaveStatus.Approved => XLColor.FromHtml("#1a7a3a"),
                    LeaveStatus.Rejected => XLColor.FromHtml("#b71c1c"),
                    LeaveStatus.Pending => XLColor.FromHtml("#b45309"),
                    LeaveStatus.Cancelled => XLColor.Gray,
                    _ => XLColor.Black
                };
            }

            // ---- Auto-fit columns ----
            ws.Columns().AdjustToContents();
            // Cap very wide columns
            foreach (var col in ws.ColumnsUsed())
                if (col.Width > 40) col.Width = 40;

            // ---- Add totals summary ----
            int summaryRow = requests.Count + 6;
            ws.Cell(summaryRow, 1).Value = "Total Records:";
            ws.Cell(summaryRow, 1).Style.Font.Bold = true;
            ws.Cell(summaryRow, 2).Value = requests.Count;

            ws.Cell(summaryRow + 1, 1).Value = "Total Days:";
            ws.Cell(summaryRow + 1, 1).Style.Font.Bold = true;
            ws.Cell(summaryRow + 1, 2).Value = requests.Sum(r => r.TotalDays);

            // ---- Save to Documents ----
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LeaveTrackerPro", "Exports");

            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"LeaveExport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            wb.SaveAs(path);
            return path;
        }
    }
}