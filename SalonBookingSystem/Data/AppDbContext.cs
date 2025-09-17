using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SalonBookingSystem.Models;

namespace SalonBookingSystem.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) 
        { 

        }

        public DbSet<Client> Clients { get; set; }
        public DbSet<Staff> Staff { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Appointment> Appointments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            //  ApplicationUser → Client (One-to-One, optional)
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Client)
                .WithOne()
                .HasForeignKey<ApplicationUser>(u => u.ClientID)
                .OnDelete(DeleteBehavior.Restrict);

            //  ApplicationUser → Staff (One-to-One, optional)
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Staff)
                .WithOne()
                .HasForeignKey<ApplicationUser>(u => u.StaffID)
                .OnDelete(DeleteBehavior.Restrict);

            //  Client → Appointments (One-to-Many)
            builder.Entity<Client>()
                .HasMany(c => c.Appointments)
                .WithOne(a => a.Client)
                .HasForeignKey(a => a.ClientID);

            //  Staff → Appointments (One-to-Many)
            builder.Entity<Staff>()
                .HasMany(s => s.Appointments)
                .WithOne(a => a.Staff)
                .HasForeignKey(a => a.StaffID);

            //  Service → Appointments (One-to-Many)
            builder.Entity<Service>()
                .HasMany(s => s.Appointments)
                .WithOne(a => a.Service)
                .HasForeignKey(a => a.ServiceID);
        }
    }
}
