using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Notification;
using Homassy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

/// <summary>
/// Integration tests for the notification centre endpoints (#116).
/// </summary>
/// <remarks>
/// The tests this file exists for are the two about isolation
/// (<see cref="GetNotifications_AnotherUsersNotification_IsNotVisible"/>,
/// <see cref="MarkRead_AnotherUsersNotification_ReturnsNotFound"/>) and the one about the cursor
/// (<see cref="GetNotifications_RowsSharingATimestamp_PageWithoutDuplicatesOrSkips"/>). Read state
/// is personal, and a batch of notifications written by one worker iteration genuinely does share
/// a timestamp - a paging boundary that falls inside such a batch is the failure mode that would
/// show up as an inbox that loses or repeats rows.
/// </remarks>
public class NotificationControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public NotificationControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Helpers
    /// <summary>
    /// Inserts notifications directly. The rows are normally written by
    /// <c>Homassy.Notifications</c>, which does not run in the test host, so this is the whole of
    /// "the workers delivered something".
    /// </summary>
    /// <param name="createdAt">
    /// Passed explicitly, and shared across a call, so a test can reproduce the tied-timestamp
    /// batch the cursor has to survive.
    /// </param>
    private async Task<List<Guid>> AddNotificationsAsync(
        int userId,
        int count,
        DateTime? createdAt = null,
        NotificationType type = NotificationType.InventoryItemsCreated)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        var rows = new List<UserNotification>();
        for (var i = 0; i < count; i++)
        {
            rows.Add(new UserNotification
            {
                UserId = userId,
                Type = type,
                ParametersJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["count"] = (i + 1).ToString() }),
                TargetUrl = "/products",
                CreatedAt = createdAt ?? DateTime.UtcNow.AddMinutes(-i)
            });
        }

        context.Set<UserNotification>().AddRange(rows);
        await context.SaveChangesAsync();

        return rows.Select(row => row.PublicId).ToList();
    }

    private async Task<NotificationPage> GetPageAsync(string? cursor = null, int pageSize = 25)
    {
        var url = cursor == null
            ? $"/api/v1.0/notification?pageSize={pageSize}"
            : $"/api/v1.0/notification?pageSize={pageSize}&cursor={Uri.EscapeDataString(cursor)}";

        var response = await _client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"GET {url} -> {response.StatusCode}");
        _output.WriteLine(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationPage>>();
        Assert.NotNull(content?.Data);
        return content.Data;
    }

    private async Task<int> GetUnreadCountAsync()
    {
        var response = await _client.GetAsync("/api/v1.0/notification/unread-count");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<UnreadCountResponse>>();
        Assert.NotNull(content?.Data);
        return content.Data.UnreadCount;
    }
    #endregion

    [Fact]
    public async Task GetNotifications_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/notification");
        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetNotifications_NoNotifications_ReturnsAnEmptyPage()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var page = await GetPageAsync();

            Assert.Empty(page.Items);
            Assert.Null(page.NextCursor);
            Assert.Equal(0, page.UnreadCount);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The row carries its type as a *name* and its parameters as data - never rendered prose,
    /// which is what lets the client word it in whatever language the reader is using now.
    /// </summary>
    [Fact]
    public async Task GetNotifications_ReturnsTheTypeNameAndParameters()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-shape");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);
            await AddNotificationsAsync(userId.Value, 1, type: NotificationType.ShoppingListCreated);

            var page = await GetPageAsync();

            var item = Assert.Single(page.Items);
            Assert.Equal(nameof(NotificationType.ShoppingListCreated), item.Type);
            Assert.Equal("/products", item.TargetUrl);
            Assert.False(item.IsRead);
            Assert.Equal("1", item.Parameters["count"]);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetNotifications_ReturnsNewestFirst()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-order");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);
            // AddNotificationsAsync steps back a minute per row, so row 0 is the newest.
            var ids = await AddNotificationsAsync(userId.Value, 3);

            var page = await GetPageAsync();

            Assert.Equal(3, page.Items.Count);
            Assert.Equal(ids[0], page.Items[0].PublicId);
            Assert.Equal(ids[2], page.Items[2].PublicId);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The reason the cursor encodes <c>(CreatedAt, PublicId)</c> and not the timestamp alone: a
    /// worker iteration writes a whole batch in one <c>DateTime.UtcNow</c>, and SQL makes no
    /// ordering promise among rows an <c>ORDER BY</c> treats as equal. Paging one row at a time
    /// through five tied rows must yield five distinct rows.
    /// </summary>
    [Fact]
    public async Task GetNotifications_RowsSharingATimestamp_PageWithoutDuplicatesOrSkips()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-cursor");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);
            var tick = DateTime.UtcNow;
            var ids = await AddNotificationsAsync(userId.Value, 5, createdAt: tick);

            var seen = new List<Guid>();
            string? cursor = null;

            do
            {
                var page = await GetPageAsync(cursor, pageSize: 1);
                seen.AddRange(page.Items.Select(item => item.PublicId));
                cursor = page.NextCursor;
            }
            while (cursor != null && seen.Count <= ids.Count);

            Assert.Equal(ids.Count, seen.Count);
            Assert.Equal(ids.Count, seen.Distinct().Count());
            Assert.Equal(ids.OrderBy(id => id), seen.OrderBy(id => id));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetNotifications_MalformedCursor_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-badcursor");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync("/api/v1.0/notification?cursor=not-a-cursor");
            _output.WriteLine($"Status: {response.StatusCode}");

            // A client mistake, not a server fault - and never a 500 out of the middleware.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetNotifications_PageSizeAboveTheCap_IsClamped()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-clamp");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);
            await AddNotificationsAsync(userId.Value, NotificationFunctions.MaxPageSize + 5);

            // An unbounded page size is an unbounded query over a table that grows with every
            // worker iteration for every family member.
            var page = await GetPageAsync(pageSize: 10_000);

            Assert.Equal(NotificationFunctions.MaxPageSize, page.Items.Count);
            Assert.NotNull(page.NextCursor);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task MarkRead_UnreadNotification_DecrementsTheUnreadCount()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-read");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);
            var ids = await AddNotificationsAsync(userId.Value, 3);

            Assert.Equal(3, await GetUnreadCountAsync());

            var response = await _client.PostAsync($"/api/v1.0/notification/{ids[0]}/read", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<UnreadCountResponse>>();

            // The endpoint answers with the new number, so the client never has to guess it.
            Assert.Equal(2, content?.Data?.UnreadCount);
            Assert.Equal(2, await GetUnreadCountAsync());
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task MarkRead_CalledTwice_IsIdempotent()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-read2x");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);
            var ids = await AddNotificationsAsync(userId.Value, 2);

            await _client.PostAsync($"/api/v1.0/notification/{ids[0]}/read", null);
            var second = await _client.PostAsync($"/api/v1.0/notification/{ids[0]}/read", null);

            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            // Not 0: re-reading a read row must not keep decrementing the badge.
            Assert.Equal(1, await GetUnreadCountAsync());
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task MarkAllRead_ClearsTheUnreadCount()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-readall");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);
            await AddNotificationsAsync(userId.Value, 4);

            var response = await _client.PostAsync("/api/v1.0/notification/read-all", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.Equal(0, await GetUnreadCountAsync());

            var page = await GetPageAsync();
            Assert.All(page.Items, item => Assert.True(item.IsRead));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// Dismissing takes the row out of the list and out of the count. Both: a badge that keeps
    /// counting a row nobody can open is a badge that cannot be cleared.
    /// </summary>
    [Fact]
    public async Task Dismiss_RemovesTheRowAndStopsCountingIt()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-dismiss");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);
            var ids = await AddNotificationsAsync(userId.Value, 2);

            var response = await _client.DeleteAsync($"/api/v1.0/notification/{ids[0]}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await GetPageAsync();
            Assert.Single(page.Items);
            Assert.Equal(ids[1], page.Items[0].PublicId);
            Assert.Equal(1, await GetUnreadCountAsync());
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task Dismiss_AlreadyDismissed_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-dismiss2x");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);
            var ids = await AddNotificationsAsync(userId.Value, 1);

            await _client.DeleteAsync($"/api/v1.0/notification/{ids[0]}");
            var second = await _client.DeleteAsync($"/api/v1.0/notification/{ids[0]}");

            Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The isolation test. Two members of the same household are still two inboxes: read state is
    /// personal, and one member must not see another's notifications even inside a family.
    /// </summary>
    [Fact]
    public async Task GetNotifications_AnotherUsersNotification_IsNotVisible()
    {
        string? ownerEmail = null;
        string? readerEmail = null;
        try
        {
            var (owner, _) = await _authHelper.CreateAndAuthenticateUserAsync("notif-owner");
            ownerEmail = owner;
            var ownerId = _factory.GetUserIdByEmail(owner);
            Assert.NotNull(ownerId);
            await AddNotificationsAsync(ownerId.Value, 3);
            _authHelper.ClearAuthToken();

            var (reader, readerAuth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-reader");
            readerEmail = reader;
            _authHelper.SetAuthToken(readerAuth.AccessToken);

            var page = await GetPageAsync();

            Assert.Empty(page.Items);
            Assert.Equal(0, page.UnreadCount);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (readerEmail != null) await _authHelper.CleanupUserAsync(readerEmail);
            if (ownerEmail != null) await _authHelper.CleanupUserAsync(ownerEmail);
        }
    }

    /// <summary>
    /// The other half of isolation: not merely invisible, but unactionable - and answered as 404
    /// rather than 403, so the endpoint cannot be used to probe whether a given notification id
    /// exists for somebody else.
    /// </summary>
    [Fact]
    public async Task MarkRead_AnotherUsersNotification_ReturnsNotFound()
    {
        string? ownerEmail = null;
        string? readerEmail = null;
        try
        {
            var (owner, _) = await _authHelper.CreateAndAuthenticateUserAsync("notif-owner2");
            ownerEmail = owner;
            var ownerId = _factory.GetUserIdByEmail(owner);
            Assert.NotNull(ownerId);
            var ids = await AddNotificationsAsync(ownerId.Value, 1);
            _authHelper.ClearAuthToken();

            var (reader, readerAuth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-reader2");
            readerEmail = reader;
            _authHelper.SetAuthToken(readerAuth.AccessToken);

            var read = await _client.PostAsync($"/api/v1.0/notification/{ids[0]}/read", null);
            var dismiss = await _client.DeleteAsync($"/api/v1.0/notification/{ids[0]}");

            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, dismiss.StatusCode);

            // And the owner's row is untouched.
            var (scope, context) = _factory.CreateScopedDbContext();
            await using var _ = scope as IAsyncDisposable;
            var row = await context.Set<UserNotification>().FirstAsync(n => n.PublicId == ids[0]);
            Assert.Null(row.ReadAt);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (readerEmail != null) await _authHelper.CleanupUserAsync(readerEmail);
            if (ownerEmail != null) await _authHelper.CleanupUserAsync(ownerEmail);
        }
    }

    /// <summary>
    /// The retention sweep drops what is past the window and keeps what is not. A hard delete, so
    /// this checks the row is actually gone rather than merely filtered.
    /// </summary>
    [Fact]
    public async Task PruneAsync_RemovesOnlyRowsPastTheRetentionWindow()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("notif-prune");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var userId = _factory.GetUserIdByEmail(email);
            Assert.NotNull(userId);
            var now = DateTime.UtcNow;
            var fresh = await AddNotificationsAsync(userId.Value, 1, createdAt: now.AddDays(-1));
            var stale = await AddNotificationsAsync(userId.Value, 1, createdAt: now.AddDays(-(NotificationFunctions.RetentionDays + 1)));

            var (scope, context) = _factory.CreateScopedDbContext();
            await using var _ = scope as IAsyncDisposable;

            var removed = await NotificationFunctions.PruneAsync(context, now);

            Assert.True(removed >= 1);
            Assert.False(await context.Set<UserNotification>().IgnoreQueryFilters().AnyAsync(n => n.PublicId == stale[0]));
            Assert.True(await context.Set<UserNotification>().AnyAsync(n => n.PublicId == fresh[0]));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }
}
