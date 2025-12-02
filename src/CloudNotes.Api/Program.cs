using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ===== Services =====

// Controllers
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CloudNotes API",
        Version = "v1",
        Description = "API для синхронизации заметок CloudNotes"
    });
});

var app = builder.Build();

// ===== Middleware Pipeline =====

// Swagger (в Development)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CloudNotes API v1");
        options.RoutePrefix = string.Empty; // Swagger на корне
    });
}

// Routing
app.UseRouting();

// TODO: app.UseAuthentication();
// TODO: app.UseAuthorization();

app.MapControllers();

app.Run();
