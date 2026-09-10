using System.Net;
using System.Net.Http.Json;
using Homassy.API.Entities.Activity;
using Homassy.API.Entities.Family;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Activity;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
using Homassy.API.Models.ImageUpload;
using Homassy.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

public class UserControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public UserControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Unauthorized Tests
    [Fact]
    public async Task GetProfile_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1.0/user/profile");

        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSettings_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new UpdateUserSettingsRequest { Name = "New Name" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1.0/user/settings", request);

        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadProfilePicture_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new UploadUserProfileImageRequest { ImageBase64 = "dGVzdA==" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/user/profile-picture", request);

        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProfilePicture_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.DeleteAsync("/api/v1.0/user/profile-picture");

        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfilePicture_WithoutToken_ReturnsUnauthorized()
    {
        // The endpoint answers with image bytes rather than an ApiResponse envelope, so it is
        // worth pinning that it is still behind [Authorize] like the rest of the controller.
        var response = await _client.GetAsync($"/api/v1.0/user/{Guid.NewGuid()}/profile-picture");

        _output.WriteLine($"Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Authenticated Tests
    [Fact]
    public async Task GetProfile_WithValidToken_ReturnsProfile()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("profile");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Act
            var response = await _client.GetAsync("/api/v1.0/user/profile");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<UserProfileResponse>>();
            Assert.NotNull(content);
            Assert.True(content.Success);
            Assert.NotNull(content.Data);
            Assert.Equal(email.ToLowerInvariant(), content.Data.Email);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task UpdateSettings_ValidRequest_ReturnsSuccess()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("settings");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new UpdateUserSettingsRequest
            {
                DisplayName = "Updated Display Name"
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/v1.0/user/settings", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task UpdateSettings_InvalidEmail_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("invalid-email");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new UpdateUserSettingsRequest
            {
                Email = "not-a-valid-email"
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/v1.0/user/settings", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task UpdateSettings_NameTooShort_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("short-name");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new UpdateUserSettingsRequest
            {
                Name = "A" // Too short, minimum is 2
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/v1.0/user/settings", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task UploadProfilePicture_InvalidBase64_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("invalid-base64");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new UploadUserProfileImageRequest
            {
                ImageBase64 = "not-valid-base64!!!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1.0/user/profile-picture", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task DeleteProfilePicture_NoProfilePicture_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("no-picture");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Act - try to delete when no picture exists
            var response = await _client.DeleteAsync("/api/v1.0/user/profile-picture");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task UploadAndDeleteProfilePicture_FullFlow_Succeeds()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("picture-flow");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // 50x50 - the upload path's minimum dimensions.
            var validBase64 = TestImages.PngBase64();

            // Step 1: Upload
            _output.WriteLine("=== Step 1: Upload Profile Picture ===");
            var uploadRequest = new UploadUserProfileImageRequest { ImageBase64 = validBase64 };
            var uploadResponse = await _client.PostAsJsonAsync("/api/v1.0/user/profile-picture", uploadRequest);
            var uploadBody = await uploadResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Upload Status: {uploadResponse.StatusCode}");
            _output.WriteLine($"Upload Response: {uploadBody}");
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

            // Wait for cache to refresh (cache service refreshes every 5 seconds)
            _output.WriteLine("Waiting 6 seconds for cache refresh...");
            await Task.Delay(TimeSpan.FromSeconds(6));

            // Step 2: Verify in profile
            _output.WriteLine("\n=== Step 2: Verify in Profile ===");
            var profileResponse = await _client.GetAsync("/api/v1.0/user/profile");
            var profileContent = await profileResponse.Content.ReadFromJsonAsync<ApiResponse<UserProfileResponse>>();

            var pictureUrl = profileContent?.Data?.ProfilePictureUrl;
            Assert.NotNull(pictureUrl);
            // The profile carries a versioned URL, not the image: that is the whole point of the
            // separate endpoint, and a payload that still embedded base64 would pass the old
            // assertion just as well.
            Assert.Contains("/profile-picture?", pictureUrl);
            Assert.Contains("v=", pictureUrl);
            _output.WriteLine($"Profile picture URL: {pictureUrl}");

            // Step 2b: the URL serves bytes, with an ETag that answers a conditional request
            _output.WriteLine("\n=== Step 2b: Fetch the image ===");
            var imageResponse = await _client.GetAsync(pictureUrl);

            Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
            Assert.StartsWith("image/", imageResponse.Content.Headers.ContentType?.MediaType);
            Assert.NotEmpty(await imageResponse.Content.ReadAsByteArrayAsync());

            var etag = imageResponse.Headers.ETag;
            Assert.NotNull(etag);
            Assert.True(imageResponse.Headers.CacheControl?.Private);
            // Answered from the thumbnail generated on upload, not by falling back to the
            // full-size image.
            Assert.Contains("thumb", etag.Tag);

            var conditional = new HttpRequestMessage(HttpMethod.Get, pictureUrl);
            conditional.Headers.IfNoneMatch.Add(etag);
            var notModified = await _client.SendAsync(conditional);

            Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
            _output.WriteLine("Conditional request answered 304");

            // Step 3: Delete
            _output.WriteLine("\n=== Step 3: Delete Profile Picture ===");
            var deleteResponse = await _client.DeleteAsync("/api/v1.0/user/profile-picture");
            var deleteBody = await deleteResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Delete Status: {deleteResponse.StatusCode}");
            _output.WriteLine($"Delete Response: {deleteBody}");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            // Step 4: the URL stops resolving once the picture is gone
            var goneResponse = await _client.GetAsync(pictureUrl);
            Assert.Equal(HttpStatusCode.NotFound, goneResponse.StatusCode);

            _output.WriteLine("\n=== Full Profile Picture Flow Completed Successfully! ===");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region Identity Color Tests
    [Fact]
    public async Task UpdateUserSettings_WithPaletteIdentityColor_PersistsAndReturnsIt()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("identity-color");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new UpdateUserSettingsRequest
            {
                IdentityColor = "teal"
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/v1.0/user/settings", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // The profile GET is cache-first (see GetAllUserDataById); the write above only
            // touches the database, so the same wait the profile-picture flow test uses applies
            // here too (cache service refreshes every 5 seconds).
            await Task.Delay(TimeSpan.FromSeconds(6));

            var profileResponse = await _client.GetAsync("/api/v1.0/user/profile");
            var profileContent = await profileResponse.Content.ReadFromJsonAsync<ApiResponse<UserProfileResponse>>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
            Assert.NotNull(profileContent?.Data);
            Assert.Equal("teal", profileContent.Data.IdentityColor);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task UpdateUserSettings_WithAutoIdentityColor_ClearsTheOverride()
    {
        string? testEmail = null;
        try
        {
            // Arrange: set "teal" first.
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("identity-color-auto");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var setRequest = new UpdateUserSettingsRequest { IdentityColor = "teal" };
            var setResponse = await _client.PutAsJsonAsync("/api/v1.0/user/settings", setRequest);
            Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);

            // Verify intermediate state: the write path actually persisted "teal" to the database.
            // This ensures the test fails if the entire IdentityColor write path becomes a silent no-op.
            var userId = _factory.GetUserIdByEmail(email);
            var (scope, context) = _factory.CreateScopedDbContext();
            await using var _ = scope as IAsyncDisposable;

            var userProfile = context.UserProfiles.FirstOrDefault(up => up.UserId == userId);
            Assert.NotNull(userProfile);
            Assert.Equal("teal", userProfile.IdentityColor);

            // Act
            var clearRequest = new UpdateUserSettingsRequest { IdentityColor = "auto" };
            var clearResponse = await _client.PutAsJsonAsync("/api/v1.0/user/settings", clearRequest);
            var clearBody = await clearResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {clearResponse.StatusCode}");
            _output.WriteLine($"Response: {clearBody}");

            Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);

            // The profile GET is cache-first (see GetAllUserDataById); the write above only
            // touches the database, so the same wait the profile-picture flow test uses applies
            // here too (cache service refreshes every 5 seconds).
            await Task.Delay(TimeSpan.FromSeconds(6));

            var profileResponse = await _client.GetAsync("/api/v1.0/user/profile");
            var profileContent = await profileResponse.Content.ReadFromJsonAsync<ApiResponse<UserProfileResponse>>();

            // Assert
            Assert.NotNull(profileContent?.Data);
            Assert.Null(profileContent.Data.IdentityColor);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Theory]
    [InlineData("#abc")]           // three-digit shorthand
    [InlineData("#abcdefgh")]      // eight-digit / not valid hex digits either way
    [InlineData("rgb(1,2,3)")]     // not a hex form at all
    [InlineData("red")]            // bare colour name
    [InlineData("unknown")]        // not a palette key either
    public async Task UpdateUserSettings_WithMalformedIdentityColor_Returns400(string malformed)
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("identity-color-bad");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new UpdateUserSettingsRequest
            {
                IdentityColor = malformed
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/v1.0/user/settings", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Assert: only a palette key, "auto", or a strict #rrggbb hex may reach the column -
            // anything else (shorthand hex, alpha hex, rgb(), a bare name, an unknown key) is a 400.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Theory]
    [InlineData("#1a2b3c", "#1a2b3c")]  // already-lowercase hex persists unchanged
    [InlineData("#1A2B3C", "#1a2b3c")]  // uppercase hex is stored lowercased
    [InlineData("#AbCdEf", "#abcdef")]  // mixed-case hex is stored lowercased
    public async Task UpdateUserSettings_WithHexIdentityColor_PersistsLowercased(string submitted, string expectedStored)
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("identity-color-hex");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new UpdateUserSettingsRequest
            {
                IdentityColor = submitted
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/v1.0/user/settings", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Read the persisted row directly rather than through the cache-first profile GET, so
            // this does not need the 5s cache-refresh wait the palette-key tests above use.
            var userId = _factory.GetUserIdByEmail(email);
            var (scope, context) = _factory.CreateScopedDbContext();
            await using var _ = scope as IAsyncDisposable;

            var userProfile = context.UserProfiles.FirstOrDefault(up => up.UserId == userId);
            Assert.NotNull(userProfile);
            Assert.Equal(expectedStored, userProfile.IdentityColor);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetProfile_ReturnsPublicId()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("profile-publicid");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Act
            var response = await _client.GetAsync("/api/v1.0/user/profile");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Assert: the client needs a non-empty publicId to derive the deterministic colour
            // when no override is set.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<UserProfileResponse>>();
            Assert.NotNull(content?.Data);
            Assert.NotEqual(Guid.Empty, content.Data.PublicId);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region Activity Timeline Tests

    /// <summary>
    /// Inserts an Activity row directly (bypassing RecordActivityAsync, which always stamps
    /// DateTime.UtcNow) so tests can control the exact timestamps the aggregation and cursor
    /// logic key off. Activities are never cached by ActivityFunctions' read paths (unlike
    /// User/UserProfile), so this is visible to the endpoint immediately - no cache refresh needed.
    /// </summary>
    private async Task<Guid> SeedActivityAsync(
        int userId,
        ActivityType activityType,
        DateTime timestamp,
        string recordName,
        int? familyId = null)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        var activity = new Activity
        {
            UserId = userId,
            FamilyId = familyId,
            Timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
            ActivityType = activityType,
            RecordId = 1,
            RecordName = recordName
        };
        context.Activities.Add(activity);
        await context.SaveChangesAsync();

        return activity.PublicId;
    }

    /// <summary>
    /// Creates a family directly and assigns it as every given user's FamilyId, refreshing each
    /// user's cache entry immediately (mirrors CreateAndAuthenticateUserAsync's own priming) so a
    /// same-request-after-setup call sees the new family without racing CacheManagementService's
    /// poller.
    /// </summary>
    private async Task<int> CreateFamilyWithMembersAsync(params int[] userIds)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        var family = new Family { Name = $"Timeline Test Family {Guid.NewGuid():N}" };
        context.Families.Add(family);
        await context.SaveChangesAsync();

        foreach (var userId in userIds)
        {
            var user = context.Users.First(u => u.Id == userId);
            user.FamilyId = family.Id;
        }
        await context.SaveChangesAsync();

        var userFunctions = scope.ServiceProvider.GetRequiredService<UserFunctions>();
        foreach (var userId in userIds)
            await userFunctions.RefreshUserCacheAsync(userId);

        return family.Id;
    }

    private async Task DeleteFamilyAsync(int familyId)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        var family = context.Families.FirstOrDefault(f => f.Id == familyId);
        if (family != null)
        {
            context.Families.Remove(family);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Sets a member's IdentityColor override directly in the database, then force-refreshes the
    /// UserFunctions profile cache entry (same helper CreateAndAuthenticateUserAsync uses at
    /// creation). GetAllUsersDataByIds - what the timeline endpoint resolves actors through - is
    /// cache-first, and the user was already cached with IdentityColor == null at creation; without
    /// this the endpoint would see the stale value until CacheManagementService's 5s poller ran,
    /// which is exactly the sleep this test suite is not allowed to add.
    /// </summary>
    private async Task SetIdentityColorAsync(int userId, string color)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        await using var _ = scope as IAsyncDisposable;

        var profile = context.UserProfiles.First(p => p.UserId == userId);
        profile.IdentityColor = color;
        await context.SaveChangesAsync();

        var userFunctions = scope.ServiceProvider.GetRequiredService<UserFunctions>();
        await userFunctions.RefreshUserProfileCacheAsync(profile.Id);
    }

    private Guid GetUserPublicId(int userId)
    {
        var (scope, context) = _factory.CreateScopedDbContext();
        using var _ = scope;

        return context.Users.First(u => u.Id == userId).PublicId;
    }

    [Fact]
    public async Task GetActivityTimeline_WithoutCursor_ReturnsNewestFirstAndACursor()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-first-page");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var userId = _factory.GetUserIdByEmail(email)!.Value;

            var baseTime = DateTime.UtcNow.AddHours(-2);
            await SeedActivityAsync(userId, ActivityType.ProductCreate, baseTime, "Oldest");
            var middle = await SeedActivityAsync(userId, ActivityType.ProductCreate, baseTime.AddMinutes(10), "Middle");
            var newest = await SeedActivityAsync(userId, ActivityType.ProductCreate, baseTime.AddMinutes(20), "Newest");

            var response = await _client.GetAsync("/api/v1.0/user/activities/timeline?pageSize=2");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(2, content.Data.Entries.Count);
            Assert.Equal(newest, content.Data.Entries[0].PublicId);
            Assert.Equal(middle, content.Data.Entries[1].PublicId);
            Assert.NotNull(content.Data.NextCursor);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// The optional window (#127): <c>since</c> is exclusive, <c>until</c> is inclusive, and both
    /// narrow the same timeline rather than switching to a different code path. This is what the
    /// away-delta card links to, so the window it was counted over and the window the timeline
    /// shows have to agree exactly.
    /// </summary>
    [Fact]
    public async Task GetActivityTimeline_WithAWindow_ReturnsOnlyTheActivityInsideIt()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-window");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var userId = _factory.GetUserIdByEmail(email)!.Value;

            var since = DateTime.UtcNow.AddHours(-2);
            var until = DateTime.UtcNow.AddHours(-1);

            await SeedActivityAsync(userId, ActivityType.ProductCreate, since.AddMinutes(-30), "Before the window");
            var inside = await SeedActivityAsync(userId, ActivityType.ProductCreate, since.AddMinutes(30), "Inside the window");
            await SeedActivityAsync(userId, ActivityType.ProductCreate, until.AddMinutes(30), "After the window");

            var query = $"since={Uri.EscapeDataString(since.ToString("O"))}&until={Uri.EscapeDataString(until.ToString("O"))}";
            var response = await _client.GetAsync($"/api/v1.0/user/activities/timeline?pageSize=30&{query}");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(content?.Data);

            var entry = Assert.Single(content.Data.Entries);
            Assert.Equal(inside, entry.PublicId);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// No window is still the whole timeline - the parameters are optional, and adding them must not
    /// have changed what an ordinary request returns.
    /// </summary>
    [Fact]
    public async Task GetActivityTimeline_WithoutAWindow_StillReturnsEverything()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-no-window");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var userId = _factory.GetUserIdByEmail(email)!.Value;

            var baseTime = DateTime.UtcNow.AddHours(-3);
            await SeedActivityAsync(userId, ActivityType.ProductCreate, baseTime, "Old");
            await SeedActivityAsync(userId, ActivityType.ProductUpdate, baseTime.AddHours(2), "Recent");

            var response = await _client.GetAsync("/api/v1.0/user/activities/timeline?pageSize=30");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(2, content.Data.Entries.Count);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_FollowingTheCursor_DoesNotRepeatOrSkipEntries()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-walk-pages");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var userId = _factory.GetUserIdByEmail(email)!.Value;

            // 12 activities, 10 minutes apart (well outside the 5-minute aggregation bucket) so
            // this test isolates paging correctness from the separate aggregation behaviour.
            var baseTime = DateTime.UtcNow.AddHours(-3);
            var seededIds = new List<Guid>();
            for (var i = 0; i < 12; i++)
            {
                var id = await SeedActivityAsync(userId, ActivityType.ProductCreate, baseTime.AddMinutes(i * 10), $"Item {i}");
                seededIds.Add(id);
            }

            var collected = new List<Guid>();
            string? cursor = null;
            var pagesFetched = 0;
            const int maxPages = 20; // safety net: a stuck cursor fails the test instead of looping forever

            do
            {
                var query = cursor == null ? "pageSize=5" : $"pageSize=5&cursor={Uri.EscapeDataString(cursor)}";
                var response = await _client.GetAsync($"/api/v1.0/user/activities/timeline?{query}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
                Assert.NotNull(content?.Data);

                collected.AddRange(content.Data.Entries.Select(e => e.PublicId));
                cursor = content.Data.NextCursor;
                pagesFetched++;

                Assert.True(pagesFetched <= maxPages, "Paging did not terminate - the cursor is likely stuck.");
            } while (cursor != null);

            _output.WriteLine($"Pages fetched: {pagesFetched}, entries collected: {collected.Count}");

            // No duplicates and nothing missing: the union of collected publicIds must equal the
            // seeded set exactly.
            Assert.Equal(seededIds.Count, collected.Count);
            Assert.Equal(seededIds.Count, collected.Distinct().Count());
            Assert.Empty(seededIds.Except(collected));
            Assert.Empty(collected.Except(seededIds));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_WhenANewActivityIsInsertedMidPaging_DoesNotShiftTheWindow()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-insert-mid-paging");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var userId = _factory.GetUserIdByEmail(email)!.Value;

            var baseTime = DateTime.UtcNow.AddHours(-4);
            for (var i = 0; i < 8; i++)
                await SeedActivityAsync(userId, ActivityType.ProductCreate, baseTime.AddMinutes(i * 10), $"Item {i}");

            var page1Response = await _client.GetAsync("/api/v1.0/user/activities/timeline?pageSize=5");
            Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
            var page1Content = await page1Response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(page1Content?.Data);
            Assert.Equal(5, page1Content.Data.Entries.Count);
            var page1Ids = page1Content.Data.Entries.Select(e => e.PublicId).ToList();
            var cursor = page1Content.Data.NextCursor;
            Assert.NotNull(cursor);

            // This is exactly the race the cursor exists for: a brand-new activity, newer than
            // everything seeded so far, inserted while the client is sitting mid-scroll on page 1.
            var insertedId = await SeedActivityAsync(userId, ActivityType.ProductCreate, DateTime.UtcNow, "Inserted mid-paging");

            var page2Response = await _client.GetAsync($"/api/v1.0/user/activities/timeline?pageSize=5&cursor={Uri.EscapeDataString(cursor!)}");
            Assert.Equal(HttpStatusCode.OK, page2Response.StatusCode);
            var page2Content = await page2Response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(page2Content?.Data);
            var page2Ids = page2Content.Data.Entries.Select(e => e.PublicId).ToList();

            Assert.DoesNotContain(insertedId, page2Ids);
            Assert.Empty(page1Ids.Intersect(page2Ids));
            Assert.Equal(3, page2Ids.Count); // the 3 older seeded activities page 1 did not consume
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_CollapsesSameActorSameTypeRun()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-collapse-run");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var userId = _factory.GetUserIdByEmail(email)!.Value;

            var baseTime = DateTime.UtcNow.AddMinutes(-10);
            for (var i = 0; i < 12; i++)
                await SeedActivityAsync(userId, ActivityType.ShoppingListItemAdd, baseTime.AddSeconds(i * 2), $"Bulk item {i}");

            // pageSize=5 on purpose: the over-fetch (pageSize*4+1 = 21) still covers all 12 raw
            // rows, so the run collapses fully into one entry even though 12 activities dwarf the
            // requested page size - proving the over-fetch, not just the grouping.
            var response = await _client.GetAsync("/api/v1.0/user/activities/timeline?pageSize=5");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(content?.Data);
            Assert.Single(content.Data.Entries);
            var entry = content.Data.Entries[0];
            Assert.Equal(12, entry.Count);
            Assert.NotNull(entry.Items);
            Assert.Equal(12, entry.Items!.Count);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_DoesNotCollapseAcrossActors()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        int? familyId = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-actor-a");
            testEmailA = emailA;
            var (emailB, _) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-actor-b");
            testEmailB = emailB;

            var userIdA = _factory.GetUserIdByEmail(emailA)!.Value;
            var userIdB = _factory.GetUserIdByEmail(emailB)!.Value;
            familyId = await CreateFamilyWithMembersAsync(userIdA, userIdB);

            var baseTime = DateTime.UtcNow.AddMinutes(-30);
            await SeedActivityAsync(userIdA, ActivityType.ProductCreate, baseTime, "A's item", familyId);
            await SeedActivityAsync(userIdB, ActivityType.ProductCreate, baseTime.AddSeconds(10), "B's item", familyId);

            _authHelper.SetAuthToken(authA.AccessToken);
            var response = await _client.GetAsync("/api/v1.0/user/activities/timeline?pageSize=10");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(2, content.Data.Entries.Count);
            Assert.All(content.Data.Entries, e => Assert.Equal(1, e.Count));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmailA != null)
                await _authHelper.CleanupUserAsync(testEmailA);
            if (testEmailB != null)
                await _authHelper.CleanupUserAsync(testEmailB);
            if (familyId.HasValue)
                await DeleteFamilyAsync(familyId.Value);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_DoesNotCollapseAcrossTheTimeBucket()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-time-bucket");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var userId = _factory.GetUserIdByEmail(email)!.Value;

            var baseTime = DateTime.UtcNow.AddMinutes(-30);
            await SeedActivityAsync(userId, ActivityType.ProductCreate, baseTime, "First");
            await SeedActivityAsync(userId, ActivityType.ProductCreate, baseTime.AddMinutes(10), "Ten minutes later");

            var response = await _client.GetAsync("/api/v1.0/user/activities/timeline?pageSize=10");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(2, content.Data.Entries.Count);
            Assert.All(content.Data.Entries, e => Assert.Equal(1, e.Count));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_FiltersByActivityTypeAndByMember()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        int? familyId = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-filter-a");
            testEmailA = emailA;
            var (emailB, _) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-filter-b");
            testEmailB = emailB;

            var userIdA = _factory.GetUserIdByEmail(emailA)!.Value;
            var userIdB = _factory.GetUserIdByEmail(emailB)!.Value;
            familyId = await CreateFamilyWithMembersAsync(userIdA, userIdB);
            var userPublicIdA = GetUserPublicId(userIdA);

            var baseTime = DateTime.UtcNow.AddMinutes(-30);
            await SeedActivityAsync(userIdA, ActivityType.ProductCreate, baseTime, "A ProductCreate", familyId);
            await SeedActivityAsync(userIdA, ActivityType.ShoppingListCreate, baseTime.AddMinutes(11), "A ShoppingListCreate", familyId);
            await SeedActivityAsync(userIdB, ActivityType.ProductCreate, baseTime.AddMinutes(22), "B ProductCreate", familyId);

            _authHelper.SetAuthToken(authA.AccessToken);

            // Filter by type: both ProductCreate entries (A's and B's), not A's ShoppingListCreate.
            var typeResponse = await _client.GetAsync(
                $"/api/v1.0/user/activities/timeline?pageSize=10&activityType={(int)ActivityType.ProductCreate}");
            Assert.Equal(HttpStatusCode.OK, typeResponse.StatusCode);
            var typeContent = await typeResponse.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(typeContent?.Data);
            Assert.Equal(2, typeContent.Data.Entries.Count);
            Assert.All(typeContent.Data.Entries, e => Assert.Equal(ActivityType.ProductCreate, e.ActivityType));

            // Filter by member: only A's two activities (both types), never B's.
            var memberResponse = await _client.GetAsync(
                $"/api/v1.0/user/activities/timeline?pageSize=10&userPublicId={userPublicIdA}");
            Assert.Equal(HttpStatusCode.OK, memberResponse.StatusCode);
            var memberContent = await memberResponse.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(memberContent?.Data);
            Assert.Equal(2, memberContent.Data.Entries.Count);
            Assert.All(memberContent.Data.Entries, e => Assert.Equal(userPublicIdA, e.UserPublicId));
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmailA != null)
                await _authHelper.CleanupUserAsync(testEmailA);
            if (testEmailB != null)
                await _authHelper.CleanupUserAsync(testEmailB);
            if (familyId.HasValue)
                await DeleteFamilyAsync(familyId.Value);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_LastPageReturnsNullCursor()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-last-page");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var userId = _factory.GetUserIdByEmail(email)!.Value;

            var baseTime = DateTime.UtcNow.AddMinutes(-30);
            await SeedActivityAsync(userId, ActivityType.ProductCreate, baseTime, "Only one");
            await SeedActivityAsync(userId, ActivityType.ProductCreate, baseTime.AddMinutes(20), "Only two");

            var response = await _client.GetAsync("/api/v1.0/user/activities/timeline?pageSize=5");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(content?.Data);
            Assert.Equal(2, content.Data.Entries.Count);
            Assert.Null(content.Data.NextCursor);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_WithAnInvalidCursor_Returns400()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-bad-cursor");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync("/api/v1.0/user/activities/timeline?cursor=not-a-valid-cursor!!");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_EntriesCarryTheActorsIdentityColor()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-identity-color");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var userId = _factory.GetUserIdByEmail(email)!.Value;

            await SetIdentityColorAsync(userId, "teal");
            await SeedActivityAsync(userId, ActivityType.ProductCreate, DateTime.UtcNow.AddMinutes(-5), "Colored item");

            var response = await _client.GetAsync("/api/v1.0/user/activities/timeline?pageSize=10");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(content?.Data);
            Assert.Single(content.Data.Entries);
            Assert.Equal("teal", content.Data.Entries[0].UserIdentityColor);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
    #endregion

    #region Activity Visibility Filter Tests (member filter scoped to the caller's own family)

    // Fix: ApplyActivityVisibilityFilter used to apply `a.UserId == requestedUser.Id` for a named
    // UserPublicId with no family constraint at all, so any authenticated user could read a
    // stranger's whole activity feed by guessing (or otherwise obtaining) their public GUID. The
    // three scenarios below are exercised against both GET /activities and GET
    // /activities/timeline, since both now go through the same shared filter.

    [Fact]
    public async Task GetActivities_FilterByOwnPublicId_ReturnsOwnRows()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("activities-vis-self");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var userId = _factory.GetUserIdByEmail(email)!.Value;
            var userPublicId = GetUserPublicId(userId);

            await SeedActivityAsync(userId, ActivityType.ProductCreate, DateTime.UtcNow.AddMinutes(-5), "My own item");

            var response = await _client.GetAsync($"/api/v1.0/user/activities?userPublicId={userPublicId}&pageSize=10");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ActivityInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.Single(content.Data.Items);
            Assert.Equal(userPublicId, content.Data.Items[0].UserPublicId);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetActivities_FilterByFamilyMembersPublicId_ReturnsThatMembersRows()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        int? familyId = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("activities-vis-member-a");
            testEmailA = emailA;
            var (emailB, _) = await _authHelper.CreateAndAuthenticateUserAsync("activities-vis-member-b");
            testEmailB = emailB;

            var userIdA = _factory.GetUserIdByEmail(emailA)!.Value;
            var userIdB = _factory.GetUserIdByEmail(emailB)!.Value;
            familyId = await CreateFamilyWithMembersAsync(userIdA, userIdB);
            var userPublicIdB = GetUserPublicId(userIdB);

            await SeedActivityAsync(userIdA, ActivityType.ProductCreate, DateTime.UtcNow.AddMinutes(-10), "A's item", familyId);
            await SeedActivityAsync(userIdB, ActivityType.ProductCreate, DateTime.UtcNow.AddMinutes(-5), "B's item", familyId);

            _authHelper.SetAuthToken(authA.AccessToken);
            var response = await _client.GetAsync($"/api/v1.0/user/activities?userPublicId={userPublicIdB}&pageSize=10");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ActivityInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.Single(content.Data.Items);
            Assert.Equal(userPublicIdB, content.Data.Items[0].UserPublicId);
            Assert.Equal("B's item", content.Data.Items[0].RecordName);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmailA != null)
                await _authHelper.CleanupUserAsync(testEmailA);
            if (testEmailB != null)
                await _authHelper.CleanupUserAsync(testEmailB);
            if (familyId.HasValue)
                await DeleteFamilyAsync(familyId.Value);
        }
    }

    [Fact]
    public async Task GetActivities_FilterByUserInAnotherFamily_ReturnsEmptyResult()
    {
        string? testEmailA = null;
        string? testEmailStranger = null;
        int? familyIdA = null;
        int? familyIdStranger = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("activities-vis-cross-a");
            testEmailA = emailA;
            var (emailStranger, _) = await _authHelper.CreateAndAuthenticateUserAsync("activities-vis-cross-s");
            testEmailStranger = emailStranger;

            var userIdA = _factory.GetUserIdByEmail(emailA)!.Value;
            var userIdStranger = _factory.GetUserIdByEmail(emailStranger)!.Value;
            familyIdA = await CreateFamilyWithMembersAsync(userIdA);
            familyIdStranger = await CreateFamilyWithMembersAsync(userIdStranger);
            var userPublicIdStranger = GetUserPublicId(userIdStranger);

            await SeedActivityAsync(userIdStranger, ActivityType.ProductCreate, DateTime.UtcNow.AddMinutes(-5), "Stranger's item", familyIdStranger);

            _authHelper.SetAuthToken(authA.AccessToken);
            var response = await _client.GetAsync($"/api/v1.0/user/activities?userPublicId={userPublicIdStranger}&pageSize=10");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {body}");

            // Pre-fix, this returned the stranger's whole feed - record names, quantities,
            // timestamps - with nothing but the GUID's unguessability standing in the way.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ActivityInfo>>>();
            Assert.NotNull(content?.Data);
            Assert.Empty(content.Data.Items);
            Assert.Equal(0, content.Data.TotalCount);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmailA != null)
                await _authHelper.CleanupUserAsync(testEmailA);
            if (testEmailStranger != null)
                await _authHelper.CleanupUserAsync(testEmailStranger);
            if (familyIdA.HasValue)
                await DeleteFamilyAsync(familyIdA.Value);
            if (familyIdStranger.HasValue)
                await DeleteFamilyAsync(familyIdStranger.Value);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_FilterByOwnPublicId_ReturnsOwnRows()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-vis-self");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            var userId = _factory.GetUserIdByEmail(email)!.Value;
            var userPublicId = GetUserPublicId(userId);

            await SeedActivityAsync(userId, ActivityType.ProductCreate, DateTime.UtcNow.AddMinutes(-5), "My own item");

            var response = await _client.GetAsync($"/api/v1.0/user/activities/timeline?userPublicId={userPublicId}&pageSize=10");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(content?.Data);
            Assert.Single(content.Data.Entries);
            Assert.Equal(userPublicId, content.Data.Entries[0].UserPublicId);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_FilterByFamilyMembersPublicId_ReturnsThatMembersRows()
    {
        string? testEmailA = null;
        string? testEmailB = null;
        int? familyId = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-vis-member-a");
            testEmailA = emailA;
            var (emailB, _) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-vis-member-b");
            testEmailB = emailB;

            var userIdA = _factory.GetUserIdByEmail(emailA)!.Value;
            var userIdB = _factory.GetUserIdByEmail(emailB)!.Value;
            familyId = await CreateFamilyWithMembersAsync(userIdA, userIdB);
            var userPublicIdB = GetUserPublicId(userIdB);

            await SeedActivityAsync(userIdA, ActivityType.ProductCreate, DateTime.UtcNow.AddMinutes(-10), "A's item", familyId);
            await SeedActivityAsync(userIdB, ActivityType.ProductCreate, DateTime.UtcNow.AddMinutes(-5), "B's item", familyId);

            _authHelper.SetAuthToken(authA.AccessToken);
            var response = await _client.GetAsync($"/api/v1.0/user/activities/timeline?userPublicId={userPublicIdB}&pageSize=10");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(content?.Data);
            Assert.Single(content.Data.Entries);
            Assert.Equal(userPublicIdB, content.Data.Entries[0].UserPublicId);
            Assert.Equal("B's item", content.Data.Entries[0].RecordName);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmailA != null)
                await _authHelper.CleanupUserAsync(testEmailA);
            if (testEmailB != null)
                await _authHelper.CleanupUserAsync(testEmailB);
            if (familyId.HasValue)
                await DeleteFamilyAsync(familyId.Value);
        }
    }

    [Fact]
    public async Task GetActivityTimeline_FilterByUserInAnotherFamily_ReturnsEmptyResult()
    {
        string? testEmailA = null;
        string? testEmailStranger = null;
        int? familyIdA = null;
        int? familyIdStranger = null;
        try
        {
            var (emailA, authA) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-vis-cross-a");
            testEmailA = emailA;
            var (emailStranger, _) = await _authHelper.CreateAndAuthenticateUserAsync("timeline-vis-cross-s");
            testEmailStranger = emailStranger;

            var userIdA = _factory.GetUserIdByEmail(emailA)!.Value;
            var userIdStranger = _factory.GetUserIdByEmail(emailStranger)!.Value;
            familyIdA = await CreateFamilyWithMembersAsync(userIdA);
            familyIdStranger = await CreateFamilyWithMembersAsync(userIdStranger);
            var userPublicIdStranger = GetUserPublicId(userIdStranger);

            await SeedActivityAsync(userIdStranger, ActivityType.ProductCreate, DateTime.UtcNow.AddMinutes(-5), "Stranger's item", familyIdStranger);

            _authHelper.SetAuthToken(authA.AccessToken);
            var response = await _client.GetAsync($"/api/v1.0/user/activities/timeline?userPublicId={userPublicIdStranger}&pageSize=10");
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse<ActivityTimelineResult>>();
            Assert.NotNull(content?.Data);
            Assert.Empty(content.Data.Entries);
            Assert.Null(content.Data.NextCursor);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmailA != null)
                await _authHelper.CleanupUserAsync(testEmailA);
            if (testEmailStranger != null)
                await _authHelper.CleanupUserAsync(testEmailStranger);
            if (familyIdA.HasValue)
                await DeleteFamilyAsync(familyIdA.Value);
            if (familyIdStranger.HasValue)
                await DeleteFamilyAsync(familyIdStranger.Value);
        }
    }
    #endregion
}
