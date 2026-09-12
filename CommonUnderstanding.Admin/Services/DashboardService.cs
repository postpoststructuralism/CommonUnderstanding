using CommonUnderstanding.Admin.Models;

namespace CommonUnderstanding.Admin.Services;

public sealed class DashboardService(
    AdoptionMetricsService adoption,
    AzureMonitorService azureMonitor,
    EndpointTrafficService endpointTraffic,
    AvailabilityMonitorService availability)
{
    public async Task<DashboardSnapshot> GetAsync(int rangeHours, CancellationToken cancellationToken)
    {
        var adoptionTask = adoption.GetAsync(rangeHours, cancellationToken);
        var platformTask = azureMonitor.GetAsync(rangeHours, cancellationToken);
        var aiComputeTask = azureMonitor.GetAiComputeAsync(rangeHours, cancellationToken);
        var endpointTrafficTask = endpointTraffic.GetAsync(rangeHours, cancellationToken);
        var availabilityTask = availability.CheckAsync(cancellationToken);
        await Task.WhenAll(adoptionTask, platformTask, aiComputeTask, endpointTrafficTask, availabilityTask);

        var adoptionSnapshot = await adoptionTask;
        var platformSnapshot = await platformTask;
        var aiComputeSnapshot = await aiComputeTask;
        var endpointTrafficSnapshot = await endpointTrafficTask;
        var availabilitySnapshot = await availabilityTask;
        var warnings = new List<string>();
        if (!adoptionSnapshot.IsAvailable)
        {
            warnings.Add(adoptionSnapshot.Error ?? "Adoption metrics are unavailable.");
        }
        if (!platformSnapshot.IsAvailable)
        {
            warnings.Add(platformSnapshot.Error ?? "Azure platform metrics are unavailable.");
        }
        if (!aiComputeSnapshot.IsAvailable)
        {
            warnings.Add(aiComputeSnapshot.Error ?? "Foundry AI compute metrics are unavailable.");
        }
        if (!endpointTrafficSnapshot.IsAvailable)
        {
            warnings.Add(endpointTrafficSnapshot.Error ?? "Endpoint traffic details are unavailable.");
        }
        warnings.Add("Concurrent users are approximated by distinct non-service accounts with discovery or XP activity in the last 15 minutes.");
        warnings.Add("Availability history covers only checks made while this local dashboard is running; it does not wake or query the production database.");

        return new DashboardSnapshot(DateTimeOffset.UtcNow, rangeHours, adoptionSnapshot, aiComputeSnapshot,
            endpointTrafficSnapshot, platformSnapshot, availabilitySnapshot, warnings);
    }
}