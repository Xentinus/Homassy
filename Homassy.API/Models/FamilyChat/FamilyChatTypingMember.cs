namespace Homassy.API.Models.FamilyChat
{
    /// <summary>One family member currently typing (#148).</summary>
    /// <remarks>
    /// A public id and a name, and nothing else. The indicator says "Anna is typing…" - it does not
    /// draw an avatar, so an avatar URL here would be a field nobody renders on an event that fires
    /// every couple of seconds.
    /// </remarks>
    public record FamilyChatTypingMember
    {
        public Guid PublicId { get; init; }

        public string DisplayName { get; init; } = string.Empty;
    }
}
