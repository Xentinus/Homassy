using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Homassy.API.Models.FamilyChat;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

/// <summary>
/// The family chat's HTTP surface (#144): who may read and post, how a page boundary behaves, and
/// who may delete.
/// </summary>
/// <remarks>
/// Every test here drives the real endpoints against the real database. The two that matter most
/// are the isolation pair - a member of another family must see nothing and post nothing - because
/// the access rule is what the rest of R9 is built on top of.
/// </remarks>
public class FamilyChatControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public FamilyChatControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Unauthorized

    [Fact]
    public async Task GetMessages_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/familychat/messages");

        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/familychat/messages",
            new SendFamilyChatMessageRequest { Body = "hello" });

        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Family scope

    [Fact]
    public async Task GetMessages_UserWithoutFamily_ReturnsForbidden()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-nofamily");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync("/api/v1.0/familychat/messages");

            _output.WriteLine($"Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetMessages_MemberOfAnotherFamily_DoesNotSeeTheirMessages()
    {
        string? ownerEmail = null;
        string? outsiderEmail = null;
        try
        {
            // A family with something said in it.
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-owner");
            ownerEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Chat Owner Family");
            await SendAsync("a secret only this family sees");

            // A second family, entirely unrelated.
            _authHelper.ClearAuthToken();
            var (otherEmail, otherAuth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-outsider");
            outsiderEmail = otherEmail;
            _authHelper.SetAuthToken(otherAuth.AccessToken);
            await CreateFamilyAsync("Outsider Family");

            var page = await GetPageAsync();

            _output.WriteLine($"Outsider saw {page.Items.Count} message(s)");
            Assert.DoesNotContain(page.Items, m => m.Body == "a secret only this family sees");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (ownerEmail != null) await _authHelper.CleanupUserAsync(ownerEmail);
            if (outsiderEmail != null) await _authHelper.CleanupUserAsync(outsiderEmail);
        }
    }

    #endregion

    #region Paging

    [Fact]
    public async Task GetMessages_CursorPaging_ReturnsEachMessageExactlyOnce()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-paging");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Paging Family");

            const int total = 7;
            for (var i = 0; i < total; i++)
            {
                await SendAsync($"message {i}");
            }

            // Two pages of three plus a remainder, so at least one boundary falls inside the set.
            var seen = new List<Guid>();
            string? cursor = null;
            var pages = 0;

            do
            {
                var page = await GetPageAsync(cursor, 3);
                seen.AddRange(page.Items.Select(m => m.PublicId));
                cursor = page.NextCursor;
                pages++;
                Assert.True(pages < 10, "paging did not terminate");
            }
            while (cursor != null);

            _output.WriteLine($"Walked {pages} page(s), {seen.Count} message(s)");

            Assert.Equal(total, seen.Count);
            Assert.Equal(total, seen.Distinct().Count());
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetMessages_MalformedCursor_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-cursor");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Cursor Family");

            var response = await _client.GetAsync("/api/v1.0/familychat/messages?before=not-a-cursor");

            _output.WriteLine($"Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    #endregion

    #region Deleting

    [Fact]
    public async Task DeleteMessage_OwnMessage_RemovesItFromHistory()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-delete-own");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Delete Family");

            var message = await SendAsync("this one goes away");

            var deleteResponse = await _client.DeleteAsync($"/api/v1.0/familychat/messages/{message.PublicId}");
            _output.WriteLine($"Delete status: {deleteResponse.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            var page = await GetPageAsync();
            Assert.DoesNotContain(page.Items, m => m.PublicId == message.PublicId);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task DeleteMessage_AnotherMembersMessage_ReturnsNotFound()
    {
        string? ownerEmail = null;
        string? memberEmail = null;
        try
        {
            // One family with two members in it, so "somebody else's message" is a message the
            // caller can genuinely see - the case the ownership check exists for.
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-owner2");
            ownerEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var shareCode = await CreateFamilyAsync("Two Member Family");

            _authHelper.ClearAuthToken();
            var (secondEmail, secondAuth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-member");
            memberEmail = secondEmail;
            _authHelper.SetAuthToken(secondAuth.AccessToken);

            var joinResponse = await _client.PostAsJsonAsync(
                "/api/v1.0/family/join-requests",
                new JoinFamilyRequest { ShareCode = shareCode });
            Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);

            // The owner approves, then the new member says something.
            _authHelper.ClearAuthToken();
            _authHelper.SetAuthToken(auth.AccessToken);
            var pending = await _client.GetFromJsonAsync<ApiResponse<List<FamilyJoinRequestResponse>>>(
                "/api/v1.0/family/join-requests");
            var request = Assert.Single(pending!.Data!);

            var approveResponse = await _client.PostAsync(
                $"/api/v1.0/family/join-requests/{request.PublicId}/approve", null);
            Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

            _authHelper.ClearAuthToken();
            _authHelper.SetAuthToken(secondAuth.AccessToken);
            var theirMessage = await SendAsync("mine, not yours");

            // Back to the owner, who can read it but must not be able to delete it.
            _authHelper.ClearAuthToken();
            _authHelper.SetAuthToken(auth.AccessToken);

            var visible = await GetPageAsync();
            Assert.Contains(visible.Items, m => m.PublicId == theirMessage.PublicId);

            var deleteResponse = await _client.DeleteAsync($"/api/v1.0/familychat/messages/{theirMessage.PublicId}");
            _output.WriteLine($"Delete status: {deleteResponse.StatusCode}");
            Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

            // And it is still there afterwards.
            var after = await GetPageAsync();
            Assert.Contains(after.Items, m => m.PublicId == theirMessage.PublicId);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (ownerEmail != null) await _authHelper.CleanupUserAsync(ownerEmail);
            if (memberEmail != null) await _authHelper.CleanupUserAsync(memberEmail);
        }
    }

    #endregion

    #region Sending

    [Fact]
    public async Task SendMessage_EmptyBody_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Empty Body Family");

            var response = await _client.PostAsJsonAsync(
                "/api/v1.0/familychat/messages",
                new SendFamilyChatMessageRequest { Body = "   " });

            _output.WriteLine($"Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task SendMessage_KeepsAngleBracketsAndCodeIntact()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-text");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Plain Text Family");

            // The chat body deliberately skips [SanitizedString], which rejects '<' and '>'.
            // "5 < 10" is an ordinary thing to send your family, and a chat that refuses it is
            // broken; the safety comes from rendering text nodes instead. This pins that choice.
            const string body = "5 < 10 -> buy more";
            var message = await SendAsync(body);

            Assert.Equal(body, message.Body);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    #endregion

    #region Helpers

    /// <summary>Creates a family for the currently authenticated caller and returns its share code.</summary>
    private async Task<string> CreateFamilyAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/family/create",
            new CreateFamilyRequest { Name = name });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<FamilyInfo>>();
        Assert.NotNull(body?.Data?.ShareCode);
        return body!.Data!.ShareCode;
    }

    private async Task<FamilyChatMessageInfo> SendAsync(string body)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/familychat/messages",
            new SendFamilyChatMessageRequest { Body = body });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var parsed = await response.Content.ReadFromJsonAsync<ApiResponse<FamilyChatMessageInfo>>();
        Assert.NotNull(parsed?.Data);
        return parsed!.Data!;
    }

    private async Task<FamilyChatPage> GetPageAsync(string? before = null, int? limit = null)
    {
        var query = new List<string>();
        if (before != null) query.Add($"before={Uri.EscapeDataString(before)}");
        if (limit != null) query.Add($"limit={limit}");

        var url = "/api/v1.0/familychat/messages" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var parsed = await response.Content.ReadFromJsonAsync<ApiResponse<FamilyChatPage>>();
        Assert.NotNull(parsed?.Data);
        return parsed!.Data!;
    }

    #endregion
}
