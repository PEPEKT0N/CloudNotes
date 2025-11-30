using System;
using CloudNotes.Desktop.Data;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Services;

public static class DbContextProvider
{
    private static AppDbContext? _context;

    public static AppDbContext GetContext()
    {
        if (_context == null)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite($"Data Source={Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/CloudNotes/notes.db");
            _context = new AppDbContext(optionsBuilder.Options);
            _context.Database.Migrate(); // Applies migrations to update database schema
        }

        return _context;
    }
}
