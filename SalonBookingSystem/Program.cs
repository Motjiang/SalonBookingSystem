using Mailjet.Client.Resources;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SalonBookingSystem.Data;
using SalonBookingSystem.Models;
using SalonBookingSystem.Services;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// Add Cors
builder.Services.AddCors();
// Add DB Context
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add Services
builder.Services.AddScoped<JWTService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<AppDataSeed>();

// defining our IdentityCore Service
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    // password configuration
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;

    // for email confirmation
    options.SignIn.RequireConfirmedEmail = true;
})
    .AddRoles<IdentityRole>() // be able to add roles
    .AddRoleManager<RoleManager<IdentityRole>>() // be able to make use of RoleManager
    .AddEntityFrameworkStores<AppDbContext>() // providing our context
    .AddSignInManager<SignInManager<ApplicationUser>>() // make use of Signin manager
    .AddUserManager<UserManager<ApplicationUser>>() // make use of UserManager to create users
    .AddDefaultTokenProviders(); // be able to create tokens for email confirmation

// be able to authenticate users using JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // validate the token based on the key we have provided inside appsettings.development.json JWT:Key
            ValidateIssuerSigningKey = true,
            // the issuer singning key based on JWT:Key
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Key"])),
            // the issuer which in here is the api project url we are using
            ValidIssuer = builder.Configuration["JWT:Issuer"],
            // validate the issuer (who ever is issuing the JWT)
            ValidateIssuer = true,
            // don't validate audience (angular side)
            ValidateAudience = false
        };
    });

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Sliding window for public GET endpoints
    options.AddSlidingWindowLimiter(policyName:"public_get", options =>
    {
        options.Window = TimeSpan.FromSeconds(10);
        options.PermitLimit = 3;       // max 3 requests per window
        options.SegmentsPerWindow = 2; // divide window into 2 segments
        options.QueueLimit = 0;
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Fixed window for Admin endpoints
    options.AddFixedWindowLimiter(policyName:"admin_crud", options =>
    {
        options.Window = TimeSpan.FromSeconds(30);
        options.PermitLimit = 2;  // only 2 admin actions per 30 seconds
        options.QueueLimit = 0;
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Custom response for rejected requests
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429; // Too Many Requests
        context.HttpContext.Response.ContentType = "application/json";

        var response = new
        {
            Message = "Too many requests. Please try again later."
        };

        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: token);
    };
});

// Add Memory Cache
builder.Services.AddMemoryCache();


var app = builder.Build();

// Use Rate Limiting
app.UseRateLimiter();

// Enable CORS
app.UseCors(opt =>
{
    opt.AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithOrigins(builder.Configuration["JWT:ClientUrl"]);
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

#region AppDataSeed
using var scope = app.Services.CreateScope();
try
{
    var appDataSeedService = scope.ServiceProvider.GetService<AppDataSeed>();
    await appDataSeedService.InitializeContextAsync();
}
catch (Exception ex)
{
    var logger = scope.ServiceProvider.GetService<ILogger<Program>>();
    logger.LogError(ex.Message, "Failed to initialize and seed the database");
}
#endregion

app.Run();
