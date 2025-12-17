using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ApplicationSettings;

public class HttpsSettings
{
    public bool Enabled { get; set; } = true;

    [Range(1, 65535)]
    public int? HttpsPort { get; set; }

    public HstsSettings Hsts { get; set; } = new();
}
