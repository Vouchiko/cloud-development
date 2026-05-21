using Ocelot.Errors;
using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Responses;
using Ocelot.Values;

namespace WarehouseItem.Gateway.LoadBalancer;

/// <summary>
/// Query-based балансировщик нагрузки: выбирает downstream по query-параметру <c>id</c>.
/// Если параметр отсутствует/невалиден - выбирает случайный downstream.
/// </summary>
public sealed class QueryBasedLoadBalancer(
    ILogger<QueryBasedLoadBalancer> logger,
    Func<Task<List<Service>>> servicesProvider) : ILoadBalancer
{
    private const string IdParam = "id";

    public string Type => nameof(QueryBasedLoadBalancer);

    public void Release(ServiceHostAndPort hostAndPort) { }

    public async Task<Response<ServiceHostAndPort>> LeaseAsync(HttpContext httpContext)
    {
        var services = await servicesProvider();

        if (services.Count == 0)
        {
            return new ErrorResponse<ServiceHostAndPort>(new NoServicesError());
        }

        var selectedIndex = ChooseIndex(httpContext, services.Count, out var parsedId);

        if (parsedId is null)
        {
            logger.LogWarning("Query param {param} missing/invalid; index={index} selected by random.", IdParam,
                selectedIndex);
        }
        else
        {
            logger.LogInformation("Query-based selection: id={id} -> index={index}.", parsedId, selectedIndex);
        }

        return new OkResponse<ServiceHostAndPort>(services[selectedIndex].HostAndPort);
    }

    private static int ChooseIndex(HttpContext httpContext, int servicesCount, out int? id)
    {
        id = null;

        if (TryParseId(httpContext, out var parsed))
        {
            id = parsed;
            return parsed % servicesCount;
        }

        return Random.Shared.Next(servicesCount);
    }

    private static bool TryParseId(HttpContext httpContext, out int id)
    {
        id = 0;

        if (!httpContext.Request.Query.TryGetValue(IdParam, out var values) || values.Count == 0)
        {
            return false;
        }

        var raw = values[0];
        return !string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out id) && id >= 0;
    }
}

internal sealed class NoServicesError() : Error("No services available", OcelotErrorCode.UnableToFindDownstreamRouteError, 503);
