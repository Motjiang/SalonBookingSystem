using System.ComponentModel.DataAnnotations;

namespace SalonBookingSystem.DTOs.Staff
{
    public class SharedStaffDto
    {
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [RegularExpression("^\\w+@[a-zA-Z_]+?\\.[a-zA-Z]{2,3}$", ErrorMessage = "Invalid email address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Designation is required.")]
        [StringLength(100, ErrorMessage = "Designation cannot exceed 100 characters.")]
        public string Designation { get; set; }
    }
}
