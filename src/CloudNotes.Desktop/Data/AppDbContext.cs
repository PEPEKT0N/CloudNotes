using Microsoft.EntityFrameworkCore;
using CloudNotes.Models;
namespace CloudNotes.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Note> Notes { get; set; } = null!;
}
