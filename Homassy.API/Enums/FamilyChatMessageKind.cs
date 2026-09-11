namespace Homassy.API.Enums
{
    /// <summary>
    /// What a <see cref="Entities.Family.FamilyChatMessage"/> carries (#144).
    /// </summary>
    /// <remarks>
    /// <b>Numbering is permanent</b>, the same rule <see cref="NotificationType"/> states: the
    /// value is what the <c>Kind</c> column persists, so renumbering a member reinterprets every
    /// existing row.
    /// </remarks>
    public enum FamilyChatMessageKind
    {
        /// <summary>A text message. <c>Body</c> carries it and is never null.</summary>
        Text = 0,

        /// <summary>
        /// A picture (#147). The bytes live in <c>FamilyChatImages</c> and are served from the
        /// chat image endpoint; <c>Body</c> is the optional caption.
        /// </summary>
        Image = 1
    }
}
