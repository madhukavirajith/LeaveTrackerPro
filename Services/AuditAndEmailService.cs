// ============================================================
// FILE: Services/AuditService.cs
// Writes immutable audit log entries to the database.
// Every important action (login, submit, approve, reject)
// gets recorded with who did it, when, and what.
// ============================================================

using LeaveTrackerPro.Data;
using LeaveTrackerPro.Helpers;
using LeaveTrackerPro.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace LeaveTrackerPro.Services
{
    public class AuditService
    {
        public void Log(int? requestId, int actorUserId, string action, string details = "")
        {
            try
            {
                using var db = new AppDbContext();
                db.AuditLogs.Add(new AuditLog
                {
                    LeaveRequestId = requestId,
                    ActorUserId = actorUserId,
                    Action = action,
                    Details = details,
                    Timestamp = DateTime.UtcNow,
                    MachineName = Environment.MachineName
                });
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                // Audit failures must never crash the app
                System.Diagnostics.Debug.WriteLine($"[AUDIT ERROR] {ex.Message}");
            }
        }

        public List<AuditLog> GetLogs(int? requestId = null, int? userId = null, int maxRows = 200)
        {
            using var db = new AppDbContext();
            var q = db.AuditLogs
                .Include(a => a.ActorUser)
                .Include(a => a.LeaveRequest)
                .AsQueryable();

            if (requestId.HasValue) q = q.Where(a => a.LeaveRequestId == requestId);
            if (userId.HasValue) q = q.Where(a => a.ActorUserId == userId);

            return q.OrderByDescending(a => a.Timestamp).Take(maxRows).ToList();
        }
    }
}


// ============================================================
// FILE: Services/EmailService.cs
// Sends email notifications using MailKit (SMTP).
// Email settings are loaded from the AppSettings table
// so an admin can configure them without touching code.
// If email is disabled or misconfigured, failures are
// caught silently — email must never crash the app.
// ============================================================


namespace LeaveTrackerPro.Services
{
    public class EmailService
    {
        private Dictionary<string, string> LoadSettings()
        {
            using var db = new AppDbContext();
            return db.AppSettings.ToDictionary(s => s.Key, s => s.Value);
        }

        // -------------------------------------------------------
        // SEND: new request submitted — notify managers
        // -------------------------------------------------------
        public async Task SendNewRequestNotificationAsync(LeaveRequest request, User manager)
        {
            var settings = LoadSettings();
            if (!IsEmailEnabled(settings)) return;

            string subject = $"New Leave Request — {request.User?.FullName}";
            string body = $@"
<h2>New Leave Request Submitted</h2>
<table style='font-family:sans-serif;border-collapse:collapse'>
  <tr><td style='padding:6px 12px;color:#666'>Employee</td><td style='padding:6px 12px'><b>{request.User?.FullName}</b></td></tr>
  <tr><td style='padding:6px 12px;color:#666'>Department</td><td style='padding:6px 12px'>{request.User?.Department}</td></tr>
  <tr><td style='padding:6px 12px;color:#666'>Leave Type</td><td style='padding:6px 12px'>{request.LeaveType}</td></tr>
  <tr><td style='padding:6px 12px;color:#666'>Dates</td><td style='padding:6px 12px'>{request.StartDate:dd/MM/yyyy} – {request.EndDate:dd/MM/yyyy} ({request.TotalDays} working day(s))</td></tr>
  <tr><td style='padding:6px 12px;color:#666'>Reason</td><td style='padding:6px 12px'>{request.Reason}</td></tr>
</table>
<p style='margin-top:16px'>Please open the Leave Request Tracker to approve or reject this request.</p>
";
            await SendAsync(settings, manager.Email, subject, body);
        }

        // -------------------------------------------------------
        // SEND: request approved — notify employee
        // -------------------------------------------------------
        public async Task SendApprovalNotificationAsync(LeaveRequest request)
        {
            var settings = LoadSettings();
            if (!IsEmailEnabled(settings) || request.User == null) return;

            string subject = $"Leave Request Approved — {request.LeaveType}";
            string body = $@"
<h2 style='color:#1a7a3a'>Your Leave Request Has Been Approved</h2>
<p>Your {request.LeaveType} request from <b>{request.StartDate:dd/MM/yyyy}</b> to <b>{request.EndDate:dd/MM/yyyy}</b> ({request.TotalDays} day(s)) has been <b style='color:#1a7a3a'>approved</b>.</p>
{(string.IsNullOrEmpty(request.ManagerNote) ? "" : $"<p><b>Manager note:</b> {request.ManagerNote}</p>")}
";
            await SendAsync(settings, request.User.Email, subject, body);
        }

        // -------------------------------------------------------
        // SEND: request rejected — notify employee
        // -------------------------------------------------------
        public async Task SendRejectionNotificationAsync(LeaveRequest request)
        {
            var settings = LoadSettings();
            if (!IsEmailEnabled(settings) || request.User == null) return;

            string subject = $"Leave Request Rejected — {request.LeaveType}";
            string body = $@"
<h2 style='color:#b71c1c'>Your Leave Request Has Been Rejected</h2>
<p>Your {request.LeaveType} request from <b>{request.StartDate:dd/MM/yyyy}</b> to <b>{request.EndDate:dd/MM/yyyy}</b> has been <b style='color:#b71c1c'>rejected</b>.</p>
{(string.IsNullOrEmpty(request.ManagerNote) ? "" : $"<p><b>Reason:</b> {request.ManagerNote}</p>")}
<p>Please contact your manager if you have questions.</p>
";
            await SendAsync(settings, request.User.Email, subject, body);
        }

        // -------------------------------------------------------
        // CORE SEND METHOD using MailKit
        // -------------------------------------------------------
        private async Task SendAsync(Dictionary<string, string> s, string toEmail, string subject, string htmlBody)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(s.GetValueOrDefault("SmtpFrom", "noreply@company.com")));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;
                message.Body = new TextPart("html") { Text = WrapHtml(htmlBody, s.GetValueOrDefault("CompanyName", "")) };

                using var client = new SmtpClient();
                await client.ConnectAsync(
                    s.GetValueOrDefault("SmtpHost", "smtp.gmail.com"),
                    int.Parse(s.GetValueOrDefault("SmtpPort", "587")),
                    SecureSocketOptions.StartTls);

                string user = s.GetValueOrDefault("SmtpUser", "");
                string pass = s.GetValueOrDefault("SmtpPassword", "");
                if (!string.IsNullOrEmpty(user))
                    await client.AuthenticateAsync(user, pass);

                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Email errors are logged but never crash the app
                System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR] {ex.Message}");
            }
        }

        private static bool IsEmailEnabled(Dictionary<string, string> settings)
            => settings.GetValueOrDefault("EmailEnabled", "false").ToLower() == "true"
            && !string.IsNullOrEmpty(settings.GetValueOrDefault("SmtpHost", ""));

        private static string WrapHtml(string body, string company) => $@"
<!DOCTYPE html><html><body style='font-family:Segoe UI,sans-serif;max-width:600px;margin:auto;color:#222'>
  <div style='background:#1E50A0;padding:16px 24px;border-radius:8px 8px 0 0'>
    <h1 style='color:white;margin:0;font-size:18px'>{company} — Leave Request Tracker</h1>
  </div>
  <div style='background:#f9f9f9;padding:24px;border:1px solid #ddd;border-radius:0 0 8px 8px'>
    {body}
    <hr style='border:none;border-top:1px solid #eee;margin-top:24px'>
    <p style='color:#999;font-size:12px'>This is an automated message. Do not reply.</p>
  </div>
</body></html>";
    }
}