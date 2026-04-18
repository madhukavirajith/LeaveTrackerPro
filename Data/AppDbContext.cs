// ============================================================
// FILE: Data/AppDbContext.cs
// Entity Framework Core database context.
// This class is the bridge between your C# objects and the
// SQLite database file. EF Core uses it to:
//   - Create the database tables automatically
//   - Run queries (SELECT, INSERT, UPDATE, DELETE)
//   - Track changes and save them
// ============================================================

using Microsoft.EntityFrameworkCore;
using LeaveTrackerPro.Models;

namespace LeaveTrackerPro.Data
{
    public class AppDbContext : DbContext
    {
        // Each DbSet<T> = one table in the database
        public DbSet<User> Users { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveBalance> LeaveBalances { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }

        // Path to the SQLite file in the user's Documents folder
        private static readonly string DbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LeaveTrackerPro",
            "leavetracker.db"
        );

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Ensure folder exists before EF tries to create the DB file
            string folder = Path.GetDirectoryName(DbPath)!;
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            options.UseSqlite($"Data Source={DbPath}");

            // Log EF queries to the debug output window during development
#if DEBUG
            options.EnableSensitiveDataLogging();
            options.LogTo(msg => System.Diagnostics.Debug.WriteLine(msg),
                         Microsoft.Extensions.Logging.LogLevel.Information);
#endif
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ---- UNIQUE CONSTRAINTS ----
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email).IsUnique();

            // One user/year/leavetype combination = one balance row
            modelBuilder.Entity<LeaveBalance>()
                .HasIndex(b => new { b.UserId, b.Year, b.LeaveType }).IsUnique();

            // ---- RELATIONSHIPS ----

            // User → many LeaveRequests (submitted by)
            modelBuilder.Entity<LeaveRequest>()
                .HasOne(r => r.User)
                .WithMany(u => u.LeaveRequests)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // LeaveRequest → reviewed by (manager) — optional FK
            modelBuilder.Entity<LeaveRequest>()
                .HasOne(r => r.ReviewedByUser)
                .WithMany()
                .HasForeignKey(r => r.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // AuditLog → LeaveRequest (optional)
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.LeaveRequest)
                .WithMany(r => r.AuditLogs)
                .HasForeignKey(a => a.LeaveRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            // AuditLog → actor user
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.ActorUser)
                .WithMany()
                .HasForeignKey(a => a.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- ENUM STORAGE — store as string for readability ----
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();
            modelBuilder.Entity<LeaveRequest>()
                .Property(r => r.Status)
                .HasConversion<string>();

            // ---- SEED DATA — default admin account ----
            // Password: Admin@123  (user must change on first login)
            modelBuilder.Entity<User>().HasData(new User
            {
                Id = 1,
                FullName = "System Administrator",
                Email = "admin@company.com",
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Department = "IT",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });

            // Default app settings
            modelBuilder.Entity<AppSetting>().HasData(
                new AppSetting { Key = "SmtpHost", Value = "smtp.gmail.com" },
                new AppSetting { Key = "SmtpPort", Value = "587" },
                new AppSetting { Key = "SmtpUser", Value = "" },
                new AppSetting { Key = "SmtpPassword", Value = "" },
                new AppSetting { Key = "SmtpFrom", Value = "noreply@company.com" },
                new AppSetting { Key = "CompanyName", Value = "My Company" },
                new AppSetting { Key = "EmailEnabled", Value = "false" }
            );
        }

        // -------------------------------------------------------
        // Helper: apply any pending migrations and create DB
        // Called once at app startup in Program.cs
        // -------------------------------------------------------
        public static void InitializeDatabase()
        {
            using var context = new AppDbContext();
            context.Database.EnsureCreated();
        }

        public static string GetDbPath() => DbPath;
    }
}