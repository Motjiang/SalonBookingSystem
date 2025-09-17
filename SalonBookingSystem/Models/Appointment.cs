namespace SalonBookingSystem.Models
{
    public class Appointment
    {
        public int AppointmentID { get; set; }

        public int ClientID { get; set; }
        public Client Client { get; set; }

        public int StaffID { get; set; }
        public Staff Staff { get; set; }

        public int ServiceID { get; set; }
        public Service Service { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public string Status { get; set; } // Scheduled, Completed, Cancelled
    }
}
