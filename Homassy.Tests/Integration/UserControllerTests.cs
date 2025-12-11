using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
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
        var request = new UploadProfilePictureRequest { ProfilePictureBase64 = "dGVzdA==" };

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

            var request = new UploadProfilePictureRequest
            {
                ProfilePictureBase64 = "not-valid-base64!!!"
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

            // Valid Base64 image (1x1 transparent PNG)
            var validBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

            // Step 1: Upload
            _output.WriteLine("=== Step 1: Upload Profile Picture ===");
            var uploadRequest = new UploadProfilePictureRequest { ProfilePictureBase64 = validBase64 };
            var uploadResponse = await _client.PostAsJsonAsync("/api/v1.0/user/profile-picture", uploadRequest);
            var uploadBody = await uploadResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Upload Status: {uploadResponse.StatusCode}");
            _output.WriteLine($"Upload Response: {uploadBody}");
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

            // Step 2: Verify in profile
            _output.WriteLine("\n=== Step 2: Verify in Profile ===");
            var profileResponse = await _client.GetAsync("/api/v1.0/user/profile");
            var profileContent = await profileResponse.Content.ReadFromJsonAsync<ApiResponse<UserProfileResponse>>();

            Assert.NotNull(profileContent?.Data?.ProfilePictureBase64);
            _output.WriteLine("Profile picture exists in profile");

            // Step 3: Delete
            _output.WriteLine("\n=== Step 3: Delete Profile Picture ===");
            var deleteResponse = await _client.DeleteAsync("/api/v1.0/user/profile-picture");
            var deleteBody = await deleteResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Delete Status: {deleteResponse.StatusCode}");
            _output.WriteLine($"Delete Response: {deleteBody}");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

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
}
