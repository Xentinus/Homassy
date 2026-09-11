using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Homassy.API.Enums;
using Homassy.API.Models.FamilyChat;
using Homassy.API.Models.Product;
using Homassy.Tests.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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

    #region Pictures (#147)

    [Fact]
    public async Task SendImageMessage_ValidPicture_AnswersWithImageUrlsAndNoBytes()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-image");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Picture Family");

            var response = await _client.PostAsJsonAsync(
                "/api/v1.0/familychat/messages/image",
                new SendFamilyChatImageRequest { ImageBase64 = CreatePngBase64(), Caption = "the shelf" });

            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var parsed = await response.Content.ReadFromJsonAsync<ApiResponse<FamilyChatMessageInfo>>();
            var message = parsed!.Data!;

            Assert.Equal(FamilyChatMessageKind.Image, message.Kind);
            Assert.Equal("the shelf", message.Body);
            Assert.NotNull(message.ImageUrl);
            Assert.NotNull(message.ImageFullUrl);
            Assert.True(message.ImageWidth > 0);
            Assert.True(message.ImageHeight > 0);

            // The whole point of the separate image table: the payload carries a path, and the
            // bytes are nowhere in it.
            Assert.DoesNotContain("imageBase64", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetMessageImage_OwnFamily_ServesTheBytesWithCachingHeaders()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-image-get");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Serving Family");

            var sent = await SendImageAsync();

            var response = await _client.GetAsync($"/api/v1.0/familychat/messages/{sent.PublicId}/image");

            _output.WriteLine($"Status: {response.StatusCode}, type: {response.Content.Headers.ContentType}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Headers.ETag);
            Assert.Contains("immutable", response.Headers.CacheControl?.ToString() ?? string.Empty);
            Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 0);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetMessageImage_AnotherFamilysMessage_ReturnsNotFound()
    {
        string? ownerEmail = null;
        string? outsiderEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-image-owner");
            ownerEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Private Picture Family");
            var sent = await SendImageAsync();

            _authHelper.ClearAuthToken();
            var (otherEmail, otherAuth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-image-outsider");
            outsiderEmail = otherEmail;
            _authHelper.SetAuthToken(otherAuth.AccessToken);
            await CreateFamilyAsync("Nosy Family");

            var response = await _client.GetAsync($"/api/v1.0/familychat/messages/{sent.PublicId}/image");

            _output.WriteLine($"Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (ownerEmail != null) await _authHelper.CleanupUserAsync(ownerEmail);
            if (outsiderEmail != null) await _authHelper.CleanupUserAsync(outsiderEmail);
        }
    }

    #endregion

    #region Unread and read state (#149)

    [Fact]
    public async Task GetUnreadCount_DoesNotCountYourOwnMessages()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-unread-own");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Own Messages Family");

            await SendAsync("talking to myself");
            await SendAsync("still talking");

            var unread = await GetUnreadCountAsync();

            _output.WriteLine($"Unread after own messages: {unread}");
            Assert.Equal(0, unread);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task UnreadCount_CountsAnotherMembersMessagesAndMarkReadClearsThem()
    {
        string? ownerEmail = null;
        string? memberEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-unread-owner");
            ownerEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var shareCode = await CreateFamilyAsync("Unread Family");

            var (secondEmail, secondAuth) = await AddFamilyMemberAsync(shareCode, auth.AccessToken, "chat-unread-member");
            memberEmail = secondEmail;

            // The new member says two things.
            _authHelper.ClearAuthToken();
            _authHelper.SetAuthToken(secondAuth.AccessToken);
            await SendAsync("dinner at seven?");
            await SendAsync("or eight");

            // The owner has not read them.
            _authHelper.ClearAuthToken();
            _authHelper.SetAuthToken(auth.AccessToken);

            var beforeRead = await GetUnreadCountAsync();
            _output.WriteLine($"Unread before read: {beforeRead}");
            Assert.Equal(2, beforeRead);

            var afterRead = await MarkReadAsync();
            _output.WriteLine($"Unread after read: {afterRead}");
            Assert.Equal(0, afterRead);

            // And it stays read - the marker only ever moves forward.
            Assert.Equal(0, await GetUnreadCountAsync());
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (ownerEmail != null) await _authHelper.CleanupUserAsync(ownerEmail);
            if (memberEmail != null) await _authHelper.CleanupUserAsync(memberEmail);
        }
    }

    [Fact]
    public async Task GetUnreadCount_IgnoresAnotherFamilysMessages()
    {
        string? ownerEmail = null;
        string? outsiderEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-unread-a");
            ownerEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Loud Family");
            await SendAsync("nothing to do with you");

            _authHelper.ClearAuthToken();
            var (otherEmail, otherAuth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-unread-b");
            outsiderEmail = otherEmail;
            _authHelper.SetAuthToken(otherAuth.AccessToken);
            await CreateFamilyAsync("Quiet Family");

            Assert.Equal(0, await GetUnreadCountAsync());
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (ownerEmail != null) await _authHelper.CleanupUserAsync(ownerEmail);
            if (outsiderEmail != null) await _authHelper.CleanupUserAsync(outsiderEmail);
        }
    }

    [Fact]
    public async Task MarkRead_WithoutFamily_ReturnsForbidden()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-read-nofamily");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.PostAsync("/api/v1.0/familychat/read", null);

            _output.WriteLine($"Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    #endregion

    #region References

    [Fact]
    public async Task SendMessage_WithAProductReference_AnswersWithAResolvedChip()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-ref");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Reference Family");

            var product = await CreateProductAsync("Tejföl");

            var response = await _client.PostAsJsonAsync(
                "/api/v1.0/familychat/messages",
                new SendFamilyChatMessageRequest
                {
                    Body = "vegyél ilyet",
                    References =
                    [
                        new FamilyChatReferenceRequest
                        {
                            Kind = FamilyChatReferenceKind.Product,
                            PublicId = product
                        }
                    ]
                });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var parsed = await response.Content.ReadFromJsonAsync<ApiResponse<FamilyChatMessageInfo>>();
            var reference = Assert.Single(parsed!.Data!.References);

            _output.WriteLine($"Reference: {reference.Kind} {reference.Label} available={reference.IsAvailable}");
            Assert.Equal(FamilyChatReferenceKind.Product, reference.Kind);
            Assert.Equal(product, reference.PublicId);
            // The label is the server's, resolved from what this sender may see - never the
            // client's word for it.
            Assert.Equal(ProductLabel("Tejföl"), reference.Label);
            Assert.True(reference.IsAvailable);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task SendMessage_WithOnlyAReference_IsAccepted()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-ref-only");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Wordless Family");

            var product = await CreateProductAsync("Kenyér");

            // No body at all: pointing at the thing is the whole message.
            var response = await _client.PostAsJsonAsync(
                "/api/v1.0/familychat/messages",
                new SendFamilyChatMessageRequest
                {
                    References = [new FamilyChatReferenceRequest { Kind = FamilyChatReferenceKind.Product, PublicId = product }]
                });

            _output.WriteLine($"Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var parsed = await response.Content.ReadFromJsonAsync<ApiResponse<FamilyChatMessageInfo>>();
            Assert.Null(parsed!.Data!.Body);
            Assert.Single(parsed.Data.References);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null) await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task SendMessage_WithAReferenceTheSenderCannotSee_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-ref-bogus");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Strict Family");

            // An id this caller was never offered. The server resolves references against the same
            // list the picker reads, so this cannot become a chip naming something unseen.
            var response = await _client.PostAsJsonAsync(
                "/api/v1.0/familychat/messages",
                new SendFamilyChatMessageRequest
                {
                    Body = "nézd",
                    References = [new FamilyChatReferenceRequest { Kind = FamilyChatReferenceKind.Product, PublicId = Guid.NewGuid() }]
                });

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
    public async Task GetMessages_ReferenceLabel_FollowsTheProductsCurrentName()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("chat-ref-rename");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Renaming Family");

            var product = await CreateProductAsync("Régi név");

            await _client.PostAsJsonAsync(
                "/api/v1.0/familychat/messages",
                new SendFamilyChatMessageRequest
                {
                    Body = "erről van szó",
                    References = [new FamilyChatReferenceRequest { Kind = FamilyChatReferenceKind.Product, PublicId = product }]
                });

            // Renaming the product must change what the old message says it points at - that is the
            // whole reason a reference stores an id rather than only the name it had.
            var rename = await _client.PutAsJsonAsync(
                $"/api/v1.0/product/{product}",
                new { Name = "Új név" });
            Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

            var page = await GetPageAsync();
            var message = page.Items.First(m => m.Body == "erről van szó");

            _output.WriteLine($"Label after rename: {message.References[0].Label}");
            Assert.Equal(ProductLabel("Új név"), message.References[0].Label);
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

    /// <summary>
    /// Creates a product the caller can actually reference, and returns its public id.
    /// </summary>
    /// <remarks>
    /// The inventory item is not incidental: `SelectValueType.Product` lists the products a user
    /// has stock of, which is the same list every product picker in the app shows - and the same
    /// list a chat reference is validated against. A product with no inventory item is in nobody's
    /// picker, so it is not something a message can point at either.
    /// </remarks>
    private async Task<Guid> CreateProductAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/product",
            new CreateProductRequest { Name = name, Brand = "Test", Unit = Homassy.API.Enums.Unit.Piece });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var parsed = await response.Content.ReadFromJsonAsync<ApiResponse<ProductInfo>>();
        Assert.NotNull(parsed?.Data);
        var publicId = parsed!.Data!.PublicId;

        var stock = await _client.PostAsJsonAsync(
            "/api/v1.0/product/inventory/quick",
            new QuickAddInventoryItemRequest { ProductPublicId = publicId, Quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, stock.StatusCode);

        return publicId;
    }

    /// <summary>
    /// What the product select list calls a product: brand and name, which is what a chip shows.
    /// </summary>
    private static string ProductLabel(string name) => $"Test - {name}";

    private async Task<int> GetUnreadCountAsync()
    {
        var response = await _client.GetAsync("/api/v1.0/familychat/unread-count");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var parsed = await response.Content.ReadFromJsonAsync<ApiResponse<FamilyChatUnreadResponse>>();
        Assert.NotNull(parsed?.Data);
        return parsed!.Data!.TotalCount;
    }

    private async Task<int> MarkReadAsync()
    {
        var response = await _client.PostAsync("/api/v1.0/familychat/read", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var parsed = await response.Content.ReadFromJsonAsync<ApiResponse<FamilyChatUnreadResponse>>();
        Assert.NotNull(parsed?.Data);
        return parsed!.Data!.TotalCount;
    }

    /// <summary>
    /// Creates a second user and walks them through the join-request flow into the family whose
    /// share code this is, leaving the caller authenticated as nobody.
    /// </summary>
    private async Task<(string Email, TestAuthHelper.AuthResponse Auth)> AddFamilyMemberAsync(
        string shareCode, string approverToken, string prefix)
    {
        _authHelper.ClearAuthToken();
        var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync(prefix);
        _authHelper.SetAuthToken(auth.AccessToken);

        var joinResponse = await _client.PostAsJsonAsync(
            "/api/v1.0/family/join-requests",
            new JoinFamilyRequest { ShareCode = shareCode });
        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);

        _authHelper.ClearAuthToken();
        _authHelper.SetAuthToken(approverToken);

        var pending = await _client.GetFromJsonAsync<ApiResponse<List<FamilyJoinRequestResponse>>>(
            "/api/v1.0/family/join-requests");
        var request = Assert.Single(pending!.Data!);

        var approveResponse = await _client.PostAsync(
            $"/api/v1.0/family/join-requests/{request.PublicId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        _authHelper.ClearAuthToken();
        return (email, auth);
    }

    private async Task<FamilyChatMessageInfo> SendImageAsync(string? caption = null)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1.0/familychat/messages/image",
            new SendFamilyChatImageRequest { ImageBase64 = CreatePngBase64(), Caption = caption });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var parsed = await response.Content.ReadFromJsonAsync<ApiResponse<FamilyChatMessageInfo>>();
        Assert.NotNull(parsed?.Data);
        return parsed!.Data!;
    }

    /// <summary>A real, decodable PNG — the upload path runs it through the image decoder.</summary>
    private static string CreatePngBase64(int width = 64, int height = 48)
    {
        using var image = new Image<Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return Convert.ToBase64String(stream.ToArray());
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
