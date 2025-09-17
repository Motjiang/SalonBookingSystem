using Microsoft.AspNetCore.SignalR;
using SalonBookingSystem.Models;
using System.Threading.Tasks;

namespace SalonBookingSystem.System_Communication.Hubs
{
    public class AppointmentHub : Hub
    {
        // Called when a client or staff connects
        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier; // Use JWT's user id
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            }

            await base.OnConnectedAsync();
        }

        // Called when a client or staff disconnects
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Notify only the client and staff about the new appointment
        public async Task SendAppointmentCreated(Appointment appointment)
        {
            var clientId = appointment.ClientID.ToString();
            var staffId = appointment.StaffID.ToString();

            // Send to client
            await Clients.Group(clientId)
                .SendAsync("AppointmentCreated", appointment);

            // Send to staff
            await Clients.Group(staffId)
                .SendAsync("AppointmentCreated", appointment);
        }

        // Similarly for updates
        public async Task SendAppointmentUpdated(Appointment appointment)
        {
            var clientId = appointment.ClientID.ToString();
            var staffId = appointment.StaffID.ToString();

            await Clients.Group(clientId).SendAsync("AppointmentUpdated", appointment);
            await Clients.Group(staffId).SendAsync("AppointmentUpdated", appointment);
        }

        public async Task SendAppointmentCancelled(int appointmentId, int clientId, int staffId)
        {
            await Clients.Group(clientId.ToString()).SendAsync("AppointmentCancelled", appointmentId);
            await Clients.Group(staffId.ToString()).SendAsync("AppointmentCancelled", appointmentId);
        }
    }
}
