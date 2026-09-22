using System.Text;
using System.Text.Json;

namespace Homassy.Migrator.Migrations
{
    /// <summary>What a seeding run did, so the caller can report it without re-deriving anything.</summary>
    public enum DevSeedOutcome
    {
        /// <summary>Not Development — nothing was attempted.</summary>
        NotDevelopment,

        /// <summary>The identity already existed; nothing was written.</summary>
        AlreadyExists,

        /// <summary>A fresh identity was created in Kratos.</summary>
        Created,

        /// <summary>Kratos could not be reached or refused the identity. Never fatal — see the class remarks.</summary>
        Failed
    }

    /// <summary>
    /// Creates one fixed Kratos identity so a developer can log in to a freshly started stack without
    /// registering an account by hand every time the volumes are wiped.
    /// </summary>
    /// <remarks>
    /// <b>Development only, twice over.</b> The environment is checked here (<see cref="IsDevelopmentEnvironment"/>,
    /// which treats an <em>unset</em> environment as production — the migrator is given no environment
    /// variable at all in <c>docker-compose.production.yml</c>), and the variables that switch it on live in
    /// <c>docker-compose.override.yml</c>, the dev-only file the production compose invocation never merges.
    /// Neither gate depends on the other: removing one still leaves the seed off in production.
    /// <para>
    /// <b>It seeds an identity, not a password.</b> This app logs in with a one-time code that Kratos
    /// generates and Kratos alone validates; there is no configuration that pins it to a fixed value, and
    /// inventing one would mean a second authentication path that only exists in development — exactly the
    /// kind of thing that survives into production. So the login is the real login, and the only concession
    /// to development is that <c>Homassy.Email</c> writes the code it was asked to send into its own log
    /// (see <c>KratosWebhookEndpoint</c>): <c>docker logs homassy-email</c> is the dev inbox.
    /// </para>
    /// <para>
    /// <b>It never fails the migration.</b> The migrator gates the API's startup, and a dev convenience that
    /// can stop the whole stack from coming up is worse than no dev convenience. Every failure is reported
    /// and swallowed.
    /// </para>
    /// <para>
    /// No local <c>User</c> row is written: the API creates one lazily on the first authenticated request
    /// (<c>AuthController.EnsureLocalUserAsync</c>), so seeding one here would be a second, divergent copy
    /// of that logic for no gain.
    /// </para>
    /// </remarks>
    public sealed class DevUserSeeder
    {
        /// <summary>The seeded developer's address. Deliberately a `.local` domain: it can never receive real mail.</summary>
        public const string DefaultEmail = "dev@homassy.local";

        private const string DefaultName = "Dev User";
        private const string DefaultDisplayName = "Dev";

        private readonly HttpClient _httpClient;
        private readonly string _kratosAdminUrl;
        private readonly string _email;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public DevUserSeeder(string kratosAdminUrl, string? email = null, HttpClient? httpClient = null)
        {
            _kratosAdminUrl = kratosAdminUrl.TrimEnd('/');
            _email = string.IsNullOrWhiteSpace(email) ? DefaultEmail : email.Trim();
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        /// <summary>The address the seeded identity is reachable at — what the developer types on the login screen.</summary>
        public string Email => _email;

        /// <summary>
        /// Whether this environment may be seeded. Anything other than an explicit "Development" is a no:
        /// an unset variable is the production case, not an unknown one.
        /// </summary>
        public static bool IsDevelopmentEnvironment(string? environmentName)
            => string.Equals(environmentName?.Trim(), "Development", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the identity if it is not there yet. Waits for Kratos first: the migrator and Kratos both
        /// start off the same healthy database, so on a cold <c>docker compose up</c> this can easily run
        /// before Kratos has finished its own migrations.
        /// </summary>
        public async Task<DevSeedOutcome> RunAsync(CancellationToken cancellationToken = default)
        {
            if (!await WaitForKratosAsync(cancellationToken))
            {
                Console.WriteLine($"  WARNING: Kratos at {_kratosAdminUrl} did not become available; dev user not seeded");
                return DevSeedOutcome.Failed;
            }

            var existing = await FindIdentityIdAsync(cancellationToken);
            if (existing != null)
            {
                Console.WriteLine($"  Dev user already present: {_email} (identity {existing})");
                return DevSeedOutcome.AlreadyExists;
            }

            var created = await CreateIdentityAsync(cancellationToken);
            if (created == null)
            {
                return DevSeedOutcome.Failed;
            }

            Console.WriteLine($"  Dev user created: {_email} (identity {created})");
            return DevSeedOutcome.Created;
        }

        private async Task<bool> WaitForKratosAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= 30; attempt++)
            {
                try
                {
                    using var response = await _httpClient.GetAsync($"{_kratosAdminUrl}/health/alive", cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    // Not up yet. The loop is the wait; the exception carries nothing a developer needs
                    // until every attempt has failed, which is reported by the caller.
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }

            return false;
        }

        /// <summary>The identity id registered for this address, or null when Kratos does not know it.</summary>
        private async Task<string?> FindIdentityIdAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _httpClient.GetAsync(
                    $"{_kratosAdminUrl}/admin/identities?credentials_identifier={Uri.EscapeDataString(_email)}",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(body);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                foreach (var identity in document.RootElement.EnumerateArray())
                {
                    if (identity.TryGetProperty("id", out var id))
                    {
                        return id.GetString();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  WARNING: could not query Kratos for the dev user: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> CreateIdentityAsync(CancellationToken cancellationToken)
        {
            try
            {
                // The address is marked verified: a developer who has to complete an email verification
                // round trip before the seeded account is usable has been given a chore, not a shortcut.
                var payload = new
                {
                    schema_id = "default",
                    state = "active",
                    traits = new
                    {
                        email = _email,
                        name = DefaultName,
                        display_name = DefaultDisplayName,
                        default_language = "hu"
                    },
                    verifiable_addresses = new[]
                    {
                        new
                        {
                            value = _email,
                            via = "email",
                            verified = true,
                            status = "completed"
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload, _jsonOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync($"{_kratosAdminUrl}/admin/identities", content, cancellationToken);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"  WARNING: Kratos refused the dev identity ({(int)response.StatusCode}): {responseBody}");
                    return null;
                }

                using var document = JsonDocument.Parse(responseBody);
                return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  WARNING: could not create the dev identity: {ex.Message}");
                return null;
            }
        }
    }
}
