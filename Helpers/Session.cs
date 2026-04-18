// ============================================================
// FILE: Helpers/Session.cs
// Tracks the currently logged-in user for the whole app.
// After login, set Session.CurrentUser once.
// Every form checks Session.CurrentUser to know who is
// using the app and what they are allowed to do.
// ============================================================

using LeaveTrackerPro.Models;

namespace LeaveTrackerPro.Helpers
{
    public static class Session
    {
        // The logged-in user — null means nobody is logged in
        public static User? CurrentUser { get; private set; }

        public static bool IsLoggedIn => CurrentUser != null;

        // Shortcut role checks used throughout the app
        public static bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
        public static bool IsManager => CurrentUser?.Role == UserRole.Manager || IsAdmin;
        public static bool IsEmployee => CurrentUser?.Role == UserRole.Employee;

        public static void Login(User user)
        {
            CurrentUser = user;
        }

        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}