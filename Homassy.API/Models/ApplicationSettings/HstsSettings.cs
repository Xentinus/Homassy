using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ApplicationSettings;

public class HstsSettings
{
    public bool Enabled { get; set; } = true;

    [Range(0, int.MaxValue)]
    public int MaxAgeDays { get; set; } = 365;

    public bool IncludeSubDomains { get; set; } = true;

    public bool Preload { get; set; }
}
