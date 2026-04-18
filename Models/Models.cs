// ============================================================
// FILE: Models/Models.cs
// All domain models (database tables) in one place.
// Each class maps to one table in the SQLite database.
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeaveTrackerPro.Models
{
    // ----------------------------------------------------------
    // ROLES: what level of access a user has
    // ----------------------------------------------------------
    public enum UserRole
    {
        Employee = 0,
        Manager = 1,
        Admin = 2
    }

    // ----------------------------------------------------------
    // LEAVE STATUS
    // ----------------------------------------------------------
    public enum LeaveStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        Cancelled = 3
    }

    // ----------------------------------------------------------
    // USER — one row per person who can log in
    // ----------------------------------------------------------
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        // BCrypt hashed — never store plain text passwords
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(80)]
        public string Department { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.Employee;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        // Navigation: one user has many leave requests
        public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();

        // Navigation: one user has many leave balances (one per year)
        public ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
    }

    // ----------------------------------------------------------
    // LEAVE REQUEST — one row per request submitted
    // ----------------------------------------------------------
    public class LeaveRequest
    {
        [Key]
        public int Id { get; set; }

        // Foreign key — which user submitted this
        public int UserId { get; set; }
        public User? User { get; set; }

        [Required, MaxLength(80)]
        public string LeaveType { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // Foreign key — which manager reviewed it (nullable until reviewed)
        public int? ReviewedByUserId { get; set; }
        public User? ReviewedByUser { get; set; }

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(500)]
        public string ManagerNote { get; set; } = string.Empty;

        // Calculated — stored for performance (avoids recalculating on every query)
        public int TotalDays { get; set; }

        // Navigation: each request has audit log entries
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }

    // ----------------------------------------------------------
    // LEAVE BALANCE — tracks how many days each employee
    // has used / has remaining for a given year
    // ----------------------------------------------------------
    public class LeaveBalance
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int Year { get; set; }

        [Required, MaxLength(80)]
        public string LeaveType { get; set; } = string.Empty;

        public int TotalEntitlement { get; set; } = 14; // Days per year
        public int DaysUsed { get; set; } = 0;
        public int DaysPending { get; set; } = 0;

        [NotMapped] // Calculated, not stored
        public int DaysRemaining => TotalEntitlement - DaysUsed - DaysPending;
    }

    // ----------------------------------------------------------
    // AUDIT LOG — immutable record of every important action
    // (who did what, when, on which request)
    // ----------------------------------------------------------
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        // Which request this relates to (nullable — some actions are user-level)
        public int? LeaveRequestId { get; set; }
        public LeaveRequest? LeaveRequest { get; set; }

        // Who performed the action
        public int ActorUserId { get; set; }
        public User? ActorUser { get; set; }

        [Required, MaxLength(80)]
        public string Action { get; set; } = string.Empty; // e.g. "Submitted", "Approved", "Login"

        [MaxLength(500)]
        public string Details { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // IP / machine name for traceability
        [MaxLength(100)]
        public string MachineName { get; set; } = Environment.MachineName;
    }

    // ----------------------------------------------------------
    // EMAIL SETTINGS — stored in DB so admin can change them
    // without touching code
    // ----------------------------------------------------------
    public class AppSetting
    {
        [Key, MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Value { get; set; } = string.Empty;
    }
}