namespace CommonUnderstanding.Data;

/// <summary>
/// Singleton service that indicates whether the database provider is PostgreSQL.
/// Used by ApplicationDbContext to conditionally apply PostgreSQL-specific configurations.
/// </summary>
public class DatabaseProviderInfo
{
    public bool IsPostgres { get; }

    public DatabaseProviderInfo(bool isPostgres)
    {
        IsPostgres = isPostgres;
    }
}