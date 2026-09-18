using CommonUnderstanding.Services.Social;

namespace CommonUnderstanding.Tests.Services;

public sealed class CalibrationServiceTests
{
    [Fact]
    public void CalculateSummary_WithoutResolvedForecasts_ReturnsEmptySummary()
    {
        var summary = CalibrationService.CalculateSummary(Array.Empty<ResolvedForecast>());

        Assert.Equal(0, summary.ResolvedPredictionCount);
        Assert.Null(summary.BrierScore);
        Assert.Null(summary.CalibrationScore);
    }

    [Fact]
    public void CalculateSummary_ComputesMeanBrierAndInverseCalibrationScore()
    {
        var summary = CalibrationService.CalculateSummary(new[]
        {
            new ResolvedForecast(0.8, true),
            new ResolvedForecast(0.3, false)
        });

        Assert.Equal(2, summary.ResolvedPredictionCount);
        Assert.Equal(0.065, summary.BrierScore!.Value, 10);
        Assert.Equal(0.935, summary.CalibrationScore!.Value, 10);
    }

    [Fact]
    public void CalculateSummary_PerfectForecasts_ReturnPerfectCalibration()
    {
        var summary = CalibrationService.CalculateSummary(new[]
        {
            new ResolvedForecast(1, true),
            new ResolvedForecast(0, false)
        });

        Assert.Equal(0, summary.BrierScore);
        Assert.Equal(1, summary.CalibrationScore);
    }
}