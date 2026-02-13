using Homassy.API.Models.Kratos;
using Serilog;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Homassy.API.Services
{
    /// <summary>
    /// Service for interacting with Ory Kratos identity provider.
    /// </summary>
    public interface IKratosService
    {
        /// <summary>
        /// Validates a session using cookie or bearer token.
        /// </summary>
        Task<KratosSession?> GetSessionAsync(string? cookie, string? sessionToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets an identity by ID from the Admin API.
        /// </summary>
        Task<KratosIdentity?> GetIdentityAsync(string identityId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets an identity by email from the Admin API.
        /// </summary>
        Task<KratosIdentity?> GetIdentityByEmailAsync(string email, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new identity with the given traits.
        /// </summary>
        Task<KratosIdentity?> CreateIdentityAsync(KratosTraits traits, bool verifyEmail = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an identity's traits.
        /// </summary>
        Task<KratosIdentity?> UpdateIdentityTraitsAsync(string identityId, KratosTraits traits, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes all sessions for an identity.
        /// </summary>
        Task<bool> DeleteIdentitySessionsAsync(string identityId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all identities (paginated).
        /// </summary>
        Task<List<KratosIdentity>> GetIdentitiesAsync(int page = 0, int perPage = 100, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Implementation of IKratosService for Ory Kratos interactions.
    /// </summary>
    public class KratosService : IKratosService
    {
        private readonly HttpClient _httpClient;
        private readonly string _publicUrl;
        private readonly string _adminUrl;
        private readonly string _sessionCookieName;
        private readonly JsonSerializerOptions _jsonOptions;

        public const string SESSION_COOKIE_NAME = "ory_kratos_session";

        public KratosService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _publicUrl = configuration["Kratos:PublicUrl"] ?? "http://localhost:4433";
            _adminUrl = configuration["Kratos:AdminUrl"] ?? "http://localhost:4434";
            _sessionCookieName = configuration["Kratos:SessionCookieName"] ?? SESSION_COOKIE_NAME;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <inheritdoc/>
        public async Task<KratosSession?> GetSessionAsync(string? cookie, string? sessionToken, CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = $"{_publicUrl}/sessions/whoami";
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                // Add session cookie or X-Session-Token header
                if (!string.IsNullOrEmpty(sessionToken))
                {
                    request.Headers.Add("X-Session-Token", sessionToken);
                }
                else if (!string.IsNullOrEmpty(cookie))
                {
                    request.Headers.Add("Cookie", cookie);
                }
                else
                {
                    return null;
                }

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Log.Debug("Kratos session validation returned 401 - session invalid or expired");
                        return null;
                    }

                    Log.Warning($"Kratos session validation failed with status {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var session = JsonSerializer.Deserialize<KratosSession>(content, _jsonOptions);

                if (session == null || !session.Active)
                {
                    Log.Debug("Kratos session is null or inactive");
                    return null;
                }

                return session;
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "Failed to connect to Kratos for session validation");
                return null;
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "Failed to deserialize Kratos session response");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during Kratos session validation");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<KratosIdentity?> GetIdentityAsync(string identityId, CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = $"{_adminUrl}/admin/identities/{identityId}";
                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Log.Debug($"Kratos identity {identityId} not found");
                        return null;
                    }

                    Log.Warning($"Kratos get identity failed with status {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<KratosIdentity>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error getting Kratos identity {identityId}");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<KratosIdentity?> GetIdentityByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            try
            {
                // Use the credentials_identifier filter to find by email
                var encodedEmail = Uri.EscapeDataString(email);
                var requestUrl = $"{_adminUrl}/admin/identities?credentials_identifier={encodedEmail}";
                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning($"Kratos get identity by email failed with status {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var identities = JsonSerializer.Deserialize<List<KratosIdentity>>(content, _jsonOptions);
                
                return identities?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error getting Kratos identity by email {email}");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<KratosIdentity?> CreateIdentityAsync(KratosTraits traits, bool verifyEmail = true, CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = $"{_adminUrl}/admin/identities";
                
                var createPayload = new
                {
                    schema_id = "default",
                    state = "active",
                    traits,
                    verifiable_addresses = verifyEmail ? new[]
                    {
                        new
                        {
                            value = traits.Email,
                            via = "email",
                            verified = true, // Mark as verified for migrated users
                            status = "completed"
                        }
                    } : null
                };

                var json = JsonSerializer.Serialize(createPayload, _jsonOptions);
                Log.Debug($"Creating Kratos identity with payload: {json}");
                
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    Log.Warning($"Kratos create identity failed with status {response.StatusCode}: {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Log.Debug($"Kratos identity created successfully for {traits.Email}");
                return JsonSerializer.Deserialize<KratosIdentity>(responseContent, _jsonOptions);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error creating Kratos identity for email {traits.Email}");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<KratosIdentity?> UpdateIdentityTraitsAsync(string identityId, KratosTraits traits, CancellationToken cancellationToken = default)
        {
            try
            {
                // First, get the current identity
                var identity = await GetIdentityAsync(identityId, cancellationToken);
                if (identity == null)
                {
                    return null;
                }

                var requestUrl = $"{_adminUrl}/admin/identities/{identityId}";
                
                var updatePayload = new
                {
                    schema_id = identity.SchemaId,
                    state = identity.State,
                    traits = traits
                };

                var json = JsonSerializer.Serialize(updatePayload, _jsonOptions);
                Log.Debug($"Updating Kratos identity {identityId} with payload: {json}");
                
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(requestUrl, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    Log.Warning($"Kratos update identity failed with status {response.StatusCode}: {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Log.Debug($"Kratos identity {identityId} updated successfully");
                return JsonSerializer.Deserialize<KratosIdentity>(responseContent, _jsonOptions);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error updating Kratos identity {identityId}");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteIdentitySessionsAsync(string identityId, CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = $"{_adminUrl}/admin/identities/{identityId}/sessions";
                var response = await _httpClient.DeleteAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning($"Kratos delete sessions failed with status {response.StatusCode}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error deleting Kratos sessions for identity {identityId}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<KratosIdentity>> GetIdentitiesAsync(int page = 0, int perPage = 100, CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = $"{_adminUrl}/admin/identities?page={page}&per_page={perPage}";
                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning($"Kratos get identities failed with status {response.StatusCode}");
                    return new List<KratosIdentity>();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<List<KratosIdentity>>(content, _jsonOptions) ?? new List<KratosIdentity>();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting Kratos identities");
                return new List<KratosIdentity>();
            }
        }

        /// <summary>
        /// Extracts the session cookie value from a full Cookie header.
        /// </summary>
        public static string? ExtractSessionCookie(string? cookieHeader)
        {
            if (string.IsNullOrEmpty(cookieHeader))
            {
                return null;
            }

            var cookies = cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var cookie in cookies)
            {
                if (cookie.StartsWith($"{SESSION_COOKIE_NAME}=", StringComparison.OrdinalIgnoreCase))
                {
                    return cookie;
                }
            }

            return null;
        }
    }
}
