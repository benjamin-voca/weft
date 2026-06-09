using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Weft.Api.Data;

namespace Weft.Api.Tests;

/// <summary>
/// Boots the real Program pipeline against an in-memory SQLite database
/// (connection kept open for the factory lifetime) — the same Minimal API
/// endpoints, seeding and rules the production app uses.
/// </summary>
public sealed class WeftApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "sqlite");
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // EF Core 9+ splits the registration into DbContextOptions and
            // IDbContextOptionsConfiguration — both must go, otherwise the
            // original Npgsql configuration still applies.
            var stale = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<WeftDb>)
                         || d.ServiceType.FullName == "Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration`1[[Weft.Api.Data.WeftDb, Weft.Api]]")
                .ToList();
            foreach (var d in stale) services.Remove(d);

            _connection.Open();
            services.AddSingleton(_connection);
            services.AddDbContext<WeftDb>((sp, options) =>
                options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _connection.Dispose();
        base.Dispose(disposing);
    }
}
