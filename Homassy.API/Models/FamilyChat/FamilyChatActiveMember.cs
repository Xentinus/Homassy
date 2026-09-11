namespace Homassy.API.Models.FamilyChat
{
    /// <summary>One family member currently watching the conversation.</summary>
    /// <remarks>
    /// Same shape as <see cref="FamilyChatTypingMember"/> and deliberately a separate type: the two
    /// answer different questions ("who is writing" versus "who is here"), they are broadcast on
    /// different events, and collapsing them into one DTO would mean a change made for one of them
    /// silently reshaping the other.
    /// <para>
    /// A public id and a name, and nothing else. The bubble shows a count and the panel a short
    /// list; neither draws an avatar, so an avatar URL here would be a field nobody renders on an
    /// event that fires whenever anyone opens or closes the chat.
    /// </para>
    /// </remarks>
    public record FamilyChatActiveMember
    {
        public Guid PublicId { get; init; }

        public string DisplayName { get; init; } = string.Empty;
    }
}
