using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Kratos;

namespace Homassy.API.Context
{
    public static class SessionInfo
    {
        private static readonly AsyncLocal<string?> _kratosIdentityId = new();
        private static readonly AsyncLocal<Guid?> _publicId = new();
        private static readonly AsyncLocal<int?> _userId = new();
        private static readonly AsyncLocal<int?> _familyId = new();
        private static readonly AsyncLocal<Language?> _language = new();
        private static readonly AsyncLocal<KratosSession?> _kratosSession = new();

        public static string? GetKratosIdentityId() => _kratosIdentityId.Value;
        public static Guid? GetPublicId() => _publicId.Value;
        public static int? GetUserId() => _userId.Value;
        public static int? GetFamilyId() => _familyId.Value;
        public static Language GetLanguage() => _language.Value ?? Language.English;
        public static KratosSession? GetKratosSession() => _kratosSession.Value;

        /// <summary>
        /// Sets the session info from a Kratos session.
        /// Looks up the local User by KratosIdentityId.
        /// </summary>
        public static void SetFromKratosSession(KratosSession session)
        {
            _kratosSession.Value = session;
            _kratosIdentityId.Value = session.Identity.Id;

            // Try to find local User by KratosIdentityId
            var user = new UserFunctions().GetUserByKratosIdentityId(session.Identity.Id);

            if (user != null)
            {
                _userId.Value = user.Id;
                _publicId.Value = user.PublicId;
                _familyId.Value = user.FamilyId ?? session.Identity.Traits.FamilyId;

                // Get language from Kratos traits or user profile
                var languageCode = session.Identity.Traits.DefaultLanguage;
                _language.Value = ParseLanguage(languageCode);
            }
            else
            {
                // User doesn't exist locally yet - use data from Kratos traits
                _userId.Value = null;
                _publicId.Value = null;
                _familyId.Value = session.Identity.Traits.FamilyId;
                _language.Value = ParseLanguage(session.Identity.Traits.DefaultLanguage);
            }
        }

        /// <summary>
        /// Legacy method for setting user from PublicId (kept for backwards compatibility).
        /// </summary>
        [Obsolete("Use SetFromKratosSession instead")]
        public static void SetUser(Guid? publicId, int? familyId = null)
        {
            var user = new UserFunctions().GetUserByPublicId(publicId);
            _userId.Value = user?.Id;
            _publicId.Value = publicId ?? user?.PublicId;
            _familyId.Value = familyId ?? user?.FamilyId;

            // Get language from UserProfile cache
            if (user?.Id != null)
            {
                var profile = new UserFunctions().GetUserProfileByUserId(user.Id);
                _language.Value = profile?.DefaultLanguage ?? Language.English;
            }
            else
            {
                _language.Value = Language.English;
            }
        }

        public static void Clear()
        {
            _kratosIdentityId.Value = null;
            _kratosSession.Value = null;
            _publicId.Value = null;
            _userId.Value = null;
            _familyId.Value = null;
            _language.Value = null;
        }

        /// <summary>
        /// Parses a language code (e.g., "hu", "en", "de") to Language enum.
        /// </summary>
        private static Language ParseLanguage(string? languageCode)
        {
            return languageCode?.ToLowerInvariant() switch
            {
                "hu" => Language.Hungarian,
                "de" => Language.German,
                "en" => Language.English,
                _ => Language.English
            };
        }
    }
}
