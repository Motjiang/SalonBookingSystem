namespace SalonBookingSystem.Data
{
    public class Constants
    {
        // Designations
        public const string AdminRole = "Admin";
        public const string StaffRole = "Staff";
        public const string ClientRole = "Client";

        // Default User names
        public const string AdminUserName = "admin@example.com";
        public const string StaffUserName = "staff@example.com";

        // Login attempts restriction
        public const int MaximumLoginAttempts = 3;
    }
}
