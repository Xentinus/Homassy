using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
using Homassy.API.Models.ImageUpload;
using Homassy.Tests.Infrastructure;
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

    [Fact]
    public async Task UpdateUserSettings_WithUnknownIdentityColor_Returns400()
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
                IdentityColor = "#ff0000"
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/v1.0/user/settings", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Assert: a free-form colour must never reach the column - the palette is what
            // guarantees contrast in both themes.
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
}
