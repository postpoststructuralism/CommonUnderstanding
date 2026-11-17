using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.FeatureManagement;

namespace CommonUnderstanding.Filters;

/// <summary>
/// MVC action filter that returns 404 when a feature flag is disabled.
/// This makes gated features invisible rather than returning an error page.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class FeatureGateAttribute : ActionFilterAttribute
{
    private readonly string _feature;

    public FeatureGateAttribute(string feature)
    {
        _feature = feature;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var featureManager = context.HttpContext.RequestServices.GetRequiredService<IFeatureManager>();

        if (await featureManager.IsEnabledAsync(_feature))
        {
            await next();
        }
        else
        {
            context.Result = new NotFoundResult();
        }
    }
}