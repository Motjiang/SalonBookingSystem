namespace SalonBookingSystem.DTOs.Appointment
{
    public class AppointmentDto
    {
        public int AppointmentID { get; set; }
        public int ClientID { get; set; }
        public string ClientName { get; set; }

        public int StaffID { get; set; }
        public string StaffName { get; set; }
        public string StaffUserId { get; set; } // ApplicationUser.Id of the staff

        public int ServiceID { get; set; }
        public string ServiceName { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public string Status { get; set; }
    }
}
