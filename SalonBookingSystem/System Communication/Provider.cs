using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace SalonBookingSystem.System_Communication
{
    public class Provider : IUserIdProvider
    {
        public string GetUserId(HubConnectionContext connection)
        {
            // Use the "sub" claim or whichever claim represents your user ID
            return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
