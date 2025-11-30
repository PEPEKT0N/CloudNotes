using Avalonia;
using System;
using Microsoft.EntityFrameworkCore;
using CloudNotes.Desktop.Data;
using System.IO;

namespace CloudNotes.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Create DbContextOptionsBuilder for configuring the connection
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        // optionsBuilder.UseSqlite($"Data Source={Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/CloudNotes/notes.db");
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CloudNotes"
        );
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, "notes.db");
        var optionsBuidler = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        // Create context and apply migrations
        using (var context = new AppDbContext(optionsBuilder.Options))
        {
            context.Database.Migrate();
        }

        // Launch the Avalonia application
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
