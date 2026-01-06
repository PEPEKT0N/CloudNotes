using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Api.Extensions;

/// <summary>
/// Middleware для обработки исключений и логирования ошибок.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Необработанное исключение при обработке {Method} {Path}: {Message}\n{StackTrace}", 
                context.Request.Method,
                context.Request.Path,
                ex.Message,
                ex.StackTrace);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, error, message) = exception switch
        {
            ArgumentException or ArgumentNullException =>
                (HttpStatusCode.BadRequest, "Неверный запрос", exception.Message),
            UnauthorizedAccessException =>
                (HttpStatusCode.Unauthorized, "Доступ запрещён", exception.Message),
            KeyNotFoundException =>
                (HttpStatusCode.NotFound, "Ресурс не найден", exception.Message),
            DbUpdateException =>
                (HttpStatusCode.BadRequest, "Ошибка базы данных", "Произошла ошибка при работе с базой данных"),
            _ =>
                (HttpStatusCode.InternalServerError, "Внутренняя ошибка сервера",
                    _environment.IsDevelopment() ? exception.Message : null)
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error,
            message,
            stackTrace = _environment.IsDevelopment() ? exception.StackTrace : null
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

