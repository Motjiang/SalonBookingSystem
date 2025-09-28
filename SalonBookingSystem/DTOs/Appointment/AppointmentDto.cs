namespace SalonBookingSystem.DTOs.Appointment
{
    public class AppointmentDto
    {
        public int StaffID { get; set; }

        public int ServiceID { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}
