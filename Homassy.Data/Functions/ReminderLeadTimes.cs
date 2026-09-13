using System.Text.Json;

namespace Homassy.Data.Functions
{
    /// <summary>
    /// Reads the per-calendar reminder lead times out of the JSON column they are stored in.
    /// </summary>
    /// <remarks>
    /// Shared because both sides of the boundary read the same column: the API when it answers a
    /// calendar request, and <c>Homassy.Notifications</c> when the reminder worker decides how far
    /// ahead of an event to push (#91). A worker that parsed it differently would send reminders at
    /// times the owner never chose.
    /// </remarks>
    public static class ReminderLeadTimes
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// The lead times in minutes, or an empty list when the column is null, empty or
        /// unreadable. A malformed value is treated as "no reminders" rather than thrown: the
        /// alternative is a worker that stops sweeping because one family's row is bad.
        /// </summary>
        public static List<int> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];

            try
            {
                return JsonSerializer.Deserialize<List<int>>(json, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
