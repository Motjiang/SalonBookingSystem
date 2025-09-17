using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonBookingSystem.Data;
using SalonBookingSystem.Models;
using SalonBookingSystem.System_Communication.Interfaces;

namespace SalonBookingSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAppointmentNotificationService _notificationService;

        public AppointmentController(AppDbContext context, IAppointmentNotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        #region POST: api/appointment
        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateAppointment([FromBody] Appointment appointment)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                await _notificationService.NotifyAppointmentCreated(appointment);

                return Ok(new { Message = "Appointment created successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error creating appointment"});
            }
        }
        #endregion

        #region PUT: api/appointment/{id}
        [Authorize]
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateAppointment(int id, [FromBody] Appointment updatedAppointment)
        {
            try
            {
                var appointment = await _context.Appointments.FindAsync(id);
                if (appointment == null)
                    return NotFound(new { Message = "Appointment not found" });

                appointment.StartTime = updatedAppointment.StartTime;
                appointment.EndTime = updatedAppointment.EndTime;
                appointment.Status = updatedAppointment.Status;
                appointment.ServiceID = updatedAppointment.ServiceID;
                appointment.StaffID = updatedAppointment.StaffID;

                await _context.SaveChangesAsync();

                await _notificationService.NotifyAppointmentUpdated(appointment);

                return Ok(new { Message = "Appointment updated successfully"});
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error updating appointment"});
            }
        }
        #endregion

        #region DELETE: api/appointment/{id}
        [Authorize]
        [HttpDelete("cancel/{id}")]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            try
            {
                var appointment = await _context.Appointments.FindAsync(id);
                if (appointment == null)
                    return NotFound(new { Message = "Appointment not found" });

                _context.Appointments.Remove(appointment);
                await _context.SaveChangesAsync();

                await _notificationService.NotifyAppointmentCancelled(appointment.AppointmentID, appointment.ClientID, appointment.StaffID);

                return Ok(new { Message = "Appointment cancelled successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error cancelling appointment"});
            }
        }
        #endregion
    }
}
