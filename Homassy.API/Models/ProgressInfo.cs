namespace Homassy.API.Models;

public class ProgressInfo
{
    public Guid JobId { get; set; }
    public int Percentage { get; set; }
    public ProgressStage Stage { get; set; }
    public ProgressStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum ProgressStage
{
    Validating = 0,
    Compressing = 1,
    Uploading = 2,
    Processing = 3,
    Saving = 4,
    Completed = 5
}

public enum ProgressStatus
{
    InProgress = 0,
    Completed = 1,
    Failed = 2,
    Cancelled = 3
}

public class ProgressResponse
{
    public Guid JobId { get; set; }
    public int Percentage { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class UploadJobResponse
{
    public Guid JobId { get; set; }
}
