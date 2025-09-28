namespace SalonBookingSystem.DTOs.Appointment
{
    public class UpdateAppointmentDto : AppointmentDto
    {
        public int AppointmentID { get; set; }
        public string Status { get; set; } // Scheduled, Completed, Cancelled
    }
}
