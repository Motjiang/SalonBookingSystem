using System.ComponentModel.DataAnnotations;

namespace SalonBookingSystem.DTOs.Service
{
    public class SharedServiceDto
    {
        [Required(ErrorMessage = "Service name is required.")]
        [StringLength(100, ErrorMessage = "Service name cannot exceed 100 characters.")]
        public string Name { get; set; }

        [Range(1, 1440, ErrorMessage = "Duration must be between 1 and 1440 minutes (24 hours).")]
        public int DurationMinutes { get; set; }

        [Range(0.01, 10000, ErrorMessage = "Price must be between 0.01 and 10,000.")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }
    }
}
