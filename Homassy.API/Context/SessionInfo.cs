using Homassy.API.Functions;

namespace Homassy.API.Context
{
    public static class SessionInfo
    {
        private static readonly AsyncLocal<Guid?> _publicId = new();
        private static readonly AsyncLocal<int?> _userId = new();
        private static readonly AsyncLocal<int?> _familyId = new();

        public static Guid? GetPublicId() => _publicId.Value;
        public static int? GetUserId() => _userId.Value;
        public static int? GetFamilyId() => _familyId.Value;

        public static void SetUser(Guid? publicId, int? familyId = null)
        {
            _publicId.Value = publicId;
            var user = new UserFunctions().GetUserByPublicId(publicId);
            _userId.Value = user?.Id;
            _familyId.Value = familyId;
        }

        public static void Clear()
        {
            _publicId.Value = null;
            _userId.Value = null;
            _familyId.Value = null;
        }
    }
}
