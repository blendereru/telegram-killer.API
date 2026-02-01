using Microsoft.EntityFrameworkCore;
using telegram_killer.API.Models;

namespace telegram_killer.API.Data;

public class ApplicationContext : DbContext
{
    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options)
    {
        
    }
    
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshSession> RefreshSessions { get; set; }
    public DbSet<EmailConfirmationCode> EmailConfirmationCodes { get; set; }
}