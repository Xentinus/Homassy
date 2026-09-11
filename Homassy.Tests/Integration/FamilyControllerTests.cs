using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

public class FamilyControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public FamilyControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    #region Unauthorized Tests
    [Fact]
    public async Task GetFamily_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1.0/family");

        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateFamily_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateFamilyRequest { Name = "Test Family" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/family/create", request);

        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateFamily_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new UpdateFamilyRequest { Name = "Updated Family" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1.0/family", request);

        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task JoinFamily_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new JoinFamilyRequest { ShareCode = "ABCD1234" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1.0/family/join-requests", request);

        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LeaveFamily_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/v1.0/family/leave", null);

        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    #endregion

    #region Validation Tests
    [Fact]
    public async Task CreateFamily_EmptyName_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("family-empty");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Send empty object (Name is required)
            var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/v1.0/family/create", content);
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
    public async Task CreateFamily_NameTooShort_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("family-short");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new CreateFamilyRequest { Name = "A" }; // Too short, minimum is 2

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1.0/family/create", request);
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
    public async Task JoinFamily_InvalidShareCode_ReturnsNotFound()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("join-invalid");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new JoinFamilyRequest { ShareCode = "INVALID1" };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1.0/family/join-requests", request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task JoinFamily_ShareCodeTooShort_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("join-short");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var request = new JoinFamilyRequest { ShareCode = "ABC" }; // Too short, must be 8

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1.0/family/join-requests", request);
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
    #endregion

    #region Authenticated Tests - No Family
    [Fact]
    public async Task GetFamily_UserWithoutFamily_ReturnsBadRequestOrUnauthorized()
    {
        string? testEmail = null;
        try
        {
            // Arrange - new user without family
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("no-family");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Act
            var response = await _client.GetAsync("/api/v1.0/family");
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            // Assert - should fail because user has no family
            Assert.True(
                response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Unauthorized,
                $"Expected BadRequest or Unauthorized, got {response.StatusCode}"
            );
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task LeaveFamily_UserWithoutFamily_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            // Arrange - new user without family
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("leave-no-family");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Act
            var response = await _client.PostAsync("/api/v1.0/family/leave", null);
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
    #endregion

    #region Full Family Flow
    [Fact]
    public async Task CreateAndManageFamily_FullFlow_Succeeds()
    {
        string? testEmail = null;
        try
        {
            // Arrange
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("family-flow");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            // Step 1: Create Family
            _output.WriteLine("=== Step 1: Create Family ===");
            var createRequest = new CreateFamilyRequest
            {
                Name = "Test Family",
                Description = "A test family for integration tests"
            };
            var createResponse = await _client.PostAsJsonAsync("/api/v1.0/family/create", createRequest);
            var createBody = await createResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Create Status: {createResponse.StatusCode}");
            _output.WriteLine($"Create Response: {createBody}");
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var createContent = await createResponse.Content.ReadFromJsonAsync<ApiResponse<FamilyInfo>>();
            Assert.NotNull(createContent?.Data?.ShareCode);
            var shareCode = createContent.Data.ShareCode;
            _output.WriteLine($"Share Code: {shareCode}");

            // Step 2: Get Family Details
            _output.WriteLine("\n=== Step 2: Get Family Details ===");
            var getResponse = await _client.GetAsync("/api/v1.0/family");
            var getBody = await getResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Get Status: {getResponse.StatusCode}");
            _output.WriteLine($"Get Response: {getBody}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            // Step 3: Update Family
            _output.WriteLine("\n=== Step 3: Update Family ===");
            var updateRequest = new UpdateFamilyRequest
            {
                Name = "Updated Test Family",
                Description = "Updated description"
            };
            var updateResponse = await _client.PutAsJsonAsync("/api/v1.0/family", updateRequest);
            var updateBody = await updateResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Update Status: {updateResponse.StatusCode}");
            _output.WriteLine($"Update Response: {updateBody}");
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            // Step 4: Leave Family
            _output.WriteLine("\n=== Step 4: Leave Family ===");
            var leaveResponse = await _client.PostAsync("/api/v1.0/family/leave", null);
            var leaveBody = await leaveResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Leave Status: {leaveResponse.StatusCode}");
            _output.WriteLine($"Leave Response: {leaveBody}");
            Assert.Equal(HttpStatusCode.OK, leaveResponse.StatusCode);

            _output.WriteLine("\n=== Full Family Flow Completed Successfully! ===");
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// Approving a pending join request actually puts the requester in the family.
    /// </summary>
    /// <remarks>
    /// Regression test. <c>LoadActionableRequestAsync</c> used to hand its callers a read-only
    /// context that it had already disposed, so both approve and decline answered 400 and a
    /// family could never gain a second member - which is every shared feature in the app.
    /// </remarks>
    [Fact]
    public async Task ApproveJoinRequest_PendingRequest_AddsRequesterToFamily()
    {
        string? ownerEmail = null;
        string? joinerEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("family-approve-owner");
            ownerEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1.0/family/create",
                new CreateFamilyRequest { Name = "Approving Family" });
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<FamilyInfo>>();
            var shareCode = created!.Data!.ShareCode;

            _authHelper.ClearAuthToken();
            var (secondEmail, secondAuth) = await _authHelper.CreateAndAuthenticateUserAsync("family-approve-joiner");
            joinerEmail = secondEmail;
            _authHelper.SetAuthToken(secondAuth.AccessToken);

            var joinResponse = await _client.PostAsJsonAsync(
                "/api/v1.0/family/join-requests",
                new JoinFamilyRequest { ShareCode = shareCode });
            Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);

            _authHelper.ClearAuthToken();
            _authHelper.SetAuthToken(auth.AccessToken);

            var pending = await _client.GetFromJsonAsync<ApiResponse<List<FamilyJoinRequestResponse>>>(
                "/api/v1.0/family/join-requests");
            var request = Assert.Single(pending!.Data!);

            var approveResponse = await _client.PostAsync(
                $"/api/v1.0/family/join-requests/{request.PublicId}/approve", null);
            var approveBody = await approveResponse.Content.ReadAsStringAsync();

            _output.WriteLine($"Approve status: {approveResponse.StatusCode}");
            _output.WriteLine($"Approve response: {approveBody}");
            Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

            var members = await _client.GetFromJsonAsync<ApiResponse<List<FamilyMemberResponse>>>(
                "/api/v1.0/family/members");
            Assert.Equal(2, members!.Data!.Count);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (ownerEmail != null) await _authHelper.CleanupUserAsync(ownerEmail);
            if (joinerEmail != null) await _authHelper.CleanupUserAsync(joinerEmail);
        }
    }
    #endregion
}
