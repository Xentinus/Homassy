extern alias NotificationsProject;
using Homassy.API.Entities.Family;
using Homassy.API.Entities.ShoppingList;
using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.Tests.Infrastructure;
using NotificationsProject::Homassy.Notifications.Services;

namespace Homassy.Tests.Unit;

/// <summary>
/// The number a chat push writes onto the installed app's icon.
/// </summary>
/// <remarks>
/// Driven against the same local PostgreSQL the integration tests use: what is under test is a
/// pair of queries, and the rule they encode ("unread means sent after your read marker, yours
/// excluded") has nothing to prove against a fake.
/// </remarks>
public class AppBadgeCountTests
{
    private sealed record Seeded(int FamilyId, int OwnerUserId, int OtherUserId);

    private static async Task<Seeded> SeedFamilyAsync(string prefix)
    {
        using var context = TestConfiguration.DbContextFactory.CreateDbContext();

        var family = new Family { Name = $"{prefix} Family" };
        context.Families.Add(family);
        await context.SaveChangesAsync();

        var owner = NewUser(prefix, family.Id);
        var other = NewUser($"{prefix}-other", family.Id);
        context.Users.AddRange(owner, other);
        await context.SaveChangesAsync();

        return new Seeded(family.Id, owner.Id, other.Id);
    }

    private static Homassy.API.Entities.User.User NewUser(string prefix, int familyId) => new()
    {
        Email = $"{prefix}-{Guid.NewGuid():N}@test.homassy.local",
        Name = $"{prefix} User",
        KratosIdentityId = Guid.NewGuid().ToString(),
        CreatedAt = DateTime.UtcNow,
        LastLoginAt = DateTime.UtcNow,
        Status = UserStatus.Active,
        FamilyId = familyId
    };

    private static async Task SendMessagesAsync(int familyId, int senderUserId, int count, DateTime sentAt)
    {
        using var context = TestConfiguration.DbContextFactory.CreateDbContext();

        for (var i = 0; i < count; i++)
        {
            context.Add(new FamilyChatMessage
            {
                FamilyId = familyId,
                SenderUserId = senderUserId,
                Kind = FamilyChatMessageKind.Text,
                Body = $"message {i}",
                SentAt = sentAt.AddSeconds(i)
            });
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ForUser_CountsUnreadChatMessages_ExcludingTheirOwn()
    {
        var seeded = await SeedFamilyAsync("badge-chat");

        await SendMessagesAsync(seeded.FamilyId, seeded.OtherUserId, 3, DateTime.UtcNow.AddMinutes(-5));
        // The reader's own messages are never unread to them, however many they send.
        await SendMessagesAsync(seeded.FamilyId, seeded.OwnerUserId, 2, DateTime.UtcNow.AddMinutes(-4));

        using var context = TestConfiguration.DbContextFactory.CreateForReading();
        var count = await AppBadgeCount.ForUserAsync(context, seeded.OwnerUserId, seeded.FamilyId, CancellationToken.None);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task ForUser_ReadMarker_StopsCountingWhatIsBehindIt()
    {
        var seeded = await SeedFamilyAsync("badge-read");

        await SendMessagesAsync(seeded.FamilyId, seeded.OtherUserId, 2, DateTime.UtcNow.AddMinutes(-10));

        using (var write = TestConfiguration.DbContextFactory.CreateDbContext())
        {
            write.Add(new FamilyChatReadState
            {
                FamilyId = seeded.FamilyId,
                UserId = seeded.OwnerUserId,
                LastReadAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await write.SaveChangesAsync();
        }

        // One message after the marker: the badge is about what is still waiting, not about
        // everything that was ever sent.
        await SendMessagesAsync(seeded.FamilyId, seeded.OtherUserId, 1, DateTime.UtcNow.AddMinutes(-1));

        using var context = TestConfiguration.DbContextFactory.CreateForReading();
        var count = await AppBadgeCount.ForUserAsync(context, seeded.OwnerUserId, seeded.FamilyId, CancellationToken.None);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ForUser_AddsDueShoppingListItems()
    {
        var seeded = await SeedFamilyAsync("badge-deadline");

        using (var write = TestConfiguration.DbContextFactory.CreateDbContext())
        {
            var list = new ShoppingList { Name = "Badge list", FamilyId = seeded.FamilyId };
            write.ShoppingLists.Add(list);
            await write.SaveChangesAsync();

            write.ShoppingListItems.AddRange(
                new ShoppingListItem { ShoppingListId = list.Id, CustomName = "overdue", Quantity = 1, Unit = Homassy.API.Enums.Unit.Piece, DeadlineAt = DateTime.UtcNow.AddDays(-2) },
                new ShoppingListItem { ShoppingListId = list.Id, CustomName = "due soon", Quantity = 1, Unit = Homassy.API.Enums.Unit.Piece, DueAt = DateTime.UtcNow.AddDays(3) },
                // Outside the 14-day window, and one already bought: neither is waiting on anyone.
                new ShoppingListItem { ShoppingListId = list.Id, CustomName = "far off", Quantity = 1, Unit = Homassy.API.Enums.Unit.Piece, DeadlineAt = DateTime.UtcNow.AddDays(60) },
                new ShoppingListItem { ShoppingListId = list.Id, CustomName = "bought", Quantity = 1, Unit = Homassy.API.Enums.Unit.Piece, DeadlineAt = DateTime.UtcNow.AddDays(1), PurchasedAt = DateTime.UtcNow });
            await write.SaveChangesAsync();
        }

        await SendMessagesAsync(seeded.FamilyId, seeded.OtherUserId, 1, DateTime.UtcNow.AddMinutes(-2));

        using var context = TestConfiguration.DbContextFactory.CreateForReading();
        var count = await AppBadgeCount.ForUserAsync(context, seeded.OwnerUserId, seeded.FamilyId, CancellationToken.None);

        // One unread message plus the two items that are due or overdue - the badge is a sum, and
        // this is the pair the app counts by default.
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task ForUser_WithoutFamily_CountsNoChat()
    {
        var seeded = await SeedFamilyAsync("badge-nofamily");

        await SendMessagesAsync(seeded.FamilyId, seeded.OtherUserId, 2, DateTime.UtcNow.AddMinutes(-2));

        using var context = TestConfiguration.DbContextFactory.CreateForReading();
        var count = await AppBadgeCount.ForUserAsync(context, seeded.OwnerUserId, null, CancellationToken.None);

        Assert.Equal(0, count);
    }
}
