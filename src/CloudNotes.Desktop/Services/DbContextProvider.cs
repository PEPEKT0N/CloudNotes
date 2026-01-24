using System;
using CloudNotes.Desktop.Data;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Services;

public static class DbContextProvider
{
    private static readonly object _lock = new object();
    private static AppDbContext? _singletonContext;
    private static bool _migrationsApplied = false;

    /// <summary>
    /// Получить singleton контекст (для обратной совместимости).
    /// </summary>
    public static AppDbContext GetContext()
    {
        lock (_lock)
        {
            if (_singletonContext == null)
            {
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseSqlite($"Data Source={Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/CloudNotes/notes.db");
                _singletonContext = new AppDbContext(optionsBuilder.Options);
                
                if (!_migrationsApplied)
                {
                    _singletonContext.Database.Migrate(); // Applies migrations to update database schema
                    _migrationsApplied = true;
                }
            }

            return _singletonContext;
        }
    }

    /// <summary>
    /// Создать новый экземпляр контекста для thread-safe операций.
    /// </summary>
    public static AppDbContext CreateContext()
    {
        lock (_lock)
        {
            if (!_migrationsApplied)
            {
                // Применяем миграции один раз при первом создании контекста
                var tempOptions = new DbContextOptionsBuilder<AppDbContext>();
                tempOptions.UseSqlite($"Data Source={Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/CloudNotes/notes.db");
                using var tempContext = new AppDbContext(tempOptions.Options);
                tempContext.Database.Migrate();
                _migrationsApplied = true;
            }
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/CloudNotes/notes.db");
        return new AppDbContext(optionsBuilder.Options);
    }
}
