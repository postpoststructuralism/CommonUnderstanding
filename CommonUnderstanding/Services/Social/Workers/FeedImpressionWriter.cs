using System.Threading.Channels;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;

namespace CommonUnderstanding.Services.Social.Workers;

public sealed class FeedImpressionWriter : BackgroundService
{
    private readonly Channel<FeedImpressionEvent> _channel;
    private readonly SingletonDbContextFactory _dbFactory;
    private readonly ILogger<FeedImpressionWriter> _logger;
    private readonly int _batchSize;

    public FeedImpressionWriter(
        SingletonDbContextFactory dbFactory,
        IConfiguration configuration,
        ILogger<FeedImpressionWriter> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _batchSize = configuration.GetValue("Recommendation:ImpressionBatchSize", 100);
        _channel = Channel.CreateBounded<FeedImpressionEvent>(new BoundedChannelOptions(
            configuration.GetValue("Recommendation:ImpressionQueueCapacity", 5000))
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public bool TryEnqueue(IEnumerable<FeedImpressionEvent> events)
    {
        var accepted = true;
        foreach (var impression in events)
            accepted &= _channel.Writer.TryWrite(impression);
        if (!accepted)
            _logger.LogWarning("Recommendation impression queue is full; telemetry was dropped.");
        return accepted;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<FeedImpressionEvent>(_batchSize);
        await foreach (var impression in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            batch.Add(impression);
            while (batch.Count < _batchSize && _channel.Reader.TryRead(out var next))
                batch.Add(next);
            await WriteBatchAsync(batch, stoppingToken);
            batch.Clear();
        }
    }

    private async Task WriteBatchAsync(List<FeedImpressionEvent> batch, CancellationToken ct)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.FeedImpressionEvents.AddRange(batch);
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist {Count} feed impressions.", batch.Count);
        }
    }
}