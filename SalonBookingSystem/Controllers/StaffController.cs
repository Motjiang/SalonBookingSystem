using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SalonBookingSystem.Data;
using SalonBookingSystem.DTOs.Staff;
using SalonBookingSystem.Models;

namespace SalonBookingSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StaffController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private static readonly List<string> _staffCacheKeys = new();

        public StaffController(AppDbContext context, IMemoryCache cache, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _cache = cache;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        #region GET: api/staff
        /// <summary>
        /// Get paginated list of staff with optional search by name or role
        /// Admin only
        /// Caches results per page + search term
        /// </summary>
        /// 
        [Authorize(Roles = "Admin")]
        [HttpGet("get-all-staff")]
        [EnableRateLimiting("admin_crud")]
        public async Task<IActionResult> GetStaff(string? search = null, int page = 1, int pageSize = 10)
        {
            try
            {
                string cacheKey = GetStaffCacheKey(search, page, pageSize);

                if (!_cache.TryGetValue(cacheKey, out List<object> staffList))
                {
                    // Join Staff with ApplicationUser
                    var query = from s in _context.Staff
                                join u in _userManager.Users
                                on s.StaffID equals u.StaffID
                                where u.IsActive
                                select new
                                {
                                    s.StaffID,
                                    s.Designation,
                                    u.FirstName,
                                    u.LastName,
                                    u.Email
                                };

                    if (!string.IsNullOrEmpty(search))
                    {
                        query = query.Where(x =>
                            x.FirstName.Contains(search) ||
                            x.LastName.Contains(search) ||
                            x.Email.Contains(search) ||
                            x.Designation.Contains(search));
                    }

                    var totalCount = await query.CountAsync();

                    var staffPage = await query
                        .OrderBy(x => x.FirstName)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                    staffList = staffPage.Cast<object>().ToList();
                    _cache.Set(cacheKey, staffList, cacheOptions);

                    return Ok(new
                    {
                        Data = staffList,
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = totalCount
                    });
                }

                return Ok(new
                {
                    Data = staffList,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = staffList.Count
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error fetching staff." });
            }
        }


        #region GET: api/staff/{id}
        /// <summary>
        /// Get single staff by ID (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("get-staff/{id}")]
        public async Task<IActionResult> GetStaffById(int id)
        {
            try
            {
                var staff = await _context.Staff
                 .Join(_context.Users,
                     s => s.StaffID,
                     u => u.StaffID,
                     (s, u) => new { Staff = s, User = u })
                 .Where(x => x.Staff.StaffID == id && x.User.IsActive)
                 .Select(x => x.Staff)
                 .FirstOrDefaultAsync();

                if (staff == null)
                    return NotFound(new { Message = "Active staff not found." });

                return Ok(staff);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error fetching staff.", Details = ex.Message });
            }
        }

        #endregion

        #region POST: api/staff
        /// <summary>
        /// Add new staff (Admin only) + create corresponding ApplicationUser
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("add-staff")]
        [EnableRateLimiting("admin_crud")]
        public async Task<IActionResult> AddStaff([FromBody] CreateStaffDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (await StaffExists(dto.Email))
                    return BadRequest(new { Message = "Staff with the same email already exists." });

                // Create Staff entity first
                var staff = new Staff
                {                    
                    Designation = dto.Designation, // business role (e.g., Stylist, Barber, etc.)
                    Appointments = new List<Appointment>()
                };

                _context.Staff.Add(staff);
                await _context.SaveChangesAsync();

                // Create linked ApplicationUser
                var user = new ApplicationUser
                {
                    UserName = dto.Email,
                    Email = dto.Email,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    StaffID = staff.StaffID,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow,
                    IsActive = true,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, dto.Password);
                if (!result.Succeeded)
                {
                    // rollback staff if user creation fails
                    _context.Staff.Remove(staff);
                    await _context.SaveChangesAsync();

                    return BadRequest(new { Message = "Failed to create ApplicationUser."});
                }

                // Assign system role (always Staff)
                if (!await _roleManager.RoleExistsAsync(Constants.StaffRole))
                    await _roleManager.CreateAsync(new IdentityRole(Constants.StaffRole));

                await _userManager.AddToRoleAsync(user, Constants.StaffRole);

                RemoveStaffCache();

                return Ok(new { Message = "Staff added successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error adding staff."});
            }
        }
        #endregion

        #region PUT: api/staff/{id}
        /// <summary>
        /// Update staff by ID (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPatch("update-staff/{id}")]
        public async Task<IActionResult> UpdateStaff(int id, [FromBody] CreateStaffDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var staff = await _context.Staff.FindAsync(id);
                if (staff == null)
                    return NotFound(new { Message = "Staff not found." });

                if (await StaffExists(dto.Email, id))
                    return BadRequest(new { Message = "Another staff with the same email exists." });

                // Update Staff table               
                staff.Designation = dto.Designation;

                await _context.SaveChangesAsync();

                // Update ApplicationUser
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.StaffID == staff.StaffID);
                if (user != null)
                {
                    user.FirstName = dto.FirstName;
                    user.LastName = dto.LastName;
                    user.Email = dto.Email;
                    user.UserName = dto.Email;
                    user.DateModified = DateTime.UtcNow;

                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                        return BadRequest(new { Message = "Failed to update ApplicationUser."});

                    // Reset password if provided
                    if (!string.IsNullOrWhiteSpace(dto.Password))
                    {
                        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                        var passResult = await _userManager.ResetPasswordAsync(user, token, dto.Password);
                        if (!passResult.Succeeded)
                            return BadRequest(new { Message = "Failed to update password."});
                    }

                    // Ensure system role is always Staff
                    var roles = await _userManager.GetRolesAsync(user);
                    if (!roles.Contains(Constants.StaffRole))
                    {
                        await _userManager.RemoveFromRolesAsync(user, roles);
                        await _userManager.AddToRoleAsync(user, Constants.StaffRole);
                    }
                }

                RemoveStaffCache();

                return Ok(new { Message = "Staff updated successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error updating staff."});
            }
        }
        #endregion
        
        #region PUT: api/staff/lock-member/{id}
        /// <summary>
        /// Lock a member account (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("lock-member/{id}")]
        public async Task<IActionResult> LockMember(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { Message = "User not found." });

            // Lock for 2 days
            await _userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow.AddDays(2));

            return Ok(new { Message = $"User {user.Email} locked for 2 days." });
        }
        #endregion

        #region PUT: api/staff/unlock-member/{id}
        /// <summary>
        /// Unlock a member account (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("unlock-member/{id}")]
        public async Task<IActionResult> UnlockMember(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { Message = "User not found." });

            // Unlock immediately
            await _userManager.SetLockoutEndDateAsync(user, null);

            return Ok(new { Message = $"User {user.Email} unlocked successfully." });
        }
        #endregion

        #region DELETE: api/staff/{id}
        /// <summary>
        /// Delete staff by ID (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("delete-staff/{id}")]
        public async Task<IActionResult> DeleteStaff(int id)
        {
            try
            {
                // Find staff by id
                var staff = await _context.Staff.FindAsync(id);
                if (staff == null)
                    return NotFound(new { Message = "Staff not found." });

                // Get related ApplicationUser
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.StaffID == staff.StaffID);
                if (user == null)
                    return NotFound(new { Message = "Associated user not found." });

                // Soft delete → just mark as inactive
                user.IsActive = false;
                _context.Users.Update(user);

                // Save changes
                await _context.SaveChangesAsync();

                // If you have cache invalidation logic
                RemoveStaffCache();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error deleting staff.", Details = ex.Message });
            }
        }

        #endregion

        #region Private Methods
        /// <summary>
        /// Checks if staff email exists, optionally ignoring a specific staff by id
        /// </summary>
        private async Task<bool> StaffExists(string email, int? id = null)
        {
            email = email.ToLower().Trim();

            return await _userManager.Users
                .AnyAsync(u => u.Email.ToLower() == email &&
                               (!id.HasValue || u.StaffID != id.Value));
        }
        #endregion

        /// <summary>
        /// Generate a cache key for a given search term and page
        /// </summary>
        private string GetStaffCacheKey(string? search, int page, int pageSize)
        {
            string key = $"staff_{search}_{page}_{pageSize}";

            if (!_staffCacheKeys.Contains(key))
                _staffCacheKeys.Add(key);

            return key;
        }

        /// <summary>
        /// Remove all cached staff pages
        /// </summary>
        private void RemoveStaffCache()
        {
            foreach (var key in _staffCacheKeys)
            {
                _cache.Remove(key);
            }
            _staffCacheKeys.Clear();
        }
        #endregion
    }
}
