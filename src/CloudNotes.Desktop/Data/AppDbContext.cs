using Microsoft.EntityFrameworkCore;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Note> Notes { get; set; } = null!;
}
