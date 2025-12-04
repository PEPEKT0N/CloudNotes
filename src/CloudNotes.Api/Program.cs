using CloudNotes.Api.Data;
using CloudNotes.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ===== Services =====

// Database
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
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
    .AddDefaultTokenProviders();

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
        options.RoutePrefix = string.Empty;
    });
}

// Routing
app.UseRouting();

// TODO: app.UseAuthentication();
// TODO: app.UseAuthorization();

app.MapControllers();

app.Run();
