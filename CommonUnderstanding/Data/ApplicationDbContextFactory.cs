using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommonUnderstanding.Data;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Used by `dotnet ef migrations add` and `dotnet ef database update`.
/// Supports both PostgreSQL and SQL Server via the DatabaseProvider config key.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Read connection string from appsettings.json and User Secrets
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(typeof(ApplicationDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var dbProvider = configuration.GetValue<string>("DatabaseProvider") ?? "PostgreSQL";

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. Set it in .NET user secrets or an environment variable.");

        var isPostgres = !dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);

        if (!isPostgres)
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
        else
        {
            optionsBuilder.UseNpgsql(connectionString);
        }

        return new ApplicationDbContext(optionsBuilder.Options, new DatabaseProviderInfo(isPostgres));
    }
}