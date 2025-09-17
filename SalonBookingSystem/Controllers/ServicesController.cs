using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SalonBookingSystem.Data;
using SalonBookingSystem.DTOs.Service;
using SalonBookingSystem.Models;

namespace SalonBookingSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServicesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private static readonly List<string> _serviceCacheKeys = new();

        public ServicesController(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        #region GET: api/services
        /// <summary>
        /// Get paginated list of services with optional search by name
        /// Caches results per page + search term
        /// </summary>
        [HttpGet("get-all-services")]
        [EnableRateLimiting("public_get")]
        public async Task<IActionResult> GetServices(string? search = null,int page = 1,int pageSize = 10)
        {
            try
            {
                string cacheKey = GetServiceCacheKey(search, page, pageSize);

                if (!_cache.TryGetValue(cacheKey, out List<Service> services))
                {
                    IQueryable<Service> query = _context.Services.AsQueryable();

                    // Apply search filter if provided
                    if (!string.IsNullOrEmpty(search))
                    {
                        query = query.Where(s => s.Name.Contains(search));
                    }

                    // Apply pagination
                    services = await query
                        .OrderBy(s => s.Name)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    // Set cache options
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                    _cache.Set(cacheKey, services, cacheOptions);
                }

                return Ok(new
                {
                    Data = services,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = services.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while fetching services."});
            }
        }
        #endregion

        #region GET: api/services/{id}
        /// <summary>
        /// Get a single service by ID
        /// </summary>
        [HttpGet("get-service/{id}")]
        public async Task<IActionResult> GetServiceById(int id)
        {
            try
            {
                var service = await _context.Services.FindAsync(id);
                if (service == null)
                    return NotFound(new { Message = "Service not found." });

                return Ok(service);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "An error occurred while fetching the service.",
                    Details = ex.Message
                });
            }
        }
        #endregion

        #region POST: api/services
        /// <summary>
        /// Add a new service (Admin only)
        /// </summary>
        //[Authorize(Roles = "Admin")]
        [HttpPost("add-service")]
        [EnableRateLimiting("admin_crud")]
        public async Task<IActionResult> AddService([FromBody] SharedServiceDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Check if service with same name exists
                if (await ServiceExists(dto.Name))
                    return BadRequest(new { Message = "Service with the same name already exists." });

                var service = new Service
                {
                    Name = dto.Name,
                    DurationMinutes = dto.DurationMinutes,
                    Price = dto.Price,
                    Appointments = new List<Appointment>() // Initialize empty list
                };

                _context.Services.Add(service);
                await _context.SaveChangesAsync();

                // Remove cache for all pages since new service added
                RemoveServiceCache();

                return Ok(new { Message = "Service added successfully.", Service = service });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while adding service.", Details = ex.Message });
            }
        }
        #endregion

        #region PUT: api/services/{id}
        /// <summary>
        /// Update a service (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("update-service/{id}")]
        public async Task<IActionResult> UpdateService(int id, [FromBody] SharedServiceDto service)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var existingService = await _context.Services.FindAsync(id);
                if (existingService == null)
                    return NotFound(new { Message = "Service not found." });

                // Check if new name conflicts with another service
                if (await ServiceExists(service.Name, id))
                    return BadRequest(new { Message = "Another service with the same name already exists." });

                existingService.Name = service.Name;
                existingService.DurationMinutes = service.DurationMinutes;
                existingService.Price = service.Price;

                await _context.SaveChangesAsync();

                // Remove cache for all pages
                RemoveServiceCache();

                return Ok(new { Message = "Service updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while updating service.", Details = ex.Message });
            }
        }
        #endregion

        #region DELETE: api/services/{id}
        /// <summary>
        /// Delete a service (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("delete-service/{id}")]
        public async Task<IActionResult> DeleteService(int id)
        {
            try
            {
                var service = await _context.Services.FindAsync(id);
                if (service == null)
                    return NotFound(new { Message = "Service not found." });

                _context.Services.Remove(service);
                await _context.SaveChangesAsync();

                // Remove cache for all pages
                RemoveServiceCache();

                return Ok(new { Message = "Service deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while deleting service.", Details = ex.Message });
            }
        }
        #endregion       

        #region Private Methods
        /// <summary>
        /// Checks if a service with the given name exists, optionally ignoring a specific service by id
        /// </summary>
        private async Task<bool> ServiceExists(string name, int? id = null)
        {
            return await _context.Services.AnyAsync(s => s.Name.ToLower() == name.ToLower().Trim() &&
                (!id.HasValue || s.ServiceID != id.Value)); // ignore current service if id is provided
        }

        /// <summary>
        /// Generate a cache key for a given search term and page
        /// </summary>
        private string GetServiceCacheKey(string? search, int page, int pageSize)
        {
            string key = $"services_{search}_{page}_{pageSize}";

            // Track the key if not already tracked
            if (!_serviceCacheKeys.Contains(key))
                _serviceCacheKeys.Add(key);

            return key;
        }

        /// <summary>
        /// Remove all cached service pages
        /// </summary>
        private void RemoveServiceCache()
        {
            foreach (var key in _serviceCacheKeys)
            {
                _cache.Remove(key);
            }

            // Clear the tracked keys list
            _serviceCacheKeys.Clear();
        }
        #endregion
    }
}
