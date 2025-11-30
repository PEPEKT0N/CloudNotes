using System;
using System.Threading;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CloudNotes.Desktop.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Note> Notes { get; set; } = null!;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Автоматически устанавливаем/обновляем UpdatedAt для всех заметок
        var entries = ChangeTracker.Entries<Note>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.Now;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
