using Microsoft.AspNetCore.Identity;

namespace SalonBookingSystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime DateCreated { get; set; } 
        public DateTime DateModified { get; set; }
        public bool IsActive { get; set; }

        // Optional: link to Client or Staff (nullable FKs)
        public int? ClientID { get; set; }
        public Client Client { get; set; }

        public int? StaffID { get; set; }
        public Staff Staff { get; set; }
    }
}
