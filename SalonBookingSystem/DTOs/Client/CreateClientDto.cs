using System.ComponentModel.DataAnnotations;

namespace SalonBookingSystem.DTOs.Client
{
    public class CreateClientDto
    {
        [Required]
        [StringLength(15, MinimumLength = 3, ErrorMessage = "First name must be at least {2} and maximum {1} characters.")]
        public string FirstName { get; set; }

        [Required]
        [StringLength(15, MinimumLength = 3, ErrorMessage = "Last name must be at least {2} and maximum {1} characters.")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }

        [Required]
        [StringLength(15, MinimumLength = 6, ErrorMessage = "Password must be at least {2} and maximum {1} characters.")]
        public string Password { get; set; }
    }
}
