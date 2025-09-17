using SalonBookingSystem.Models;

namespace SalonBookingSystem.System_Communication.Interfaces
{
    public interface IAppointmentNotificationService
    {
        Task NotifyAppointmentCreated(Appointment appointment);
        Task NotifyAppointmentUpdated(Appointment appointment);
        Task NotifyAppointmentCancelled(int appointmentId, int clientId, int staffId);
    }
}
