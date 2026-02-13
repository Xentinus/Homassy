using Homassy.API.Context;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Homassy.API.Models.Kratos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;

namespace Homassy.Migrator.Migrations
{
    /// <summary>
    /// Handles migration of existing users to Kratos identity system.
    /// </summary>
    public class KratosUserMigration
    {
        private readonly HomassyDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string _kratosAdminUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        public KratosUserMigration(HomassyDbContext context, IConfiguration configuration)
        {
            _context = context;
            _httpClient = new HttpClient();
            _kratosAdminUrl = configuration["Kratos:AdminUrl"] ?? "http://localhost:4434";

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
        }

        /// <summary>
        /// Migration result for a single user.
        /// </summary>
        public class MigrationResult
        {
            public int UserId { get; set; }
            public string Email { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string? KratosIdentityId { get; set; }
            public string? ErrorMessage { get; set; }
            public bool AlreadyMigrated { get; set; }
        }

        /// <summary>
        /// Summary of the migration operation.
        /// </summary>
        public class MigrationSummary
        {
            public int TotalUsers { get; set; }
            public int SuccessCount { get; set; }
            public int FailedCount { get; set; }
            public int AlreadyMigratedCount { get; set; }
            public int SkippedCount { get; set; }
            public List<MigrationResult> Results { get; set; } = new();
            public TimeSpan Duration { get; set; }
        }

        /// <summary>
        /// Migrates all users without KratosIdentityId to Kratos.
        /// </summary>
        /// <param name="dryRun">If true, only simulates the migration without making changes.</param>
        /// <param name="batchSize">Number of users to process per batch.</param>
        /// <returns>Summary of the migration operation.</returns>
        public async Task<MigrationSummary> MigrateAllUsersAsync(bool dryRun = false, int batchSize = 50)
        {
            var startTime = DateTime.UtcNow;
            var summary = new MigrationSummary();

            Console.WriteLine($"=== Kratos User Migration {(dryRun ? "(DRY RUN)" : "")} ===");
            Console.WriteLine($"Kratos Admin URL: {_kratosAdminUrl}");

            // Check Kratos connectivity
            if (!await CheckKratosConnectivityAsync())
            {
                Console.WriteLine("ERROR: Cannot connect to Kratos Admin API");
                return summary;
            }

            // Get users who need migration (no KratosIdentityId set)
            var usersToMigrate = await _context.Users
                .Include(u => u.Profile)
                .Where(u => u.KratosIdentityId == null)
                .OrderBy(u => u.Id)
                .ToListAsync();

            summary.TotalUsers = usersToMigrate.Count;
            Console.WriteLine($"Found {summary.TotalUsers} users to migrate");

            if (summary.TotalUsers == 0)
            {
                Console.WriteLine("No users need migration.");
                summary.Duration = DateTime.UtcNow - startTime;
                return summary;
            }

            // Process in batches
            var batches = usersToMigrate
                .Select((user, index) => new { user, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.user).ToList())
                .ToList();

            var batchNumber = 0;
            foreach (var batch in batches)
            {
                batchNumber++;
                Console.WriteLine($"\nProcessing batch {batchNumber}/{batches.Count} ({batch.Count} users)...");

                foreach (var user in batch)
                {
                    var result = await MigrateUserAsync(user, dryRun);
                    summary.Results.Add(result);

                    if (result.AlreadyMigrated)
                    {
                        summary.AlreadyMigratedCount++;
                    }
                    else if (result.Success)
                    {
                        summary.SuccessCount++;
                    }
                    else
                    {
                        summary.FailedCount++;
                    }

                    // Log progress
                    var status = result.Success ? "✓" : (result.AlreadyMigrated ? "⊙" : "✗");
                    Console.WriteLine($"  {status} User {user.Id} ({user.Email}): {(result.Success ? "Migrated" : (result.AlreadyMigrated ? "Already exists" : result.ErrorMessage))}");
                }

                // Save changes after each batch (if not dry run)
                if (!dryRun)
                {
                    await _context.SaveChangesAsync();
                }
            }

            summary.Duration = DateTime.UtcNow - startTime;

            // Print summary
            Console.WriteLine("\n=== Migration Summary ===");
            Console.WriteLine($"Total Users: {summary.TotalUsers}");
            Console.WriteLine($"Successfully Migrated: {summary.SuccessCount}");
            Console.WriteLine($"Already in Kratos: {summary.AlreadyMigratedCount}");
            Console.WriteLine($"Failed: {summary.FailedCount}");
            Console.WriteLine($"Duration: {summary.Duration.TotalSeconds:F2} seconds");

            if (dryRun)
            {
                Console.WriteLine("\n⚠️  This was a DRY RUN. No changes were made.");
            }

            return summary;
        }

        /// <summary>
        /// Migrates a single user to Kratos.
        /// </summary>
        private async Task<MigrationResult> MigrateUserAsync(User user, bool dryRun)
        {
            var result = new MigrationResult
            {
                UserId = user.Id,
                Email = user.Email
            };

            try
            {
                // Check if identity already exists in Kratos by email
                var existingIdentity = await GetIdentityByEmailAsync(user.Email);
                if (existingIdentity != null)
                {
                    result.AlreadyMigrated = true;
                    result.KratosIdentityId = existingIdentity.Id;
                    
                    // Update the local user with the existing Kratos ID (if not dry run)
                    if (!dryRun)
                    {
                        user.KratosIdentityId = existingIdentity.Id;
                    }
                    
                    result.Success = true;
                    return result;
                }

                if (dryRun)
                {
                    // In dry run, just report what would happen
                    result.Success = true;
                    result.KratosIdentityId = "(would be generated)";
                    return result;
                }

                // Build Kratos traits from user data
                var traits = BuildTraitsFromUser(user);

                // Create identity in Kratos
                var identity = await CreateIdentityAsync(traits);
                if (identity == null)
                {
                    result.ErrorMessage = "Failed to create Kratos identity";
                    return result;
                }

                // Update local user with Kratos ID
                user.KratosIdentityId = identity.Id;
                result.KratosIdentityId = identity.Id;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Builds KratosTraits from User and UserProfile entities.
        /// </summary>
        private KratosTraits BuildTraitsFromUser(User user)
        {
            var traits = new KratosTraits
            {
                Email = user.Email,
                Name = user.Name,
                FamilyId = user.FamilyId
            };

            if (user.Profile != null)
            {
                traits.DisplayName = user.Profile.DisplayName;
                traits.ProfilePictureBase64 = user.Profile.ProfilePictureBase64;
                traits.DateOfBirth = user.Profile.DateOfBirth?.ToString("yyyy-MM-dd");
                traits.Gender = user.Profile.Gender;
                traits.DefaultCurrency = MapCurrencyToString(user.Profile.DefaultCurrency);
                traits.DefaultTimezone = MapTimezoneToString(user.Profile.DefaultTimeZone);
                traits.DefaultLanguage = MapLanguageToString(user.Profile.DefaultLanguage);
            }

            return traits;
        }

        /// <summary>
        /// Maps Currency enum to Kratos-compatible string.
        /// </summary>
        private static string MapCurrencyToString(Currency currency)
        {
            return currency switch
            {
                Currency.Huf => "HUF",
                Currency.Eur => "EUR",
                Currency.Usd => "USD",
                Currency.Gbp => "GBP",
                Currency.Chf => "CHF",
                Currency.Pln => "PLN",
                Currency.Czk => "CZK",
                Currency.Ron => "RON",
                _ => "HUF" // Default
            };
        }

        /// <summary>
        /// Maps UserTimeZone enum to Kratos-compatible string.
        /// </summary>
        private static string MapTimezoneToString(UserTimeZone timezone)
        {
            return timezone switch
            {
                UserTimeZone.CentralEuropeStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.GreenwichStandardTime => "GMTStandardTime",
                UserTimeZone.EasternStandardTime => "EasternStandardTime",
                UserTimeZone.PacificStandardTime => "PacificStandardTime",
                UserTimeZone.TokyoStandardTime => "TokyoStandardTime",
                _ => "CentralEuropeStandardTime" // Default
            };
        }

        /// <summary>
        /// Maps Language enum to Kratos-compatible string.
        /// </summary>
        private static string MapLanguageToString(Language language)
        {
            return language switch
            {
                Language.Hungarian => "hu",
                Language.German => "de",
                Language.English => "en",
                _ => "hu" // Default
            };
        }

        /// <summary>
        /// Checks if Kratos Admin API is accessible.
        /// </summary>
        private async Task<bool> CheckKratosConnectivityAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_kratosAdminUrl}/health/alive");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets an identity by email from Kratos.
        /// </summary>
        private async Task<KratosIdentity?> GetIdentityByEmailAsync(string email)
        {
            try
            {
                var encodedEmail = Uri.EscapeDataString(email);
                var response = await _httpClient.GetAsync($"{_kratosAdminUrl}/admin/identities?credentials_identifier={encodedEmail}");
                
                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var identities = JsonSerializer.Deserialize<List<KratosIdentity>>(content, _jsonOptions);
                
                return identities?.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a new identity in Kratos.
        /// </summary>
        private async Task<KratosIdentity?> CreateIdentityAsync(KratosTraits traits)
        {
            try
            {
                var createPayload = new
                {
                    schema_id = "default",
                    state = "active",
                    traits,
                    verifiable_addresses = new[]
                    {
                        new
                        {
                            value = traits.Email,
                            via = "email",
                            verified = true, // Mark as verified for migrated users
                            status = "completed"
                        }
                    }
                };

                var json = JsonSerializer.Serialize(createPayload, _jsonOptions);
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_kratosAdminUrl}/admin/identities", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"    ERROR creating identity: {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<KratosIdentity>(responseContent, _jsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    EXCEPTION creating identity: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verifies that all users have been migrated successfully.
        /// </summary>
        public async Task<bool> VerifyMigrationAsync()
        {
            Console.WriteLine("\n=== Verifying Migration ===");

            var usersWithoutKratosId = await _context.Users
                .Where(u => u.KratosIdentityId == null)
                .CountAsync();

            var totalUsers = await _context.Users.CountAsync();
            var migratedUsers = totalUsers - usersWithoutKratosId;

            Console.WriteLine($"Total Users: {totalUsers}");
            Console.WriteLine($"Migrated Users: {migratedUsers}");
            Console.WriteLine($"Not Migrated: {usersWithoutKratosId}");

            if (usersWithoutKratosId > 0)
            {
                Console.WriteLine("\n⚠️  Some users are not migrated:");
                var unmigrated = await _context.Users
                    .Where(u => u.KratosIdentityId == null)
                    .Select(u => new { u.Id, u.Email })
                    .Take(10)
                    .ToListAsync();

                foreach (var u in unmigrated)
                {
                    Console.WriteLine($"  - User {u.Id}: {u.Email}");
                }

                if (usersWithoutKratosId > 10)
                {
                    Console.WriteLine($"  ... and {usersWithoutKratosId - 10} more");
                }
            }
            else
            {
                Console.WriteLine("\n✓ All users have been migrated to Kratos!");
            }

            return usersWithoutKratosId == 0;
        }

        /// <summary>
        /// Gets statistics about the migration state.
        /// </summary>
        public async Task<(int total, int migrated, int pending)> GetMigrationStatsAsync()
        {
            var total = await _context.Users.CountAsync();
            var migrated = await _context.Users.Where(u => u.KratosIdentityId != null).CountAsync();
            return (total, migrated, total - migrated);
        }
    }
}
