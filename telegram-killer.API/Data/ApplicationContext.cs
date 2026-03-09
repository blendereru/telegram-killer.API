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
    public DbSet<Chat> Chats { get; set; }
    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Chat>()
            .HasOne(c => c.ParticipantA)
            .WithMany(u => u.Chats);
        
        modelBuilder.Entity<Chat>()
            .HasOne(c => c.ParticipantB)
            .WithMany(u => u.Chats);
        
        modelBuilder.Entity<Chat>()
            .HasOne(c => c.LastMessage)
            .WithOne(m => m.Chat);
    }
}