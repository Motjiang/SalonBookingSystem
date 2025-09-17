using System.ComponentModel.DataAnnotations;

namespace SalonBookingSystem.DTOs.Staff
{
    public class CreateStaffDto : SharedStaffDto
    {
        [Required]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
        public string Password { get; set; }
    }
}
