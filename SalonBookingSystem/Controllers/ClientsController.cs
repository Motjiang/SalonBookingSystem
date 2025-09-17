using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SalonBookingSystem.Data;
using SalonBookingSystem.DTOs.Client;
using SalonBookingSystem.Models;

namespace SalonBookingSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private static readonly List<string> _clientCacheKeys = new();

        public ClientsController(AppDbContext context, IMemoryCache cache, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _cache = cache;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        #region GET: api/clients
        /// <summary>
        /// Get paginated list of clients with optional search by name or email
        /// Admin only
        /// Caches results per page + search term
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("get-all-clients")]
        [EnableRateLimiting("admin_crud")]
        public async Task<IActionResult> GetClients(string? search = null, int page = 1, int pageSize = 10)
        {
            try
            {
                string cacheKey = GetClientCacheKey(search, page, pageSize);

                if (!_cache.TryGetValue(cacheKey, out List<object> clientList))
                {
                    var query = from c in _context.Clients
                                join u in _userManager.Users
                                on c.ClientID equals u.ClientID
                                select new
                                {
                                    c.ClientID,
                                    u.FirstName,
                                    u.LastName,
                                    u.Email
                                };

                    if (!string.IsNullOrEmpty(search))
                    {
                        query = query.Where(x =>
                            x.FirstName.Contains(search) ||
                            x.LastName.Contains(search) ||
                            x.Email.Contains(search));
                    }

                    var totalCount = await query.CountAsync();

                    var clientPage = await query
                        .OrderBy(x => x.FirstName)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                    clientList = clientPage.Cast<object>().ToList();
                    _cache.Set(cacheKey, clientList, cacheOptions);

                    return Ok(new
                    {
                        Data = clientList,
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = totalCount
                    });
                }

                return Ok(new
                {
                    Data = clientList,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = clientList.Count
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error fetching clients." });
            }
        }
        #endregion

        #region GET: api/clients/{id}
        /// <summary>
        /// Get single client by ID (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("get-client/{id}")]
        public async Task<IActionResult> GetClientById(int id)
        {
            try
            {
                var client = await _context.Clients.FindAsync(id);
                if (client == null)
                    return NotFound(new { Message = "Client not found." });

                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.ClientID == client.ClientID);

                return Ok(new
                {
                    client.ClientID,
                    user?.FirstName,
                    user?.LastName,
                    user?.Email
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error fetching client." });
            }
        }
        #endregion

        #region POST: api/clients
        /// <summary>
        /// Add new client (Admin only) + create linked ApplicationUser
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("add-client")]
        [EnableRateLimiting("admin_crud")]
        public async Task<IActionResult> AddClient([FromBody] CreateClientDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (await ClientExists(dto.Email))
                    return BadRequest(new { Message = "Client with the same email already exists." });

                var client = new Client
                {
                    Appointments = new List<Appointment>()
                };
                _context.Clients.Add(client);
                await _context.SaveChangesAsync();

                var user = new ApplicationUser
                {
                    UserName = dto.Email,
                    Email = dto.Email,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    ClientID = client.ClientID,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow,
                    IsActive = true,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, dto.Password);
                if (!result.Succeeded)
                {
                    _context.Clients.Remove(client);
                    await _context.SaveChangesAsync();
                    return BadRequest(new { Message = "Failed to create ApplicationUser." });
                }

                if (!await _roleManager.RoleExistsAsync(Constants.ClientRole))
                    await _roleManager.CreateAsync(new IdentityRole(Constants.ClientRole));

                await _userManager.AddToRoleAsync(user, Constants.ClientRole);

                RemoveClientCache();

                return Ok(new { Message = "Client added successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error adding client." });
            }
        }
        #endregion

        #region PUT: api/clients/{id}
        /// <summary>
        /// Update client by ID (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPatch("update-client/{id}")]
        public async Task<IActionResult> UpdateClient(int id, [FromBody] CreateClientDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var client = await _context.Clients.FindAsync(id);
                if (client == null)
                    return NotFound(new { Message = "Client not found." });

                if (await ClientExists(dto.Email, id))
                    return BadRequest(new { Message = "Another client with the same email exists." });

                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.ClientID == client.ClientID);
                if (user != null)
                {
                    user.FirstName = dto.FirstName;
                    user.LastName = dto.LastName;
                    user.Email = dto.Email;
                    user.UserName = dto.Email;
                    user.DateModified = DateTime.UtcNow;

                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                        return BadRequest(new { Message = "Failed to update ApplicationUser." });

                    if (!string.IsNullOrWhiteSpace(dto.Password))
                    {
                        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                        var passResult = await _userManager.ResetPasswordAsync(user, token, dto.Password);
                        if (!passResult.Succeeded)
                            return BadRequest(new { Message = "Failed to update password." });
                    }

                    var roles = await _userManager.GetRolesAsync(user);
                    if (!roles.Contains(Constants.ClientRole))
                    {
                        await _userManager.RemoveFromRolesAsync(user, roles);
                        await _userManager.AddToRoleAsync(user, Constants.ClientRole);
                    }
                }

                RemoveClientCache();

                return Ok(new { Message = "Client updated successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error updating client." });
            }
        }
        #endregion

        #region DELETE: api/clients/{id}
        /// <summary>
        /// Delete client by ID (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("delete-client/{id}")]
        public async Task<IActionResult> DeleteClient(int id)
        {
            try
            {
                var client = await _context.Clients.FindAsync(id);
                if (client == null)
                    return NotFound(new { Message = "Client not found." });

                _context.Clients.Remove(client);
                await _context.SaveChangesAsync();

                RemoveClientCache();

                return Ok(new { Message = "Client deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Error deleting client." });
            }
        }
        #endregion

        #region Private Methods
        private async Task<bool> ClientExists(string email, int? id = null)
        {
            email = email.ToLower().Trim();
            return await _userManager.Users
                .AnyAsync(u => u.Email.ToLower() == email &&
                               (!id.HasValue || u.ClientID != id.Value));
        }

        private string GetClientCacheKey(string? search, int page, int pageSize)
        {
            string key = $"client_{search}_{page}_{pageSize}";
            if (!_clientCacheKeys.Contains(key))
                _clientCacheKeys.Add(key);
            return key;
        }

        private void RemoveClientCache()
        {
            foreach (var key in _clientCacheKeys)
                _cache.Remove(key);
            _clientCacheKeys.Clear();
        }
        #endregion
    }
}
