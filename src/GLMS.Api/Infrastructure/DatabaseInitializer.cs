using GLMS.Core.Entities;
using GLMS.Core.Services;
using GLMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Api.Infrastructure;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies migrations and seeds the first admin user. Retries because in Docker Compose the SQL Server
    /// container can report healthy slightly before it accepts logins.
    /// </summary>
    public static async Task InitialiseAsync(WebApplication app)
    {
        var config = app.Configuration;
        var logger = app.Logger;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<GlmsDbContext>();

                if (config.GetValue("Database:MigrateOnStartup", true))
                {
                    // Migrations are generated for SQL Server; other providers (SQLite in the integration tests) get the schema directly.
                    if (db.Database.IsSqlServer())
                        await db.Database.MigrateAsync();
                    else
                        await db.Database.EnsureCreatedAsync();
                }

                var username = config["Auth:SeedAdmin:Username"];
                var password = config["Auth:SeedAdmin:Password"];
                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
                    await auth.EnsureUserAsync(username, password, AppRoles.Admin);
                }

                return;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}/10). Retrying in 5 seconds.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
