using Avalonia;
using System;
using Microsoft.EntityFrameworkCore;
using CloudNotes.Data;

namespace CloudNotes;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        //DbContextOptionsBuilder for configuring the connection
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/CloudNotes/notes.db");

        // context and call EnsureCreated
        using (var context = new AppDbContext(optionsBuilder.Options))
        {
            context.Database.EnsureCreated(); // Creates the database and tables if they don't exist
        }
        // Launch the Avalonia app
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
