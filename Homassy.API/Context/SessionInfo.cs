using Homassy.API.Enums;
using Homassy.API.Functions;

namespace Homassy.API.Context
{
    public static class SessionInfo
    {
        private static readonly AsyncLocal<Guid?> _publicId = new();
        private static readonly AsyncLocal<int?> _userId = new();
        private static readonly AsyncLocal<int?> _familyId = new();
        private static readonly AsyncLocal<Language?> _language = new();

        public static Guid? GetPublicId() => _publicId.Value;
        public static int? GetUserId() => _userId.Value;
        public static int? GetFamilyId() => _familyId.Value;
        public static Language GetLanguage() => _language.Value ?? Language.English;

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
            _publicId.Value = null;
            _userId.Value = null;
            _familyId.Value = null;
            _language.Value = null;
        }
    }
}
