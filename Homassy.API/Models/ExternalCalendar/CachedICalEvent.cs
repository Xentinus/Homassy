namespace Homassy.API.Models.ExternalCalendar
{
    public class CachedICalEvent
    {
        public string Uid { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public string? Description { get; set; }
        public bool IsAllDay { get; set; }

        /// <summary>
        /// Absolute start of the occurrence. <see cref="Start"/> is server-local and carries no offset,
        /// so it cannot be used to schedule anything; reminder trigger times are computed from this.
        /// Entries cached before this field existed deserialize to <c>default</c> and are skipped by the
        /// reminder worker until the next sync refills them.
        /// </summary>
        public DateTime StartUtc { get; set; }
    }
}
