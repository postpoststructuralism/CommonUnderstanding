namespace CommonUnderstanding.Admin.Options;

public sealed class AzureResourceOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string WebAppName { get; set; } = string.Empty;
    public string SqlServerName { get; set; } = string.Empty;
    public string SqlDatabaseName { get; set; } = string.Empty;
    public string FoundryAccountName { get; set; } = string.Empty;
    public string LogAnalyticsWorkspaceId { get; set; } = string.Empty;

    public string WebAppResourceId =>
        $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroup}/providers/Microsoft.Web/sites/{WebAppName}";

    public string SqlDatabaseResourceId =>
        $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroup}/providers/Microsoft.Sql/servers/{SqlServerName}/databases/{SqlDatabaseName}";

    public string FoundryAccountResourceId =>
        $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroup}/providers/Microsoft.CognitiveServices/accounts/{FoundryAccountName}";

    public string WebAppUrl => $"https://{WebAppName}.azurewebsites.net/";
}