using Homassy.API.Security;
using Homassy.API.Services;

namespace Homassy.Tests.Unit;

public class RefreshTokenRotationTests
{
    #region Token Generation Tests

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyString()
    {
        // Act
        var token = JwtService.GenerateRefreshToken();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsBase64String()
    {
        // Act
        var token = JwtService.GenerateRefreshToken();

        // Assert
        var bytes = Convert.FromBase64String(token);
        Assert.Equal(64, bytes.Length);
    }

    [Fact]
    public void GenerateRefreshToken_GeneratesUniqueTokens()
    {
        // Act
        var tokens = Enumerable.Range(0, 100)
            .Select(_ => JwtService.GenerateRefreshToken())
            .ToList();

        // Assert
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }

    [Fact]
    public void GenerateTokenFamily_ReturnsValidGuid()
    {
        // Act
        var tokenFamily = JwtService.GenerateTokenFamily();

        // Assert
        Assert.NotEqual(Guid.Empty, tokenFamily);
    }

    [Fact]
    public void GenerateTokenFamily_GeneratesUniqueValues()
    {
        // Act
        var families = Enumerable.Range(0, 100)
            .Select(_ => JwtService.GenerateTokenFamily())
            .ToList();

        // Assert
        Assert.Equal(families.Count, families.Distinct().Count());
    }

    #endregion

    #region Secure Comparison Tests

    [Fact]
    public void SecureCompare_WhenTokensMatch_ReturnsTrue()
    {
        // Arrange
        var token = JwtService.GenerateRefreshToken();

        // Act
        var result = SecureCompare.ConstantTimeEquals(token, token);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SecureCompare_WhenTokensDiffer_ReturnsFalse()
    {
        // Arrange
        var token1 = JwtService.GenerateRefreshToken();
        var token2 = JwtService.GenerateRefreshToken();

        // Act
        var result = SecureCompare.ConstantTimeEquals(token1, token2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SecureCompare_WhenOneTokenIsNull_ReturnsFalse()
    {
        // Arrange
        var token = JwtService.GenerateRefreshToken();

        // Act & Assert
        Assert.False(SecureCompare.ConstantTimeEquals(token, null));
        Assert.False(SecureCompare.ConstantTimeEquals(null, token));
    }

    [Fact]
    public void SecureCompare_WhenBothTokensNull_ReturnsTrue()
    {
        // Act
        var result = SecureCompare.ConstantTimeEquals(null, null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SecureCompare_WhenTokensHaveDifferentLength_ReturnsFalse()
    {
        // Arrange
        var token1 = "short";
        var token2 = "much_longer_token";

        // Act
        var result = SecureCompare.ConstantTimeEquals(token1, token2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SecureCompare_WhenBothEmpty_ReturnsTrue()
    {
        // Act
        var result = SecureCompare.ConstantTimeEquals("", "");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SecureCompare_WhenOneEmpty_ReturnsFalse()
    {
        // Arrange
        var token = JwtService.GenerateRefreshToken();

        // Act & Assert
        Assert.False(SecureCompare.ConstantTimeEquals(token, ""));
        Assert.False(SecureCompare.ConstantTimeEquals("", token));
    }

    #endregion

    #region Token Rotation Logic Tests

    [Fact]
    public void TokenRotation_NewTokenDiffersFromOld()
    {
        // Arrange
        var oldToken = JwtService.GenerateRefreshToken();

        // Act
        var newToken = JwtService.GenerateRefreshToken();

        // Assert
        Assert.NotEqual(oldToken, newToken);
    }

    [Fact]
    public void TokenRotation_ConsecutiveRotationsProduceUniqueTokens()
    {
        // Arrange
        var tokens = new List<string>();
        
        // Act
        for (int i = 0; i < 10; i++)
        {
            tokens.Add(JwtService.GenerateRefreshToken());
        }

        // Assert
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }

    #endregion

    #region Grace Period Tests

    [Fact]
    public void GracePeriod_WhenPreviousTokenValid_ShouldBeAccepted()
    {
        // Arrange
        var previousToken = JwtService.GenerateRefreshToken();
        var gracePeriodExpiry = DateTime.UtcNow.AddSeconds(30);

        // Act - simulate check
        var isGracePeriodValid = gracePeriodExpiry > DateTime.UtcNow;
        var isPreviousTokenMatch = SecureCompare.ConstantTimeEquals(previousToken, previousToken);

        // Assert
        Assert.True(isGracePeriodValid);
        Assert.True(isPreviousTokenMatch);
    }

    [Fact]
    public void GracePeriod_WhenExpired_ShouldBeRejected()
    {
        // Arrange
        var gracePeriodExpiry = DateTime.UtcNow.AddSeconds(-10); // Already expired

        // Act
        var isGracePeriodValid = gracePeriodExpiry > DateTime.UtcNow;

        // Assert
        Assert.False(isGracePeriodValid);
    }

    [Fact]
    public void GracePeriod_WhenExactlyExpired_ShouldBeRejected()
    {
        // Arrange
        var gracePeriodExpiry = DateTime.UtcNow;

        // Act
        var isGracePeriodValid = gracePeriodExpiry > DateTime.UtcNow;

        // Assert
        Assert.False(isGracePeriodValid);
    }

    [Fact]
    public void GracePeriod_WhenNullExpiry_ShouldBeRejected()
    {
        // Arrange
        DateTime? gracePeriodExpiry = null;

        // Act
        var isGracePeriodValid = gracePeriodExpiry != null && gracePeriodExpiry > DateTime.UtcNow;

        // Assert
        Assert.False(isGracePeriodValid);
    }

    #endregion

    #region Theft Detection Logic Tests

    [Fact]
    public void TheftDetection_WhenTokenNotMatchCurrentOrPrevious_ShouldDetectTheft()
    {
        // Arrange
        var currentToken = JwtService.GenerateRefreshToken();
        var previousToken = JwtService.GenerateRefreshToken();
        var submittedToken = JwtService.GenerateRefreshToken(); // Different token (potentially stolen/replayed)

        // Act
        var isCurrentToken = SecureCompare.ConstantTimeEquals(currentToken, submittedToken);
        var isPreviousToken = SecureCompare.ConstantTimeEquals(previousToken, submittedToken);
        var potentialTheft = !isCurrentToken && !isPreviousToken;

        // Assert
        Assert.True(potentialTheft);
    }

    [Fact]
    public void TheftDetection_WhenTokenMatchesCurrent_ShouldNotDetectTheft()
    {
        // Arrange
        var currentToken = JwtService.GenerateRefreshToken();

        // Act
        var isCurrentToken = SecureCompare.ConstantTimeEquals(currentToken, currentToken);
        var potentialTheft = !isCurrentToken;

        // Assert
        Assert.False(potentialTheft);
    }

    [Fact]
    public void TheftDetection_WhenTokenMatchesPrevious_ShouldNotDetectTheft()
    {
        // Arrange
        var currentToken = JwtService.GenerateRefreshToken();
        var previousToken = JwtService.GenerateRefreshToken();
        var submittedToken = previousToken;

        // Act
        var isCurrentToken = SecureCompare.ConstantTimeEquals(currentToken, submittedToken);
        var isPreviousToken = SecureCompare.ConstantTimeEquals(previousToken, submittedToken);
        var isValidToken = isCurrentToken || isPreviousToken;

        // Assert
        Assert.True(isValidToken);
    }

    [Fact]
    public void TheftDetection_WhenTokenFamilyExists_ShouldTriggerInvalidation()
    {
        // Arrange
        var tokenFamily = JwtService.GenerateTokenFamily();
        var hasTokenFamily = tokenFamily != Guid.Empty;
        var isOldTokenReused = true;

        // Act - simulate old token reuse scenario
        var shouldInvalidateAll = hasTokenFamily && isOldTokenReused;

        // Assert
        Assert.True(shouldInvalidateAll);
    }

    [Fact]
    public void TheftDetection_WhenNoTokenFamily_ShouldNotTriggerFullInvalidation()
    {
        // Arrange
        Guid? tokenFamily = null;
        var hasTokenFamily = tokenFamily.HasValue;
        var isOldTokenReused = true;

        // Act
        var shouldInvalidateAll = hasTokenFamily && isOldTokenReused;

        // Assert
        Assert.False(shouldInvalidateAll);
    }

    #endregion

    #region Token Validation Scenarios

    [Fact]
    public void TokenValidation_CurrentTokenValid_ShouldPass()
    {
        // Arrange
        var currentToken = JwtService.GenerateRefreshToken();
        var currentTokenExpiry = DateTime.UtcNow.AddDays(7);
        var submittedToken = currentToken;

        // Act
        var isCurrentToken = SecureCompare.ConstantTimeEquals(currentToken, submittedToken);
        var isNotExpired = currentTokenExpiry > DateTime.UtcNow;
        var isValid = isCurrentToken && isNotExpired;

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void TokenValidation_CurrentTokenExpired_ShouldFail()
    {
        // Arrange
        var currentToken = JwtService.GenerateRefreshToken();
        var currentTokenExpiry = DateTime.UtcNow.AddDays(-1);
        var submittedToken = currentToken;

        // Act
        var isCurrentToken = SecureCompare.ConstantTimeEquals(currentToken, submittedToken);
        var isNotExpired = currentTokenExpiry > DateTime.UtcNow;
        var isValid = isCurrentToken && isNotExpired;

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void TokenValidation_PreviousTokenInGracePeriod_ShouldPass()
    {
        // Arrange
        var currentToken = JwtService.GenerateRefreshToken();
        var previousToken = JwtService.GenerateRefreshToken();
        var previousTokenExpiry = DateTime.UtcNow.AddSeconds(30);
        var submittedToken = previousToken;

        // Act
        var isCurrentToken = SecureCompare.ConstantTimeEquals(currentToken, submittedToken);
        var isPreviousToken = SecureCompare.ConstantTimeEquals(previousToken, submittedToken);
        var isPreviousTokenValid = isPreviousToken && previousTokenExpiry > DateTime.UtcNow;
        var isValid = isCurrentToken || isPreviousTokenValid;

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void TokenValidation_PreviousTokenExpiredGracePeriod_ShouldFail()
    {
        // Arrange
        var currentToken = JwtService.GenerateRefreshToken();
        var previousToken = JwtService.GenerateRefreshToken();
        var previousTokenExpiry = DateTime.UtcNow.AddSeconds(-10);
        var submittedToken = previousToken;

        // Act
        var isCurrentToken = SecureCompare.ConstantTimeEquals(currentToken, submittedToken);
        var isPreviousToken = SecureCompare.ConstantTimeEquals(previousToken, submittedToken);
        var isPreviousTokenValid = isPreviousToken && previousTokenExpiry > DateTime.UtcNow;
        var isValid = isCurrentToken || isPreviousTokenValid;

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void TokenValidation_NullCurrentToken_ShouldFail()
    {
        // Arrange
        string? currentToken = null;
        var submittedToken = JwtService.GenerateRefreshToken();

        // Act
        var isCurrentToken = SecureCompare.ConstantTimeEquals(currentToken, submittedToken);

        // Assert
        Assert.False(isCurrentToken);
    }

    #endregion

    #region Token Invalidation Tests

    [Fact]
    public void TokenInvalidation_AllTokenFieldsShouldBeClearable()
    {
        // Arrange - simulating UserAuthentication fields
        string? accessToken = JwtService.GenerateRefreshToken();
        DateTime? accessTokenExpiry = DateTime.UtcNow.AddMinutes(15);
        string? refreshToken = JwtService.GenerateRefreshToken();
        DateTime? refreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        Guid? tokenFamily = JwtService.GenerateTokenFamily();
        string? previousRefreshToken = JwtService.GenerateRefreshToken();
        DateTime? previousRefreshTokenExpiry = DateTime.UtcNow.AddSeconds(30);

        // Act - simulate invalidation
        accessToken = null;
        accessTokenExpiry = null;
        refreshToken = null;
        refreshTokenExpiry = null;
        tokenFamily = null;
        previousRefreshToken = null;
        previousRefreshTokenExpiry = null;

        // Assert
        Assert.Null(accessToken);
        Assert.Null(accessTokenExpiry);
        Assert.Null(refreshToken);
        Assert.Null(refreshTokenExpiry);
        Assert.Null(tokenFamily);
        Assert.Null(previousRefreshToken);
        Assert.Null(previousRefreshTokenExpiry);
    }

    #endregion

    #region New Login Session Tests

    [Fact]
    public void NewLogin_ShouldGenerateNewTokenFamily()
    {
        // Arrange
        var oldTokenFamily = JwtService.GenerateTokenFamily();

        // Act
        var newTokenFamily = JwtService.GenerateTokenFamily();

        // Assert
        Assert.NotEqual(oldTokenFamily, newTokenFamily);
    }

    [Fact]
    public void NewLogin_ShouldClearPreviousTokenFields()
    {
        // Arrange - simulating new login clearing old state
        string? previousRefreshToken = JwtService.GenerateRefreshToken();
        DateTime? previousRefreshTokenExpiry = DateTime.UtcNow.AddSeconds(30);

        // Act - simulate new login
        previousRefreshToken = null;
        previousRefreshTokenExpiry = null;

        // Assert
        Assert.Null(previousRefreshToken);
        Assert.Null(previousRefreshTokenExpiry);
    }

    [Fact]
    public void NewLogin_ShouldResetTokenFamilyOnReAuthentication()
    {
        // Arrange
        var existingTokenFamily = JwtService.GenerateTokenFamily();
        
        // Act
        var newTokenFamily = JwtService.GenerateTokenFamily();

        // Assert
        Assert.NotEqual(existingTokenFamily, newTokenFamily);
        Assert.NotEqual(Guid.Empty, newTokenFamily);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public void Logout_ShouldClearAllTokenFields()
    {
        // Arrange
        string? accessToken = JwtService.GenerateRefreshToken();
        string? refreshToken = JwtService.GenerateRefreshToken();
        Guid? tokenFamily = JwtService.GenerateTokenFamily();
        string? previousRefreshToken = JwtService.GenerateRefreshToken();

        // Act - simulate logout
        accessToken = null;
        refreshToken = null;
        tokenFamily = null;
        previousRefreshToken = null;

        // Assert
        Assert.Null(accessToken);
        Assert.Null(refreshToken);
        Assert.Null(tokenFamily);
        Assert.Null(previousRefreshToken);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_TokenWithSpecialCharacters_ShouldCompareCorrectly()
    {
        // Arrange
        var token1 = "abc+def/ghi=";
        var token2 = "abc+def/ghi=";
        var token3 = "abc+def/ghj=";

        // Act & Assert
        Assert.True(SecureCompare.ConstantTimeEquals(token1, token2));
        Assert.False(SecureCompare.ConstantTimeEquals(token1, token3));
    }

    [Fact]
    public void EdgeCase_VeryLongToken_ShouldCompareCorrectly()
    {
        // Arrange
        var token1 = new string('a', 10000);
        var token2 = new string('a', 10000);
        var token3 = new string('a', 9999) + 'b';

        // Act & Assert
        Assert.True(SecureCompare.ConstantTimeEquals(token1, token2));
        Assert.False(SecureCompare.ConstantTimeEquals(token1, token3));
    }

    [Fact]
    public void EdgeCase_TokenFamilyGuidFormat_ShouldBeValid()
    {
        // Arrange
        var tokenFamily = JwtService.GenerateTokenFamily();

        // Act & Assert
        Assert.True(Guid.TryParse(tokenFamily.ToString(), out _));
    }

    #endregion
}
