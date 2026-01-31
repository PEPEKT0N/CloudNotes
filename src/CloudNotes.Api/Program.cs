using System.Text;
using CloudNotes.Api.Data;
using CloudNotes.Api.Extensions;
using CloudNotes.Api.Models;
using CloudNotes.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// ===== Serilog Configuration =====
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/cloudnotes-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("Запуск CloudNotes API");

    var builder = WebApplication.CreateBuilder(args);

    // Используем Serilog вместо стандартного логирования
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/cloudnotes-.log", rollingInterval: RollingInterval.Day));

    // ===== Services =====

    // Database
    builder.Services.AddDbContext<ApiDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Identity с кастомным UserManager (UserName НЕ уникальный, только Email)
    builder.Services.AddIdentity<User, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;

            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApiDbContext>()
        .AddDefaultTokenProviders()
        .AddUserManager<CustomUserManager>();

    // Удаляем стандартные UserValidator, т.к. CustomUserManager имеет свою валидацию
    var userValidators = builder.Services
        .Where(d => d.ServiceType == typeof(IUserValidator<User>))
        .ToList();
    foreach (var validator in userValidators)
    {
        builder.Services.Remove(validator);
    }

    // JWT Authentication
    var jwtSecret = builder.Configuration["Jwt:Secret"];
    if (string.IsNullOrWhiteSpace(jwtSecret))
    {
        throw new InvalidOperationException(
            "JWT Secret не настроен в конфигурации. " +
            "Установите переменную окружения Jwt__Secret или добавьте Jwt:Secret в appsettings.json");
    }
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CloudNotes.Api";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CloudNotes.Client";

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero
            };
        });

    // Application Services
    builder.Services.AddScoped<ITokenService, TokenService>();

    // Controllers
    builder.Services.AddControllers();

    // HTTP Logging
    builder.Services.AddHttpLogging(options =>
    {
        options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
        options.RequestBodyLogLimit = 4096;
        options.ResponseBodyLogLimit = 4096;
    });

    // Health Checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection") ?? "",
            name: "database",
            timeout: TimeSpan.FromSeconds(3));

    // Swagger/OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "CloudNotes API",
            Version = "v1",
            Description = @"API для синхронизации заметок CloudNotes.

## Аутентификация
Используйте JWT Bearer токен для доступа к защищённым эндпоинтам.
1. Получите токен через `POST /api/auth/login` или `POST /api/auth/register`
2. Нажмите кнопку 'Authorize' и введите: `Bearer <ваш_токен>`

## Конфликт-резолвер (Last Write Wins)
При обновлении заметки (`PUT /api/notes/{id}`) можно передать `clientUpdatedAt`.
- Если `clientUpdatedAt >= server.updatedAt` → изменения принимаются
- Если `clientUpdatedAt < server.updatedAt` → возвращается `409 Conflict` с актуальной версией заметки"
        });

        // JWT Authorization в Swagger UI
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Введите JWT токен. Пример: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // XML комментарии
        var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    var app = builder.Build();

    // ===== Database Migrations =====
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services
                .GetRequiredService<ApiDbContext>();
            Log.Information("Применение миграций базы данных...");

            // Проверяем, может ли приложение подключиться к базе
            if (!context.Database.CanConnect())
            {
                Log.Warning("Не удается подключиться к базе данных. Создание базы данных...");
                context.Database.EnsureCreated();
            }

            // Применяем миграции
            context.Database.Migrate();
            Log.Information("Миграции базы данных успешно применены");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при применении миграций базы данных: {Message}", ex.Message);
            // Не прерываем запуск приложения, если это ошибка миграций
            // (например, таблица миграций еще не создана)
            if (ex.Message.Contains("__EFMigrationsHistory") || ex.Message.Contains("does not exist"))
            {
                Log.Warning(
                    "Это может быть нормальная ситуация при первом запуске. Продолжаем запуск...");
            }
            else
            {
                throw;
            }
        }
    }

    // ===== Middleware Pipeline =====

    // Exception Handling (должен быть первым)
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // HTTP Logging
    app.UseHttpLogging();

    // Request Logging через Serilog
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.GetLevel = (httpContext, elapsed, ex) => ex != null
            ? Serilog.Events.LogEventLevel.Error
            : elapsed > 1000
                ? Serilog.Events.LogEventLevel.Warning
                : Serilog.Events.LogEventLevel.Information;
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress?.ToString());
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        };
    });

    // Swagger (в Development)
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "CloudNotes API v1");
            options.RoutePrefix = string.Empty;
        });
    }

    // Routing
    app.UseRouting();

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Health Check endpoints
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение завершилось с ошибкой");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
