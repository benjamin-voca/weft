using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Weft.Api.Data;
using Weft.Api.Features;

var builder = WebApplication.CreateBuilder(args);

var provider = builder.Configuration["Database:Provider"] ?? "postgres";
var connectionString = builder.Configuration["Database:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=6970;Database=weft;Username=weft;Password=weft";

builder.Services.AddDbContext<WeftDb>(options => _ = provider switch
{
    "sqlite" => options.UseSqlite(connectionString),
    _ => options.UseNpgsql(connectionString)
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));
builder.Services.AddOpenApi();

// Cookie sessions for the SPA — API callers get 401s, never redirects.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "weft_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/outputs", () => Results.Ok(
        OutputCatalog.Groups.Select(g => new { g.Group, Outputs = g.Outputs }).ToList()))
    .RequireAuthorization();
app.MapAuth();
app.MapProjects();
app.MapAssets();
app.MapRenders();
app.MapStats();

// Serve the SPA (wwwroot) with history-API fallback in production.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WeftDb>();
    if (provider == "sqlite")
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.Run();

public partial class Program;
