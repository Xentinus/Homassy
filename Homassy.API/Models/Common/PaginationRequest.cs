using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Common;

public class PaginationRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1")]
    public int PageNumber { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
    public int PageSize { get; init; } = 20;
    public bool ReturnAll { get; init; } = false;
    public int Skip => (PageNumber - 1) * PageSize;
    public string? SearchText { get; init; }
}
