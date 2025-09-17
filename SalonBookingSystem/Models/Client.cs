namespace SalonBookingSystem.Models
{
    public class Client
    {
        public int ClientID { get; set; }

        // Navigation
        public ICollection<Appointment> Appointments { get; set; }
    }

}
