using Microsoft.EntityFrameworkCore;

namespace SalonBookingSystem.Models
{
    public class Service
    {
        public int ServiceID { get; set; }

        public string Name { get; set; }
        public int DurationMinutes { get; set; }
        [Precision(18, 2)]
        public decimal Price { get; set; }

        // Navigation
        public ICollection<Appointment> Appointments { get; set; }
    }

}
