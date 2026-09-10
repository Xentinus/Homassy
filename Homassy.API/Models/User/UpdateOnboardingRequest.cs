namespace Homassy.API.Models.User
{
    /// <summary>
    /// Sets or clears the caller's first-run tour flag (#98).
    /// </summary>
    /// <remarks>
    /// One endpoint with a flag rather than a "complete" and a "reset" endpoint: finishing the
    /// tour and replaying it are the same field written two ways, and a pair of verbs would be two
    /// routes to keep in step for no gain.
    /// </remarks>
    public class UpdateOnboardingRequest
    {
        /// <summary>
        /// <c>true</c> when the tour was finished or skipped; <c>false</c> to arm it again, which
        /// is what the "Replay the tour" row in the profile settings sends.
        /// </summary>
        public bool Completed { get; init; }
    }
}
