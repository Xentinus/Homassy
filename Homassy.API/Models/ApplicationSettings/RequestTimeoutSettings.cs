using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ApplicationSettings;

public class RequestTimeoutSettings
{
    [Range(1, 3600)]
    public int DefaultTimeoutSeconds { get; set; } = 30;

    public List<EndpointTimeoutSettings> Endpoints { get; set; } = [];
}
