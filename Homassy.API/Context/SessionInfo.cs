namespace Homassy.API.Context
{
    public static class SessionInfo
    {
        private static readonly AsyncLocal<int?> _userId = new();
        private static readonly AsyncLocal<string?> _email = new();
        private static readonly AsyncLocal<int?> _familyId = new();

        public static int? GetUserId() => _userId.Value;
        public static string? GetEmail() => _email.Value;
        public static int? GetFamilyId() => _familyId.Value;

        public static void SetUser(int? userId, string? email = null, int? familyId = null)
        {
            _userId.Value = userId;
            _email.Value = email;
            _familyId.Value = familyId;
        }

        public static void Clear()
        {
            _userId.Value = null;
            _email.Value = null;
            _familyId.Value = null;
        }
    }
}
