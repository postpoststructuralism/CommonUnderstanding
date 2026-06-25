using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommonUnderstanding.Data;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Used by `dotnet ef migrations add` and `dotnet ef database update`.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Read connection string from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            // Fallback for local development
            connectionString = "Host=localhost;Database=commonunderstanding;Username=postgres;Password=postgres";
        }

        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}