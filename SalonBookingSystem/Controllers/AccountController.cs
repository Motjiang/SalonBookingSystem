using Mailjet.Client.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SalonBookingSystem.Data;
using SalonBookingSystem.DTOs.Account;
using SalonBookingSystem.Models;
using SalonBookingSystem.Services;
using System.Security.Claims;
using System.Text;

namespace SalonBookingSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly JWTService _jwtService;
        private readonly AppDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly EmailService _emailService;
        private readonly IConfiguration _config;

        public AccountController(JWTService jwtService,
            AppDbContext context,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            EmailService emailService,
            IConfiguration config)
        {
            _jwtService = jwtService;
            _context = context;
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _config = config;
        }

        /// <summary>
        /// Refresh JWT token for authenticated user
        /// </summary>
        [Authorize]
        [HttpGet("refresh-user-token")]
        public async Task<ActionResult<UserDto>> RefreshUserToken()
        {
            var user = await _userManager.FindByNameAsync(User.FindFirst(ClaimTypes.Email)?.Value);

            if (await _userManager.IsLockedOutAsync(user))
                return Unauthorized("You have been locked out");

            return await CreateApplicationUserDto(user);
        }

        /// <summary>
        /// Register a new client and create linked ApplicationUser
        /// </summary>
        [HttpPost("register")]
        [EnableRateLimiting("account_crud")] // prevent spam registrations
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if(CheckEmailExistsAsync(dto.Email).GetAwaiter().GetResult())
                return Conflict($"An account with email '{dto.Email}' already exists.");

            // Create client entity
            var client = new Client
            {
                Appointments = new List<Appointment>()
            };
            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            // Create linked ApplicationUser
            var user = new ApplicationUser
            {
                UserName = dto.Email.ToLower(),
                Email = dto.Email.ToLower(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                ClientID = client.ClientID,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                _context.Clients.Remove(client);
                await _context.SaveChangesAsync();
                return BadRequest(new { Message = "Failed to create user." });
            }

            // Assign default role
            if (!await _roleManager.RoleExistsAsync(Constants.ClientRole))
                await _roleManager.CreateAsync(new IdentityRole(Constants.ClientRole));

            await _userManager.AddToRoleAsync(user, Constants.ClientRole);

            return Ok(new { Message = "Account created successfully." });
        }

        /// <summary>
        /// Login user and return JWT token and basic info
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("account_crud")] // prevent brute-force attacks
        public async Task<ActionResult<UserDto>> Login(LoginDto model)
        {
            var user = await _userManager.FindByNameAsync(model.UserName);
            if (user == null)
                return Unauthorized("Invalid username or password");

            if (!user.EmailConfirmed)
                return Unauthorized("Please confirm your email.");

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);

            if (result.IsLockedOut)
                return Unauthorized($"Your account has been locked. Try again after {user.LockoutEnd} (UTC).");

            if (!result.Succeeded)
            {
                if (!user.UserName.Equals(Constants.AdminUserName))
                    await _userManager.AccessFailedAsync(user);

                if (user.AccessFailedCount >= Constants.MaximumLoginAttempts)
                {
                    await _userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow.AddDays(1));
                    return Unauthorized($"Your account has been locked. Try again after {user.LockoutEnd} (UTC).");
                }

                return Unauthorized("Invalid username or password");
            }

            await _userManager.ResetAccessFailedCountAsync(user);
            await _userManager.SetLockoutEndDateAsync(user, null);

            return await CreateApplicationUserDto(user);
        }

        /// <summary>
        /// Confirm user's email using token
        /// </summary>
        [HttpPut("confirm-email")]
        [EnableRateLimiting("account_crud")]
        public async Task<IActionResult> ConfirmEmail(ConfirmEmailDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return Unauthorized("This email address has not been registered yet");
            if (user.EmailConfirmed) return BadRequest("Email already confirmed.");

            try
            {
                var decodedTokenBytes = WebEncoders.Base64UrlDecode(model.Token);
                var decodedToken = Encoding.UTF8.GetString(decodedTokenBytes);
                var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

                if (result.Succeeded)
                    return Ok(new { title = "Email confirmed", message = "Your email address is confirmed. You can login now" });

                return BadRequest("Invalid token. Please try again");
            }
            catch
            {
                return BadRequest("Invalid token. Please try again");
            }
        }

        /// <summary>
        /// Resend email confirmation link
        /// </summary>
        [HttpPost("resend-email-confirmation-link/{email}")]
        [EnableRateLimiting("account_crud")]
        public async Task<IActionResult> ResendEMailConfirmationLink(string email)
        {
            if (string.IsNullOrEmpty(email)) return BadRequest("Invalid email");
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return Unauthorized("Email not registered");
            if (user.EmailConfirmed) return BadRequest("Email already confirmed");

            if (await SendConfirmEMailAsync(user))
                return Ok(new { title = "Confirmation link sent", message = "Please confirm your email address." });

            return BadRequest("Failed to send email. Please contact admin");
        }

        /// <summary>
        /// Send forgot password email
        /// </summary>
        [HttpPost("forgot-password/{email}")]
        [EnableRateLimiting("account_crud")]
        public async Task<IActionResult> ForgotUsernameOrPassword(string email)
        {
            if (string.IsNullOrEmpty(email)) return BadRequest("Invalid email");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return Unauthorized("Email not registered");
            if (!user.EmailConfirmed) return BadRequest("Please confirm your email first.");

            if (await SendForgotPasswordEmail(user))
                return Ok(new { title = "Forgot password email sent", message = "Please check your (Spam) emails." });

            return BadRequest("Failed to send email. Please contact admin.");
        }

        /// <summary>
        /// Reset password using token
        /// </summary>
        [HttpPut("reset-password")]
        [EnableRateLimiting("account_crud")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return Unauthorized("Email not registered");
            if (!user.EmailConfirmed) return BadRequest("Please confirm your email first");

            try
            {
                var decodedTokenBytes = WebEncoders.Base64UrlDecode(model.Token);
                var decodedToken = Encoding.UTF8.GetString(decodedTokenBytes);
                var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.NewPassword);

                if (result.Succeeded)
                    return Ok(new { title = "Password reset success", message = "Your password has been reset" });

                return BadRequest("Invalid token. Please try again");
            }
            catch
            {
                return BadRequest("Invalid token. Please try again");
            }
        }

        #region Private Helpers

        private async Task<UserDto> CreateApplicationUserDto(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return new UserDto
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                JWT = await _jwtService.CreateJWT(user),
                UserId = user.Id 
            };
        }

        private async Task<bool> CheckEmailExistsAsync(string email)
        {
            return await _userManager.Users.AnyAsync(x => x.Email == email.ToLower());
        }

        private async Task<bool> SendConfirmEMailAsync(ApplicationUser user)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var url = $"{_config["JWT:ClientUrl"]}/{_config["Email:ConfirmEmailPath"]}?token={token}&email={user.Email}";

            var body = $"<p>Hello: {user.FirstName} {user.LastName}</p>" +
                       "<p>Please confirm your email address by clicking the following link.</p>" +
                       $"<p><a href=\"{url}\">Click here</a></p>" +
                       "<p>Thank you,</p>" +
                       $"<br>{_config["Email:ApplicationName"]}";

            return await _emailService.SendEmailAsync(new SendEmailDto(user.Email, "Confirm your email", body));
        }

        private async Task<bool> SendForgotPasswordEmail(ApplicationUser user)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var url = $"{_config["JWT:ClientUrl"]}/{_config["Email:ResetPasswordPath"]}?token={token}&email={user.Email}";

            var body = $"<p>Hello: {user.FirstName} {user.LastName}</p>" +
                       $"<p>Username: {user.UserName}</p>" +
                       "<p>To reset your password, click the link below.</p>" +
                       $"<p><a href=\"{url}\">Click here</a></p>" +
                       "<p>Thank you,</p>" +
                       $"<br>{_config["Email:ApplicationName"]}";

            return await _emailService.SendEmailAsync(new SendEmailDto(user.Email, "Forgot username or password", body));
        }

        #endregion
    }
}
