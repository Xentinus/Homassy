namespace Homassy.API.Models.Internal
{
    /// <summary>
    /// Asks the API which of these users are currently watching their family chat (#149).
    /// </summary>
    /// <remarks>
    /// The "actively watching" flags live in the API's memory, because that is where the SignalR
    /// hub is - and the worker that decides whether to notify runs in <c>Homassy.Notifications</c>.
    /// So the question crosses the process boundary the same way the inventory broadcast does: an
    /// internal, api-key-authenticated endpoint.
    /// <para>
    /// Candidates are sent explicitly rather than the API being asked for "everyone active",
    /// because the worker already knows exactly whose notification it is deciding, and a list of
    /// every watcher in the installation is both larger and none of its business.
    /// </para>
    /// </remarks>
    public class FamilyChatActiveUsersRequest
    {
        /// <summary>The users to ask about. Internal ids - this endpoint is service-to-service.</summary>
        public List<int> UserIds { get; set; } = [];
    }

    /// <summary>Which of the requested users has at least one connection actively watching the chat.</summary>
    public class FamilyChatActiveUsersResponse
    {
        public List<int> ActiveUserIds { get; set; } = [];
    }
}
