using System.Net;
using System.Net.Http.Json;
using Homassy.API.Enums;
using Homassy.API.Models.Auth;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

public class AccountLockoutIntegrationTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public AccountLockoutIntegrationTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task VerifyCode_MultipleFailedAttempts_IncrementsCounter()
    {
        var uniqueEmail = $"test-lockout-counter-{Guid.NewGuid()}@example.com";
        var registerRequest = new CreateUserRequest
        {
            Email = uniqueEmail,
            Name = "Lockout Counter Test User"
        };

        try
        {
            await _client.PostAsJsonAsync("/api/v1.0/auth/register", registerRequest);

            var verifyRequest = new VerifyLoginRequest
            {
                Email = uniqueEmail,
                VerificationCode = "00000000"
            };

            for (int i = 0; i < 3; i++)
            {
                var response = await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
                _output.WriteLine($"Attempt {i + 1}: {response.StatusCode}");
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            var failedAttempts = _factory.GetFailedLoginAttempts(uniqueEmail);
            _output.WriteLine($"Failed attempts in DB: {failedAttempts}");

            Assert.Equal(3, failedAttempts);
        }
        finally
        {
            await _factory.CleanupTestUserAsync(uniqueEmail);
        }
    }

    [Fact]
    public async Task VerifyCode_MaxFailedAttempts_LocksAccount()
    {
        var uniqueEmail = $"test-lockout-max-{Guid.NewGuid()}@example.com";
        var registerRequest = new CreateUserRequest
        {
            Email = uniqueEmail,
            Name = "Lockout Max Test User"
        };

        try
        {
            await _client.PostAsJsonAsync("/api/v1.0/auth/register", registerRequest);

            var verifyRequest = new VerifyLoginRequest
            {
                Email = uniqueEmail,
                VerificationCode = "00000000"
            };

            for (int i = 0; i < 5; i++)
            {
                var response = await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
                _output.WriteLine($"Attempt {i + 1}: {response.StatusCode}");
            }

            var lockedOutUntil = _factory.GetLockedOutUntil(uniqueEmail);
            _output.WriteLine($"Locked out until: {lockedOutUntil}");

            Assert.NotNull(lockedOutUntil);
            Assert.True(lockedOutUntil > DateTime.UtcNow);
        }
        finally
        {
            await _factory.CleanupTestUserAsync(uniqueEmail);
        }
    }

    [Fact]
    public async Task VerifyCode_WhenLockedOut_ReturnsTooManyRequests()
    {
        var uniqueEmail = $"test-lockout-429-{Guid.NewGuid()}@example.com";
        var registerRequest = new CreateUserRequest
        {
            Email = uniqueEmail,
            Name = "Lockout 429 Test User"
        };

        try
        {
            await _client.PostAsJsonAsync("/api/v1.0/auth/register", registerRequest);

            var verifyRequest = new VerifyLoginRequest
            {
                Email = uniqueEmail,
                VerificationCode = "00000000"
            };

            for (int i = 0; i < 5; i++)
            {
                await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
            }

            var response = await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Locked account response: {response.StatusCode}");
            _output.WriteLine($"Response body: {responseBody}");

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
        finally
        {
            await _factory.CleanupTestUserAsync(uniqueEmail);
        }
    }

    [Fact]
    public async Task VerifyCode_SuccessfulLogin_ResetsFailedAttempts()
    {
        var uniqueEmail = $"test-lockout-reset-{Guid.NewGuid()}@example.com";
        var registerRequest = new CreateUserRequest
        {
            Email = uniqueEmail,
            Name = "Lockout Reset Test User"
        };

        try
        {
            await _client.PostAsJsonAsync("/api/v1.0/auth/register", registerRequest);

            var wrongCodeRequest = new VerifyLoginRequest
            {
                Email = uniqueEmail,
                VerificationCode = "00000000"
            };

            for (int i = 0; i < 3; i++)
            {
                await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", wrongCodeRequest);
            }

            var failedAttemptsBefore = _factory.GetFailedLoginAttempts(uniqueEmail);
            _output.WriteLine($"Failed attempts before correct login: {failedAttemptsBefore}");
            Assert.Equal(3, failedAttemptsBefore);

            var correctCode = _factory.GetVerificationCodeForEmail(uniqueEmail);
            Assert.NotNull(correctCode);

            var correctCodeRequest = new VerifyLoginRequest
            {
                Email = uniqueEmail,
                VerificationCode = correctCode
            };

            var response = await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", correctCodeRequest);
            _output.WriteLine($"Correct login response: {response.StatusCode}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var failedAttemptsAfter = _factory.GetFailedLoginAttempts(uniqueEmail);
            _output.WriteLine($"Failed attempts after correct login: {failedAttemptsAfter}");

            Assert.Equal(0, failedAttemptsAfter);
        }
        finally
        {
            await _factory.CleanupTestUserAsync(uniqueEmail);
        }
    }

    [Fact]
    public async Task VerifyCode_LockedAccount_ShowsLockoutErrorCode()
    {
        var uniqueEmail = $"test-lockout-message-{Guid.NewGuid()}@example.com";
        var registerRequest = new CreateUserRequest
        {
            Email = uniqueEmail,
            Name = "Lockout Message Test User"
        };

        try
        {
            await _client.PostAsJsonAsync("/api/v1.0/auth/register", registerRequest);

            var verifyRequest = new VerifyLoginRequest
            {
                Email = uniqueEmail,
                VerificationCode = "00000000"
            };

            for (int i = 0; i < 6; i++)
            {
                await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
            }

            var response = await _client.PostAsJsonAsync("/api/v1.0/auth/verify-code", verifyRequest);
            var content = await response.Content.ReadFromJsonAsync<ApiResponse>();

            _output.WriteLine($"Response error codes: {string.Join(", ", content?.ErrorCodes ?? [])}");

            Assert.NotNull(content);
            Assert.False(content.Success);
            // The error code for account locked is AUTH-0004
            Assert.NotNull(content.ErrorCodes);
            Assert.NotEmpty(content.ErrorCodes);
            Assert.Equal(ErrorCodes.AuthAccountLocked, content.ErrorCodes[0]);
        }
        finally
        {
            await _factory.CleanupTestUserAsync(uniqueEmail);
        }
    }
}
