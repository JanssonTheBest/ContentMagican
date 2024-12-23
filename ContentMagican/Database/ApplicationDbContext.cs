using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ContentMagican.Database
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            this.Database.SetCommandTimeout(60*2);
        }

        //public DbSet<Plan> Plans { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<_Task> Task { get; set; }
        public DbSet<VideoAutomation> VideoAutomation { get; set; }
        public DbSet<OrderLog> Orders { get; set; }
        public DbSet<SocialMediaAccessSession> SocialMediaAccessSessions { get; set; }
    }

}
