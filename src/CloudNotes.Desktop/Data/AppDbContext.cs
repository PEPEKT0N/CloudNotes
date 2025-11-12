using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Desktop.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<CloudNotes.Desktop.Model.Note> Notes { get; set; } = null!;
}
