namespace Homassy.API.Models.Product
{
    public class ExpirationCountResponse
    {
        /// <summary>Inventory items already expired or expiring inside the warning window.</summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// How many of those are already past their date.
        /// </summary>
        /// <remarks>
        /// Split out so the client can colour its badge with the same expiration ramp the cards
        /// use: red once something has actually expired, amber while everything is only close. A
        /// single total could only ever be one colour, and it was always the alarming one.
        /// </remarks>
        public int ExpiredCount { get; set; }
    }
}
