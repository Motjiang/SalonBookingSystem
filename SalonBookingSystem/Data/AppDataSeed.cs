using Mailjet.Client.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalonBookingSystem.Models;
using System.Security.Claims;

namespace SalonBookingSystem.Data
{
    public class AppDataSeed
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AppDataSeed(AppDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task InitializeContextAsync()
        {
            if (_context.Database.GetPendingMigrationsAsync().GetAwaiter().GetResult().Count() > 0)
            {
                // applies any pending migration into our database
                await _context.Database.MigrateAsync();
            }

            if (!_roleManager.Roles.Any())
            {
                await _roleManager.CreateAsync(new IdentityRole(Constants.AdminRole));
                await _roleManager.CreateAsync(new IdentityRole(Constants.StaffRole));
                await _roleManager.CreateAsync(new IdentityRole(Constants.ClientRole));
            }

            if (!_userManager.Users.AnyAsync().GetAwaiter().GetResult())
            {
                var adminUser = new ApplicationUser
                {
                    UserName = Constants.AdminUserName,
                    Email = Constants.AdminUserName,
                    FirstName = "System",
                    LastName = "Admin",
                    IsActive = true,
                    DateCreated = DateTime.Now,
                    DateModified = DateTime.Now,
                    EmailConfirmed = true
                };
                await _userManager.CreateAsync(adminUser, "Admin@123");
                await _userManager.AddToRoleAsync(adminUser, Constants.AdminRole);
                await _userManager.AddClaimsAsync(adminUser, new[]
                {
                    new Claim(ClaimTypes.Email, adminUser.Email),
                    new Claim(ClaimTypes.Surname, adminUser.LastName)
                });

                var staffUser = new ApplicationUser
                {
                    UserName = Constants.StaffUserName,
                    Email = Constants.StaffUserName,
                    FirstName = "System",
                    LastName = "Staff",
                    IsActive = true,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow,
                    EmailConfirmed = true
                };
                await _userManager.CreateAsync(staffUser, "Staff@123");
                await _userManager.AddToRoleAsync(staffUser, Constants.StaffRole);
                await _userManager.AddClaimsAsync(staffUser, new[]
                {
                    new Claim(ClaimTypes.Email, staffUser.Email),
                    new Claim(ClaimTypes.Surname, staffUser.LastName)
                });

                // Link application users to their respective staff profiles
                var staffProfle = new Staff
                {
                    Designation = "Barber",
                };

                _context.Staff.Add(staffProfle);
                await _context.SaveChangesAsync();

                staffUser.StaffID = staffProfle.StaffID;
                await _userManager.UpdateAsync(staffUser);
            }

            if (!_context.Services.Any())
            {
                var services = new List<Service>
                {
                    new Service
                    {
                        Name = "Haircut",
                        DurationMinutes = 45,
                        Price = 150.00m
                    },
                    new Service
                    {
                        Name = "Shave",
                        DurationMinutes = 25,
                        Price = 100.00m
                    },
                    new Service
                    {
                        Name = "Hair Colouring",
                        DurationMinutes = 60,
                        Price = 300.00m
                    }
                };

                _context.Services.AddRange(services);
                await _context.SaveChangesAsync();
            }
        }
    }
}
