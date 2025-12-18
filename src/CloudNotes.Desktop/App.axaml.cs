using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CloudNotes.Desktop.Api;
using CloudNotes.Desktop.Services;
using CloudNotes.Desktop.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using System.IO;

namespace CloudNotes;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Настройка DI
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Получаем директорию, где находится исполняемый файл
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation) ?? Directory.GetCurrentDirectory();

        // Настройка конфигурации
        var configuration = new ConfigurationBuilder()
            .SetBasePath(assemblyDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Получение базового URL из конфигурации (с дефолтным значением)
        var baseUrl = configuration["Api:BaseUrl"] ?? "http://localhost:5000";

        // Auth (регистрируем первым, так как нужен для AuthHeaderHandler)
        services.AddSingleton<IAuthService, AuthService>();

        // Регистрация Refit клиента с автоматическим добавлением Authorization header
        services.AddRefitClient<ICloudNotesApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler(serviceProvider =>
            {
                var authService = serviceProvider.GetRequiredService<IAuthService>();
                return new AuthHeaderHandler(authService);
            });

        // Sync
        services.AddScoped<ISyncService, SyncService>();
    }
}
