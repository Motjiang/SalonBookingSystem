using Microsoft.AspNetCore.SignalR;
using SalonBookingSystem.Models;
using SalonBookingSystem.System_Communication.Hubs;
using SalonBookingSystem.System_Communication.Interfaces;

namespace SalonBookingSystem.System_Communication.Implementation
{
    public class AppointmentNotificationService : IAppointmentNotificationService
    {
        private readonly IHubContext<AppointmentHub> _hubContext;

        public AppointmentNotificationService(IHubContext<AppointmentHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyAppointmentCreated(Appointment appointment)
        {
            // Send to client
            await _hubContext.Clients.User(appointment.ClientID.ToString())
                .SendAsync("AppointmentCreated", appointment);

            // Send to staff
            await _hubContext.Clients.User(appointment.StaffID.ToString())
                .SendAsync("AppointmentCreated", appointment);
        }

        public async Task NotifyAppointmentUpdated(Appointment appointment)
        {
            await _hubContext.Clients.User(appointment.ClientID.ToString())
                .SendAsync("AppointmentUpdated", appointment);

            await _hubContext.Clients.User(appointment.StaffID.ToString())
                .SendAsync("AppointmentUpdated", appointment);
        }

        public async Task NotifyAppointmentCancelled(int appointmentId, int clientId, int staffId)
        {
            await _hubContext.Clients.User(clientId.ToString())
                .SendAsync("AppointmentCancelled", appointmentId);

            await _hubContext.Clients.User(staffId.ToString())
                .SendAsync("AppointmentCancelled", appointmentId);
        }
    }
}
