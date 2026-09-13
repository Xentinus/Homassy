using Homassy.Data.Entities.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.Data.Entities.User
{
    /// <summary>
    /// A user's avatar bytes. One row per user at most; see <see cref="StoredImageEntity"/> for
    /// why the bytes do not live on <see cref="UserProfile"/>.
    /// </summary>
    public class UserProfilePicture : StoredImageEntity
    {
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        public User User { get; set; } = null!;
    }
}
