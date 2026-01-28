using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CloudNotes.Desktop.Api;
using CloudNotes.Desktop.Services;
using CloudNotes.Desktop.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;

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

            // Синхронизация при запуске и запуск периодической синхронизации (если онлайн и авторизован)
            _ = Task.Run(async () =>
            {
                if (ServiceProvider != null)
                {
                    try
                    {
                        var syncService = ServiceProvider.GetRequiredService<ISyncService>();
                        var synced = await syncService.SyncOnStartupAsync();
                        if (synced)
                        {
                            syncService.StartPeriodicSync();
                        }
                    }
                    catch (Refit.ApiException ex)
                    {
                        // Логируем ошибки API, но не прерываем запуск приложения
                        System.Diagnostics.Debug.WriteLine($"SyncOnStartup API error: {ex.StatusCode} - {ex.Message}");
                        if (ex.Content != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Response content: {ex.Content}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Логируем другие ошибки, но не прерываем запуск
                        System.Diagnostics.Debug.WriteLine($"SyncOnStartup error: {ex.Message}");
                    }
                }
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Получаем директорию, где находится исполняемый файл
        // Используем AppContext.BaseDirectory, т.к. Assembly.Location пуст в single-file app
        var assemblyDirectory = AppContext.BaseDirectory;

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
        // Используем Func<IAuthService> для ленивого разрешения и избежания циклической зависимости
        services.AddRefitClient<ICloudNotesApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler(serviceProvider =>
            {
                return new AuthHeaderHandler(() => serviceProvider.GetRequiredService<IAuthService>());
            });

        // Note Service Factory (переключение между гостевым и авторизованным режимами)
        services.AddSingleton<INoteServiceFactory>(sp =>
        {
            var context = CloudNotes.Services.DbContextProvider.GetContext();
            var authService = sp.GetRequiredService<IAuthService>();
            return new NoteServiceFactory(context, authService);
        });

        // Note Service (для обратной совместимости и SyncService - всегда авторизованный сервис)
        services.AddSingleton<INoteService>(sp =>
        {
            var factory = sp.GetRequiredService<INoteServiceFactory>();
            return factory.AuthenticatedNoteService;
        });

        // Conflict Service
        services.AddSingleton<IConflictService, ConflictService>();

        // Sync (Singleton для периодической синхронизации)
        services.AddSingleton<ISyncService, SyncService>();
    }
}
