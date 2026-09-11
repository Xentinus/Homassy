extern alias NotificationsProject;
using Homassy.API.Context;
using Homassy.API.Entities.Family;
using Homassy.API.Entities.ShoppingList;
using Homassy.API.Enums;
using Homassy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NotificationsProject::Homassy.Notifications.Services;

namespace Homassy.Tests.Integration;

/// <summary>
/// The number a chat push writes onto the installed app's icon.
/// </summary>
/// <remarks>
/// Here rather than under <c>Unit/</c> because it needs the real PostgreSQL: what is under test is
/// a pair of queries, and the rules they encode ("unread means sent after your read marker, yours
/// excluded"; "due means within the same window the deadline endpoint uses") have nothing to prove
/// against a fake. Everything it seeds is removed in <see cref="DisposeAsync"/> - these rows are
/// not reachable by <c>TestAuthHelper.CleanupUserAsync</c>, which is email-keyed over users it
/// created itself.
/// </remarks>
public class AppBadgeCountTests : IAsyncLifetime
{
    private readonly List<int> _familyIds = [];
    private readonly List<int> _userIds = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var context = TestConfiguration.DbContextFactory.CreateDbContext();

        // Messages, read states and pictures cascade from the family; the users do not, and the
        // lists are keyed by family id rather than owned by it.
        await context.ShoppingListItems
            .Where(i => context.ShoppingLists
                .Where(sl => sl.FamilyId.HasValue && _familyIds.Contains(sl.FamilyId.Value) || sl.UserId.HasValue && _userIds.Contains(sl.UserId.Value))
                .Select(sl => sl.Id)
                .Contains(i.ShoppingListId))
            .ExecuteDeleteAsync();

        await context.ShoppingLists
            .Where(sl => sl.FamilyId.HasValue && _familyIds.Contains(sl.FamilyId.Value)
                || sl.UserId.HasValue && _userIds.Contains(sl.UserId.Value))
            .ExecuteDeleteAsync();

        await context.Set<FamilyChatMessage>().Where(m => _familyIds.Contains(m.FamilyId)).ExecuteDeleteAsync();
        await context.Set<FamilyChatReadState>().Where(r => _familyIds.Contains(r.FamilyId)).ExecuteDeleteAsync();
        await context.Users.Where(u => _userIds.Contains(u.Id)).ExecuteDeleteAsync();
        await context.Families.Where(f => _familyIds.Contains(f.Id)).ExecuteDeleteAsync();
    }

    private sealed record Seeded(int FamilyId, int OwnerUserId, int OtherUserId);

    private async Task<Seeded> SeedFamilyAsync(string prefix)
    {
        using var context = TestConfiguration.DbContextFactory.CreateDbContext();

        var family = new Family { Name = $"{prefix} Family" };
        context.Families.Add(family);
        await context.SaveChangesAsync();

        var owner = NewUser(prefix, family.Id);
        var other = NewUser($"{prefix}-other", family.Id);
        context.Users.AddRange(owner, other);
        await context.SaveChangesAsync();

        _familyIds.Add(family.Id);
        _userIds.Add(owner.Id);
        _userIds.Add(other.Id);

        return new Seeded(family.Id, owner.Id, other.Id);
    }

    private static Homassy.API.Entities.User.User NewUser(string prefix, int? familyId) => new()
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

    private static async Task<int> CountAsync(int userId, int? familyId)
    {
        using var context = TestConfiguration.DbContextFactory.CreateForReading();
        return await AppBadgeCount.ForUserAsync(context, userId, familyId, CancellationToken.None);
    }

    [Fact]
    public async Task ForUser_CountsUnreadChatMessages_ExcludingTheirOwn()
    {
        var seeded = await SeedFamilyAsync("badge-chat");

        await SendMessagesAsync(seeded.FamilyId, seeded.OtherUserId, 3, DateTime.UtcNow.AddMinutes(-5));
        // The reader's own messages are never unread to them, however many they send.
        await SendMessagesAsync(seeded.FamilyId, seeded.OwnerUserId, 2, DateTime.UtcNow.AddMinutes(-4));

        Assert.Equal(3, await CountAsync(seeded.OwnerUserId, seeded.FamilyId));
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

        Assert.Equal(1, await CountAsync(seeded.OwnerUserId, seeded.FamilyId));
    }

    [Fact]
    public async Task ForUser_AddsDueShoppingListItems()
    {
        var seeded = await SeedFamilyAsync("badge-deadline");

        await SeedListAsync(seeded.FamilyId, null,
            ("overdue", DateTime.UtcNow.AddDays(-2), null, null),
            ("due soon", null, DateTime.UtcNow.AddDays(3), null),
            // Outside the 14-day window, and one already bought: neither is waiting on anyone.
            ("far off", DateTime.UtcNow.AddDays(60), null, null),
            ("bought", DateTime.UtcNow.AddDays(1), null, DateTime.UtcNow));

        // Asserted before the message is sent as well, so the two halves of the sum are
        // distinguishable - a single `3` cannot say which one moved.
        Assert.Equal(2, await CountAsync(seeded.OwnerUserId, seeded.FamilyId));

        await SendMessagesAsync(seeded.FamilyId, seeded.OtherUserId, 1, DateTime.UtcNow.AddMinutes(-2));

        Assert.Equal(3, await CountAsync(seeded.OwnerUserId, seeded.FamilyId));
    }

    [Fact]
    public async Task ForUser_DeadlineWindow_IncludesDay14_ExcludesDay15()
    {
        var seeded = await SeedFamilyAsync("badge-window");

        await SeedListAsync(seeded.FamilyId, null,
            ("day 14", DateTime.UtcNow.AddDays(14), null, null),
            ("day 15", DateTime.UtcNow.AddDays(15), null, null));

        // The window is a fortnight, and the boundary is where an off-by-one or a timezone shift
        // in the date comparison would show up - nowhere else.
        Assert.Equal(1, await CountAsync(seeded.OwnerUserId, seeded.FamilyId));
    }

    [Fact]
    public async Task ForUser_ItemPointingAtADeletedProduct_DoesNotCount()
    {
        var seeded = await SeedFamilyAsync("badge-deleted-product");

        using (var write = TestConfiguration.DbContextFactory.CreateDbContext())
        {
            var product = new Homassy.API.Entities.Product.Product
            {
                Name = $"Gone {Guid.NewGuid():N}",
                Brand = "Test",
                Unit = Homassy.API.Enums.Unit.Piece
            };
            write.Products.Add(product);
            await write.SaveChangesAsync();

            var list = new ShoppingList { Name = "Badge list", FamilyId = seeded.FamilyId };
            write.ShoppingLists.Add(list);
            await write.SaveChangesAsync();

            write.ShoppingListItems.Add(new ShoppingListItem
            {
                ShoppingListId = list.Id,
                ProductId = product.Id,
                Quantity = 1,
                Unit = Homassy.API.Enums.Unit.Piece,
                DeadlineAt = DateTime.UtcNow.AddDays(1)
            });
            await write.SaveChangesAsync();

            product.DeleteRecord();
            await write.SaveChangesAsync();
        }

        // Matches GET /shoppinglist/item/deadline-count, which skips these too: the icon and the
        // in-app badge disagreeing about the same list is the failure this class exists to avoid.
        Assert.Equal(0, await CountAsync(seeded.OwnerUserId, seeded.FamilyId));
    }

    [Fact]
    public async Task ForUser_WithoutFamily_StillCountsTheirOwnListDeadlines()
    {
        var user = await SeedSoloUserAsync("badge-solo");

        await SeedListAsync(null, user, ("own deadline", DateTime.UtcNow.AddDays(1), null, null));

        // A single-user install has no family, and the deadlines on their own list are still the
        // thing waiting for them.
        Assert.Equal(1, await CountAsync(user, null));
    }

    [Fact]
    public async Task ForUser_WithoutFamily_CountsNoChat()
    {
        var seeded = await SeedFamilyAsync("badge-nofamily");

        await SendMessagesAsync(seeded.FamilyId, seeded.OtherUserId, 2, DateTime.UtcNow.AddMinutes(-2));

        // The positive control first: the same seed answers 2 when the family is named, so the 0
        // below comes from dropping the family rather than from an empty database.
        Assert.Equal(2, await CountAsync(seeded.OwnerUserId, seeded.FamilyId));
        Assert.Equal(0, await CountAsync(seeded.OwnerUserId, null));
    }

    private async Task<int> SeedSoloUserAsync(string prefix)
    {
        using var context = TestConfiguration.DbContextFactory.CreateDbContext();

        var user = NewUser(prefix, null);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        _userIds.Add(user.Id);
        return user.Id;
    }

    /// <summary>One list plus its items, as (name, deadline, due, purchased) tuples.</summary>
    private static async Task SeedListAsync(
        int? familyId,
        int? userId,
        params (string Name, DateTime? DeadlineAt, DateTime? DueAt, DateTime? PurchasedAt)[] items)
    {
        using var context = TestConfiguration.DbContextFactory.CreateDbContext();

        var list = new ShoppingList { Name = "Badge list", FamilyId = familyId, UserId = userId };
        context.ShoppingLists.Add(list);
        await context.SaveChangesAsync();

        context.ShoppingListItems.AddRange(items.Select(item => new ShoppingListItem
        {
            ShoppingListId = list.Id,
            CustomName = item.Name,
            Quantity = 1,
            Unit = Homassy.API.Enums.Unit.Piece,
            DeadlineAt = item.DeadlineAt,
            DueAt = item.DueAt,
            PurchasedAt = item.PurchasedAt
        }));

        await context.SaveChangesAsync();
    }
}
