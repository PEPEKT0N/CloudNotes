using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CloudNotes.Desktop.Data;

/// <summary>
/// Factory for creating AppDbContext instances at design time (e.g., for migrations).
/// EF Core tools will use this factory if found, bypassing other methods of creating the context.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/CloudNotes/notes.db");

        return new AppDbContext(optionsBuilder.Options);
    }
}
