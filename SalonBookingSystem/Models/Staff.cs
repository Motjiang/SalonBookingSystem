namespace SalonBookingSystem.Models
{
    public class Staff
    {
        public int StaffID { get; set; }
        public string Designation { get; set; } // e.g., Stylist, Barber, Manager

        // Navigation
        public ICollection<Appointment> Appointments { get; set; }
    }

}
