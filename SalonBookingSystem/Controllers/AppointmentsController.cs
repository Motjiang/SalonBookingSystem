using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonBookingSystem.Data;
using SalonBookingSystem.DTOs.Appointment;
using SalonBookingSystem.Models;
using SalonBookingSystem.System_Communication.Interfaces;
using System.Security.Claims;

namespace SalonBookingSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAppointmentNotificationService _notificationService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppointmentsController(AppDbContext context, IAppointmentNotificationService notificationService, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _notificationService = notificationService;
            _httpContextAccessor = httpContextAccessor;
        }

        [Authorize(Roles = "Client")]
        [HttpPost("schedule-appointment")]
        public async Task<IActionResult> CreateAppointment([FromBody] AppointmentDto appointmentDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // ✅ prevent booking in the past
                if (appointmentDto.StartTime < DateTime.Now)
                    return BadRequest(new { Message = "Cannot book an appointment in the past." });

                // ✅ restrict business hours
                var startHour = appointmentDto.StartTime.TimeOfDay;
                var dayOfWeek = appointmentDto.StartTime.DayOfWeek;

                bool isValidTime = false;

                if (dayOfWeek >= DayOfWeek.Monday && dayOfWeek <= DayOfWeek.Friday)
                {
                    // Monday to Friday: 08:00 - 17:00
                    isValidTime = startHour >= TimeSpan.FromHours(8) && appointmentDto.EndTime.TimeOfDay <= TimeSpan.FromHours(17);
                }
                else if (dayOfWeek == DayOfWeek.Saturday)
                {
                    // Saturday: 08:00 - 13:00
                    isValidTime = startHour >= TimeSpan.FromHours(8) && appointmentDto.EndTime.TimeOfDay <= TimeSpan.FromHours(13);
                }
                else
                {
                    // Sunday: Closed
                    return BadRequest(new { Message = "Appointments cannot be booked on Sunday." });
                }

                if (!isValidTime)
                    return BadRequest(new { Message = "Appointments must be within business hours: Mon-Fri 08:00-17:00, Sat 08:00-13:00." });

                // ✅ get logged-in user's Identity Id (string)
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { Message = "User not logged in." });

                // ✅ join ApplicationUser and Client to find the client record
                var client = await (from c in _context.Clients
                                    join u in _context.Users
                                    on c.ClientID equals u.ClientID
                                    where u.Id == userId
                                    select c)
                                   .FirstOrDefaultAsync();

                if (client == null)
                    return Unauthorized(new { Message = "Client not found or inactive." });

                // ✅ check if staff is available
                bool isBooked = await IsStaffBookedAsync(
                    appointmentDto.StaffID,
                    appointmentDto.StartTime,
                    appointmentDto.EndTime
                );

                if (isBooked)
                {
                    // Suggest next available time (e.g., add 30 minutes)
                    var suggestedStart = appointmentDto.EndTime.AddMinutes(30);
                    var suggestedEnd = suggestedStart.Add(appointmentDto.EndTime - appointmentDto.StartTime);

                    return Conflict(new
                    {
                        Message = "Staff is already booked for the selected time.",
                        SuggestedStartTime = suggestedStart,
                        SuggestedEndTime = suggestedEnd
                    });
                }

                // ✅ map DTO to entity
                var appointment = new Appointment
                {
                    ClientID = client.ClientID,  // int PK from Client table
                    StaffID = appointmentDto.StaffID,
                    ServiceID = appointmentDto.ServiceID,
                    StartTime = appointmentDto.StartTime,
                    EndTime = appointmentDto.EndTime,
                    Status = "Scheduled"
                };

                // Save appointment
                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                // Send notification
                await _notificationService.NotifyAppointmentCreated(appointment);

                return Ok(new { Message = "Appointment scheduled successfully", AppointmentId = appointment.AppointmentID });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error creating appointment", Details = ex.Message });
            }
        }

        #region PUT: api/appointment/{id}
        [Authorize(Roles = "Client")]
        [HttpPut("update-appointment/{id}")]
        public async Task<IActionResult> UpdateAppointment(int id, [FromBody] UpdateAppointmentDto updatedAppointmentDto)
        {
            try
            {
                // Find existing appointment
                var appointment = await _context.Appointments.FindAsync(id);
                if (appointment == null)
                    return NotFound(new { Message = "Appointment not found" });

                // ✅ prevent updating to a past time
                if (updatedAppointmentDto.StartTime < DateTime.Now)
                    return BadRequest(new { Message = "Cannot set appointment in the past." });

                // ✅ check business hours
                var startHour = updatedAppointmentDto.StartTime.TimeOfDay;
                var dayOfWeek = updatedAppointmentDto.StartTime.DayOfWeek;
                bool isValidTime = false;

                if (dayOfWeek >= DayOfWeek.Monday && dayOfWeek <= DayOfWeek.Friday)
                    isValidTime = startHour >= TimeSpan.FromHours(8) && updatedAppointmentDto.EndTime.TimeOfDay <= TimeSpan.FromHours(17);
                else if (dayOfWeek == DayOfWeek.Saturday)
                    isValidTime = startHour >= TimeSpan.FromHours(8) && updatedAppointmentDto.EndTime.TimeOfDay <= TimeSpan.FromHours(13);
                else
                    return BadRequest(new { Message = "Appointments cannot be scheduled on Sunday." });

                if (!isValidTime)
                    return BadRequest(new { Message = "Appointment time must be within business hours." });

                // ✅ check if staff is available for the new time, excluding current appointment
                bool isBooked = await IsStaffBookedAsync(
                    updatedAppointmentDto.StaffID,
                    updatedAppointmentDto.StartTime,
                    updatedAppointmentDto.EndTime,
                   excludeAppointmentId: id
                );

                if (isBooked)
                {
                    var suggestedStart = updatedAppointmentDto.EndTime.AddMinutes(30);
                    var suggestedEnd = suggestedStart.Add(updatedAppointmentDto.EndTime - updatedAppointmentDto.StartTime);

                    return Conflict(new
                    {
                        Message = "Staff is already booked for the selected time.",
                        SuggestedStartTime = suggestedStart,
                        SuggestedEndTime = suggestedEnd
                    });
                }

                // ✅ map DTO to entity
                appointment.StartTime = updatedAppointmentDto.StartTime;
                appointment.EndTime = updatedAppointmentDto.EndTime;
                appointment.ServiceID = updatedAppointmentDto.ServiceID;
                appointment.StaffID = updatedAppointmentDto.StaffID;
                appointment.Status = updatedAppointmentDto.Status;

                await _context.SaveChangesAsync();

                await _notificationService.NotifyAppointmentUpdated(appointment);

                return Ok(new { Message = "Appointment re-scheduled successfully", AppointmentId = appointment.AppointmentID });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error updating appointment", Details = ex.Message });
            }
        }
        #endregion

        #region PATCH: api/appointment/{id}/cancel
        [Authorize(Roles = "Client,Staff")]
        [HttpPatch("cancel/{id}")]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            try
            {
                // Find existing appointment
                var appointment = await _context.Appointments.FindAsync(id);
                if (appointment == null)
                    return NotFound(new { Message = "Appointment not found" });

                // Update status to Cancelled
                appointment.Status = "Cancelled";

                await _context.SaveChangesAsync();

                // Notify staff and client
                await _notificationService.NotifyAppointmentCancelled(
                    appointment.AppointmentID,
                    appointment.ClientID,
                    appointment.StaffID
                );

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error cancelling appointment", Details = ex.Message });
            }
        }
        #endregion


        #region Private Methods
        private async Task<bool> IsStaffBookedAsync(int staffId, DateTime start, DateTime end, int? excludeAppointmentId = null)
        {
            return await _context.Appointments.AnyAsync(a =>
                a.StaffID == staffId &&
                a.Status == "Scheduled" &&
                (excludeAppointmentId == null || a.AppointmentID != excludeAppointmentId) && // ✅ exclude current appointment if updating
                (
                    (start >= a.StartTime && start < a.EndTime) ||    // new start inside existing
                    (end > a.StartTime && end <= a.EndTime) ||        // new end inside existing
                    (start <= a.StartTime && end >= a.EndTime)        // new covers existing
                )
            );
        }

        #endregion
    }
}
