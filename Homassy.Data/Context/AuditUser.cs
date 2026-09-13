namespace Homassy.Data.Context
{
    /// <summary>
    /// Who the ambient operation is acting as, for the audit columns
    /// <see cref="HomassyDbContext"/> stamps onto every <c>RecordChangeEntity</c> it saves.
    /// </summary>
    /// <remarks>
    /// This is the one thing the data layer needs to know about the caller, and it is why it does
    /// not need to know anything else: <c>Homassy.API</c>'s <c>SessionInfo</c> holds the rest of
    /// the request identity (the Kratos session, the public id, the family, the language) and
    /// writes the user id through here, so the context can stamp an audit row without the library
    /// referencing the web layer that produced it (#91).
    /// <para>
    /// Null in a host that has no request to speak of - the notification workers, the migrator -
    /// which is the correct answer there: a row a background worker writes was not changed by a
    /// person, and stamping some service account onto it would be a lie.
    /// </para>
    /// <para>
    /// <see cref="AsyncLocal{T}"/> rather than a scoped service because the context reads it from
    /// inside <c>SaveChanges</c>, where there is nothing to inject into.
    /// </para>
    /// </remarks>
    public static class AuditUser
    {
        private static readonly AsyncLocal<int?> _userId = new();

        /// <summary>The acting user's local id, or null outside a request.</summary>
        public static int? UserId
        {
            get => _userId.Value;
            set => _userId.Value = value;
        }
    }
}
