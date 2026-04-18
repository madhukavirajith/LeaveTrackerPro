// ============================================================
// FILE: Services/AuthService.cs
// Handles user authentication (login / logout),
// password hashing with BCrypt, and password validation rules.
// BCrypt is the industry standard for password hashing —
// it automatically salts and is designed to be slow (safe).
// ============================================================

using LeaveTrackerPro.Data;
using LeaveTrackerPro.Models;
using LeaveTrackerPro.Helpers;
using Microsoft.EntityFrameworkCore;

namespace LeaveTrackerPro.Services
{
    public class AuthService
    {
        private readonly AuditService _audit;

        public AuthService(AuditService audit)
        {
            _audit = audit;
        }

        // -------------------------------------------------------
        // LOGIN
        // Returns (success, errorMessage)
        // -------------------------------------------------------
        public (bool Success, string Error) Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (false, "Username and password are required.");

            using var db = new AppDbContext();

            var user = db.Users
                .FirstOrDefault(u => u.Username.ToLower() == username.ToLower().Trim());

            if (user == null)
                return (false, "Invalid username or password.");

            if (!user.IsActive)
                return (false, "Your account has been deactivated. Contact your administrator.");

            // BCrypt.Verify compares the plain password against the stored hash
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                _audit.Log(null, user.Id, "LoginFailed", $"Failed login attempt for '{username}'");
                return (false, "Invalid username or password.");
            }

            // Update last login timestamp
            user.LastLoginAt = DateTime.UtcNow;
            db.SaveChanges();

            // Store in session
            Session.Login(user);

            _audit.Log(null, user.Id, "Login", $"User '{user.Username}' logged in successfully");

            return (true, string.Empty);
        }

        public void Logout()
        {
            if (Session.CurrentUser != null)
                _audit.Log(null, Session.CurrentUser.Id, "Logout", $"User '{Session.CurrentUser.Username}' logged out");

            Session.Logout();
        }

        // -------------------------------------------------------
        // CHANGE PASSWORD
        // -------------------------------------------------------
        public (bool Success, string Error) ChangePassword(int userId, string currentPassword, string newPassword)
        {
            var validation = ValidatePasswordStrength(newPassword);
            if (!validation.Valid)
                return (false, validation.Message);

            using var db = new AppDbContext();
            var user = db.Users.Find(userId);
            if (user == null) return (false, "User not found.");

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                return (false, "Current password is incorrect.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            db.SaveChanges();

            _audit.Log(null, userId, "PasswordChanged", "User changed their password");
            return (true, string.Empty);
        }

        // -------------------------------------------------------
        // RESET PASSWORD (Admin only)
        // -------------------------------------------------------
        public (bool Success, string Error) ResetPassword(int targetUserId, string newPassword)
        {
            if (!Session.IsAdmin)
                return (false, "Only administrators can reset passwords.");

            using var db = new AppDbContext();
            var user = db.Users.Find(targetUserId);
            if (user == null) return (false, "User not found.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            db.SaveChanges();

            _audit.Log(null, Session.CurrentUser!.Id, "PasswordReset",
                $"Admin reset password for user '{user.Username}'");

            return (true, string.Empty);
        }

        // -------------------------------------------------------
        // PASSWORD STRENGTH RULES
        // -------------------------------------------------------
        public static (bool Valid, string Message) ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return (false, "Password must be at least 8 characters long.");

            if (!password.Any(char.IsUpper))
                return (false, "Password must contain at least one uppercase letter.");

            if (!password.Any(char.IsLower))
                return (false, "Password must contain at least one lowercase letter.");

            if (!password.Any(char.IsDigit))
                return (false, "Password must contain at least one number.");

            if (!password.Any(c => "!@#$%^&*()-_=+[]{}|;':\",./<>?".Contains(c)))
                return (false, "Password must contain at least one special character (!@#$ etc).");

            return (true, string.Empty);
        }
    }
}