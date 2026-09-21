using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Weft.Api.Data;

namespace Weft.Cli.Tests;

/// <summary>
/// Boots the real Weft API pipeline on in-memory SQLite so the CLI's command
/// implementations are exercised end to end over HTTP.
/// </summary>
public sealed class CliApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "sqlite");
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var stale = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<WeftDb>)
                         || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition().Name
                             == "IDbContextOptionsConfiguration`1"
                             && d.ServiceType.GenericTypeArguments.Contains(typeof(WeftDb))))
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
