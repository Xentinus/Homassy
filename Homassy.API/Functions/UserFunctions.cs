using Homassy.API.Constants;
using Homassy.API.Context;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Models.Auth;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Homassy.API.Models.User;
using Homassy.API.Security;
using Homassy.API.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Homassy.API.Functions
{
    public class UserFunctions
    {
        private static readonly ConcurrentDictionary<int, User> _userCache = new();
        private static readonly ConcurrentDictionary<int, UserProfile> _userProfileCache = new();
        private static readonly ConcurrentDictionary<int, UserNotificationPreferences> _userNotificationPrefsCache = new();

        /// <summary>
        /// The curated identity-colour palette, mirroring <c>Homassy.Web/app/utils/memberColors.ts</c>.
        /// Kept as a literal set rather than an enum because the web palette is the source of truth and
        /// this is only here to reject values that would break the contrast guarantee.
        /// </summary>
        private static readonly HashSet<string> IdentityColorKeys = new(StringComparer.Ordinal)
        {
            "rose", "amber", "lime", "teal", "sky", "indigo", "violet", "fuchsia"
        };

        private const string IdentityColorAuto = "auto";

        /// <summary>
        /// Strict six-digit hex only: no three-digit shorthand, no alpha channel, no <c>rgb()</c>,
        /// no bare names. A single canonical stored form is what keeps the web client's parsing
        /// simple — see <c>memberColors.ts</c>'s <c>isHexColor</c>, which mirrors this pattern.
        /// </summary>
        private static readonly Regex IdentityColorHexPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        public static bool Inited = false;

        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;

        public UserFunctions(IDbContextFactory<HomassyDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        #region Cache Management
        public async Task InitializeCacheAsync(CancellationToken cancellationToken = default)
        {
            using var context = _contextFactory.CreateForReading();
            var users =  await context.Users
                .ToListAsync(cancellationToken);

            var profiles = await context.UserProfiles
                .ToListAsync(cancellationToken);

            var notificationPreferences = await context.UserNotificationPreferences
                .ToListAsync(cancellationToken);

            try
            {
                foreach (var user in users)
                {
                    _userCache[user.Id] = user;
                }

                foreach (var profile in profiles)
                {
                    _userProfileCache[profile.UserId] = profile;
                }

                foreach (var notificationPreference in notificationPreferences)
                {
                    _userNotificationPrefsCache[notificationPreference.UserId] = notificationPreference;
                }

                Inited = true;
                Log.Information($"Initialized user cache with {users.Count} users.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize user cache.");
                throw;
            }
        }

        public async Task RefreshUserCacheAsync(int userId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = _contextFactory.CreateForReading();
                var user = await context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

                var existsInCache = _userCache.ContainsKey(userId);

                if (user != null && existsInCache)
                {
                    _userCache[userId] = user;
                    Log.Debug($"Refreshed user {userId} in cache.");
                }
                else if (user != null && !existsInCache)
                {
                    _userCache[userId] = user;
                    Log.Debug($"Added user {userId} to cache.");
                }
                else if (user == null && existsInCache)
                {
                    _userCache.TryRemove(userId, out _);
                    Log.Debug($"Removed deleted user {userId} from cache.");
                }
                else
                {
                    Log.Debug($"User {userId} not found in DB or cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for user {userId}.");
                throw;
            }
        }

        public async Task RefreshUserProfileCacheAsync(int recordId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = _contextFactory.CreateForReading();
                var userProfile = await context.UserProfiles
                    .FirstOrDefaultAsync(u => u.Id == recordId, cancellationToken);

                if (userProfile != null)
                {
                    var existsInCache = _userProfileCache.ContainsKey(userProfile.UserId);

                    if (existsInCache)
                    {
                        _userProfileCache[userProfile.UserId] = userProfile;
                        Log.Debug($"Refreshed user profile {userProfile.UserId} in cache.");
                    }
                    else
                    {
                        _userProfileCache[userProfile.UserId] = userProfile;
                        Log.Debug($"Added user profile {userProfile.UserId} to cache.");
                    }
                }
                else
                {
                    var cacheEntry = _userProfileCache.FirstOrDefault(kvp => kvp.Value.Id == recordId);
                    if (cacheEntry.Value != null)
                    {
                        _userProfileCache.TryRemove(cacheEntry.Key, out _);
                        Log.Debug($"Removed deleted user profile record {recordId} (userId: {cacheEntry.Key}) from cache.");
                    }
                    else
                    {
                        Log.Debug($"User profile record {recordId} not found in DB or cache.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for user profile record {recordId}.");
                throw;
            }
        }

        public async Task RefreshUserNotificationCacheAsync(int recordId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = _contextFactory.CreateForReading();
                var userPrefs = await context.UserNotificationPreferences
                    .FirstOrDefaultAsync(u => u.Id == recordId, cancellationToken);

                if (userPrefs != null)
                {
                    var existsInCache = _userNotificationPrefsCache.ContainsKey(userPrefs.UserId);

                    if (existsInCache)
                    {
                        _userNotificationPrefsCache[userPrefs.UserId] = userPrefs;
                        Log.Debug($"Refreshed user notification preferences {userPrefs.UserId} in cache.");
                    }
                    else
                    {
                        _userNotificationPrefsCache[userPrefs.UserId] = userPrefs;
                        Log.Debug($"Added user notification preferences {userPrefs.UserId} to cache.");
                    }
                }
                else
                {
                    var cacheEntry = _userNotificationPrefsCache.FirstOrDefault(kvp => kvp.Value.Id == recordId);
                    if (cacheEntry.Value != null)
                    {
                        _userNotificationPrefsCache.TryRemove(cacheEntry.Key, out _);
                        Log.Debug($"Removed deleted user notification preferences record {recordId} (userId: {cacheEntry.Key}) from cache.");
                    }
                    else
                    {
                        Log.Debug($"User notification preferences record {recordId} not found in DB or cache.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for user notification preferences record {recordId}.");
                throw;
            }
        }

        public User? GetUserById(int? userId)
        {
            if (userId == null) return null;
            User? user = null;

            if (Inited)
            {
                _userCache.TryGetValue((int)userId, out user);
            }

            if (user == null)
            {
                using var context = _contextFactory.CreateForReading();
                user = context.Users
                    .FirstOrDefault(u => u.Id == userId);
            }

            return user;
        }

        public UserProfile? GetUserProfileByUserId(int? userId)
        {
            if (userId == null) return null;
            UserProfile? profile = null;

            if (Inited)
            {
                _userProfileCache.TryGetValue((int)userId, out profile);
            }

            if (profile == null)
            {
                using var context = _contextFactory.CreateForReading();
                profile = context.UserProfiles
                    .FirstOrDefault(p => p.UserId == userId);
            }

            return profile;
        }

        public UserNotificationPreferences? GetUserNotificationPreferencesByUserId(int? userId)
        {
            if (userId == null) return null;
            UserNotificationPreferences? prefs = null;

            if (Inited)
            {
                _userNotificationPrefsCache.TryGetValue((int)userId, out prefs);
            }

            if (prefs == null)
            {
                using var context = _contextFactory.CreateForReading();
                prefs = context.UserNotificationPreferences
                    .FirstOrDefault(n => n.UserId == userId);
            }

            return prefs;
        }

        public User? GetAllUserDataById(int? userId)
        {
            if (userId == null) return null;
            User? user = null;

            if (Inited)
            {
                _userCache.TryGetValue((int)userId, out user);

                if (user != null)
                {
                    _userProfileCache.TryGetValue((int)userId, out var profile);
                    _userNotificationPrefsCache.TryGetValue((int)userId, out var notificationPrefs);

                    user.Profile = profile;
                    user.NotificationPreferences = notificationPrefs;
                }
            }

            if (user == null)
            {
                using var context = _contextFactory.CreateForReading();
                user = context.Users
                    .Include(i => i.Profile)
                    .Include(i => i.NotificationPreferences)
                    .FirstOrDefault(u => u.Id == userId);
            }

            return user;
        }

        public User? GetAllUserDataByPublicId(Guid? publicId)
        {
            if (publicId == null) return null;
            User? user = null;

            if (Inited)
            {
                user = _userCache.Values.FirstOrDefault(u => u.PublicId == publicId);
                if (user != null)
                {
                    _userProfileCache.TryGetValue(user.Id, out var profile);
                    _userNotificationPrefsCache.TryGetValue(user.Id, out var notificationPrefs);
                    user.Profile = profile;
                    user.NotificationPreferences = notificationPrefs;
                }
            }

            if (user == null)
            {
                using var context = _contextFactory.CreateForReading();
                user = context.Users
                    .Include(i => i.Profile)
                    .Include(i => i.NotificationPreferences)
                    .FirstOrDefault(u => u.PublicId == publicId);
            }

            return user;
        }

        public User? GetAllUserDataByEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var normalizedEmail = email.ToLowerInvariant().Trim();
            User? user = null;

            if (Inited)
            {
                user = _userCache.Values.FirstOrDefault(u => u.Email == normalizedEmail);
                if (user != null)
                {
                    _userProfileCache.TryGetValue(user.Id, out var profile);
                    _userNotificationPrefsCache.TryGetValue(user.Id, out var notificationPrefs);
                    user.Profile = profile;
                    user.NotificationPreferences = notificationPrefs;
                }
            }

            if (user == null)
            {
                using var context = _contextFactory.CreateForReading();
                user = context.Users
                    .Include(i => i.Profile)
                    .Include(i => i.NotificationPreferences)
                    .FirstOrDefault(u => u.Email == normalizedEmail);
            }

            return user;
        }

        public User? GetUserByPublicId(Guid? publicId)
        {
            if (publicId == null) return null;
            User? user = null;

            if (Inited)
            {
                user = _userCache.Values.FirstOrDefault(u => u.PublicId == publicId);
            }

            if (user == null)
            {
                using var context = _contextFactory.CreateForReading();
                user = context.Users
                    .FirstOrDefault(u => u.PublicId == publicId);
            }

            return user;
        }

        public User? GetUserByKratosIdentityId(string? kratosIdentityId)
        {
            if (string.IsNullOrEmpty(kratosIdentityId)) return null;
            User? user = null;

            if (Inited)
            {
                user = _userCache.Values.FirstOrDefault(u => u.KratosIdentityId == kratosIdentityId);
            }

            if (user == null)
            {
                using var context = _contextFactory.CreateForReading();
                user = context.Users
                    .FirstOrDefault(u => u.KratosIdentityId == kratosIdentityId);
            }

            return user;
        }

        public User? GetUserByEmailAddress(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var normalizedEmail = email.ToLowerInvariant().Trim();

            User? user = null;

            if (Inited)
            {
                user = _userCache.Values.FirstOrDefault(u => u.Email == normalizedEmail);
            }

            if (user == null)
            {
                using var context = _contextFactory.CreateForReading();
                user = context.Users
                    .FirstOrDefault(u => u.Email == normalizedEmail);
            }

            return user;
        }

        public List<User> GetUsersByIds(List<int?> userIds)
        {
            if (userIds == null || !userIds.Any()) return new List<User>();

            var validIds = userIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<User>();

            var result = new List<User>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_userCache.TryGetValue(id, out var user))
                    {
                        result.Add(user);
                    }
                    else
                    {
                        missingIds.Add(id);
                    }
                }
            }
            else
            {
                missingIds = validIds;
            }

            if (missingIds.Count > 0)
            {
                using var context = _contextFactory.CreateForReading();
                var dbUsers = context.Users
                    .Where(u => missingIds.Contains(u.Id))
                    .ToList();

                result.AddRange(dbUsers);
            }

            return result;
        }

        public List<User> GetAllUsersDataByIds(List<int?> userIds)
        {
            if (userIds == null || !userIds.Any()) return new List<User>();

            var validIds = userIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<User>();

            var result = new List<User>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_userCache.TryGetValue(id, out var user))
                    {
                        _userProfileCache.TryGetValue(id, out var profile);
                        _userNotificationPrefsCache.TryGetValue(id, out var notificationPrefs);

                        user.Profile = profile;
                        user.NotificationPreferences = notificationPrefs;

                        result.Add(user);
                    }
                    else
                    {
                        missingIds.Add(id);
                    }
                }
            }
            else
            {
                missingIds = validIds;
            }

            if (missingIds.Count > 0)
            {
                using var context = _contextFactory.CreateForReading();
                var dbUsers = context.Users
                    .Include(i => i.Profile)
                    .Include(i => i.NotificationPreferences)
                    .Where(u => missingIds.Contains(u.Id))
                    .ToList();

                result.AddRange(dbUsers);
            }

            return result;
        }

        public List<UserInfo> GetUsersByPublicIds(List<Guid> publicIds)
        {
            if (publicIds == null || !publicIds.Any()) return new List<UserInfo>();

            var users = new List<User>();
            var missingIds = new List<Guid>();

            if (Inited)
            {
                foreach (var publicId in publicIds)
                {
                    var user = _userCache.Values.FirstOrDefault(u => u.PublicId == publicId);
                    if (user != null)
                        users.Add(user);
                    else
                        missingIds.Add(publicId);
                }
            }
            else
            {
                missingIds = publicIds;
            }

            // Fetch missing users from DB
            if (missingIds.Count > 0)
            {
                using var context = _contextFactory.CreateForReading();
                var dbUsers = context.Users
                    .Where(u => missingIds.Contains(u.PublicId))
                    .ToList();
                users.AddRange(dbUsers);
            }

            // Get profiles for all users
            var userIds = users.Select(u => u.Id).ToList();
            var profiles = GetUserProfilesByUserIds(userIds.Cast<int?>().ToList());

            // Map to UserInfo
            var userInfos = users.Select(u =>
            {
                var profile = profiles.FirstOrDefault(p => p.UserId == u.Id);
                return new UserInfo
                {
                    PublicId = u.PublicId,
                    Name = u.Name,
                    DisplayName = profile?.DisplayName ?? u.Name,
                    ProfilePictureUrl = MediaUrls.ProfilePicture(u.PublicId, profile?.ProfilePictureVersion),
                    TimeZone = profile?.DefaultTimeZone.ToTimeZoneId() ?? string.Empty,
                    Language = profile?.DefaultLanguage.ToLanguageCode() ?? string.Empty,
                    Currency = profile?.DefaultCurrency.ToCurrencyCode() ?? string.Empty,
                    IdentityColor = profile?.IdentityColor
                };
            }).ToList();

            return userInfos;
        }

        public List<UserProfile> GetUserProfilesByUserIds(List<int?> userIds)
        {
            if (userIds == null || !userIds.Any()) return new List<UserProfile>();

            var validIds = userIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<UserProfile>();

            var result = new List<UserProfile>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_userProfileCache.TryGetValue(id, out var profile))
                    {
                        result.Add(profile);
                    }
                    else
                    {
                        missingIds.Add(id);
                    }
                }
            }
            else
            {
                missingIds = validIds;
            }

            if (missingIds.Count > 0)
            {
                using var context = _contextFactory.CreateForReading();
                var dbProfiles = context.UserProfiles
                    .Where(p => missingIds.Contains(p.UserId))
                    .ToList();

                result.AddRange(dbProfiles);
            }

            return result;
        }

        public UserNotificationPreferences? GetUserPrefsByUserId(int? userId)
        {
            if (userId == null) return null;
            UserNotificationPreferences? prefs = null;

            if (Inited)
            {
                _userNotificationPrefsCache.TryGetValue((int)userId, out prefs);
            }

            if (prefs == null)
            {
                using var context = _contextFactory.CreateForReading();
                prefs = context.UserNotificationPreferences
                    .FirstOrDefault(n => n.UserId == userId);
            }

            return prefs;
        }

        public List<UserNotificationPreferences> GetUserPrefsByUserIds(List<int?> userIds)
        {
            if (userIds == null || !userIds.Any()) return new List<UserNotificationPreferences>();

            var validIds = userIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<UserNotificationPreferences>();

            var result = new List<UserNotificationPreferences>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_userNotificationPrefsCache.TryGetValue(id, out var prefs))
                    {
                        result.Add(prefs);
                    }
                    else
                    {
                        missingIds.Add(id);
                    }
                }
            }
            else
            {
                missingIds = validIds;
            }

            if (missingIds.Count > 0)
            {
                using var context = _contextFactory.CreateForReading();
                var dbPrefs = context.UserNotificationPreferences
                    .Where(n => missingIds.Contains(n.UserId))
                    .ToList();

                result.AddRange(dbPrefs);
            }

            return result;
        }
        #endregion

        #region User Management
        public async Task<User> CreateUserAsync(CreateUserRequest request, Language? defaultLanguage = null, UserTimeZone? defaultTimeZone = null, CancellationToken cancellationToken = default)
        {
            var normalizedEmail = request.Email.ToLowerInvariant().Trim();

            var user = new User
            {
                Email = normalizedEmail,
                Name = request.Name.Trim()
            };

            // Determine default currency based on language
            var language = defaultLanguage ?? Language.English;
            var defaultCurrency = language == Language.Hungarian ? Currency.Huf : Currency.Eur;

            var profile = new UserProfile
            {
                User = user,
                DisplayName = request.DisplayName?.Trim() ?? request.Name.Trim(),
                DefaultLanguage = language,
                DefaultTimeZone = defaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime,
                DefaultCurrency = defaultCurrency
            };

            var notificationPreferences = new UserNotificationPreferences
            {
                User = user
            };

            using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                context.Users.Add(user);
                context.Set<UserProfile>().Add(profile);
                // UserAuthentication no longer created - Kratos manages identity
                context.Set<UserNotificationPreferences>().Add(notificationPreferences);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error occurred while creating user");
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return user;
        }

        public async Task RemoveUserFromFamilyAsync(CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UnauthorizedException("Invalid authentication", ErrorCodes.AuthUnauthorized);
            }

            var user = GetUserById(userId);

                if (user == null)
                {
                    Log.Warning($"User not found for userId {userId.Value}");
                    throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
                }

            var hasFamily = user.FamilyId.HasValue;
            if (!hasFamily)
            {
                throw new BadRequestException("You are not a member of a family.", ErrorCodes.FamilyAlreadyMember);
            }

            using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Cache family name BEFORE user leaves
                var family = new FamilyFunctions(_contextFactory).GetFamilyById(user.FamilyId);
                var familyName = family?.Name ?? "Unknown Family";

                var familyIdToLog = user.FamilyId;
                user.FamilyId = null;
                context.Update(user);

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} left family {familyIdToLog}");

                // Record activity with cached family name
                if (familyIdToLog.HasValue)
                {
                    try
                    {
                        await new ActivityFunctions(_contextFactory).RecordActivityAsync(
                            userId.Value,
                            familyIdToLog,
                            Enums.ActivityType.FamilyLeave,
                            familyIdToLog.Value,
                            familyName,
                            null,
                            null,
                            cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to record FamilyLeave activity");
                    }
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error leaving family for user {userId.Value}");
                throw;
            }
        }
        #endregion

        #region Profile Management
        public UserProfileResponse GetProfileAsync()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            var user = GetAllUserDataById(userId);

            if (user == null)
            {
                Log.Warning($"User not found for userId {userId}");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            var profile = user.Profile;

            if (profile == null)
            {
                Log.Warning($"User profile not found for userId {userId}");
                throw new UserNotFoundException("User profile not found", ErrorCodes.UserProfileNotFound);
            }

            FamilyInfo? familyInfo = null;
            if (user.FamilyId.HasValue)
            {
                var family = new FamilyFunctions(_contextFactory).GetFamilyById(user.FamilyId.Value);
                if (family != null)
                {
                    familyInfo = new FamilyInfo
                    {
                        Name = family.Name,
                        ShareCode = family.ShareCode
                    };
                }
            }

            var profileResponse = new UserProfileResponse
            {
                PublicId = user.PublicId,
                Email = user.Email,
                Name = user.Name,
                DisplayName = profile.DisplayName,
                ProfilePictureUrl = MediaUrls.ProfilePicture(user.PublicId, profile.ProfilePictureVersion),
                TimeZone = profile.DefaultTimeZone.ToTimeZoneId(),
                Language = profile.DefaultLanguage.ToLanguageCode(),
                Currency = profile.DefaultCurrency.ToCurrencyCode(),
                IdentityColor = profile.IdentityColor,
                Family = familyInfo
            };

            return profileResponse;
        }

        public async Task UpdateUserProfileAsync(UpdateUserSettingsRequest request, IKratosService? kratosService = null, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            var user = GetUserById(userId);
            if (user == null)
            {
                Log.Warning($"User not found for userId {userId}");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            if (!string.IsNullOrWhiteSpace(request.Email) && request.Email.ToLowerInvariant().Trim() != user.Email)
            {
                var normalizedNewEmail = request.Email.ToLowerInvariant().Trim();
                var existingUser = GetUserByEmailAddress(normalizedNewEmail);
                if (existingUser != null && existingUser.Id != userId)
                {
                    throw new BadRequestException("Email address is already in use", ErrorCodes.UserEmailInUse);
                }
            }

            using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var userEntity = await context.Users.FindAsync([userId], cancellationToken);
                var profile = await context.Set<UserProfile>().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

                if (userEntity == null)
                {
                    throw new UserNotFoundException($"User not found: {userId}");
                }

                if (profile == null)
                {
                    throw new UserNotFoundException($"UserProfile not found for user {userId}");
                }

                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    userEntity.Email = request.Email.ToLowerInvariant().Trim();
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    userEntity.Name = request.Name.Trim();
                }

                if (!string.IsNullOrWhiteSpace(request.DisplayName))
                {
                    profile.DisplayName = request.DisplayName.Trim();
                }

                if (request.DefaultCurrency.HasValue)
                {
                    profile.DefaultCurrency = request.DefaultCurrency.Value;
                }

                if (request.DefaultTimeZone.HasValue)
                {
                    profile.DefaultTimeZone = request.DefaultTimeZone.Value;
                }

                if (request.DefaultLanguage.HasValue)
                {
                    profile.DefaultLanguage = request.DefaultLanguage.Value;
                }

                if (request.IdentityColor != null)
                {
                    if (request.IdentityColor == IdentityColorAuto)
                    {
                        profile.IdentityColor = null;
                    }
                    else if (IdentityColorKeys.Contains(request.IdentityColor))
                    {
                        profile.IdentityColor = request.IdentityColor;
                    }
                    else if (IdentityColorHexPattern.IsMatch(request.IdentityColor))
                    {
                        // Normalised to lowercase so the stored value always has one canonical form —
                        // the web client compares against it directly rather than re-parsing case.
                        profile.IdentityColor = request.IdentityColor.ToLowerInvariant();
                    }
                    else
                    {
                        throw new BadRequestException("Unknown identity colour.", ErrorCodes.ValidationInvalidRequest);
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} updated their settings");

                // Sync to Kratos (non-blocking)
                if (kratosService != null && userEntity != null && profile != null)
                {
                    await SyncUserProfileToKratosAsync(userEntity, profile, kratosService, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error updating settings for user {userId}");
                throw;
            }
        }

        #endregion

        #region Current User

        public UserInfo GetCurrentUser()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            var user = GetAllUserDataById(userId);

            if (user == null)
            {
                Log.Warning($"User not found for userId {userId}");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            var profile = user.Profile;

            if (profile == null)
            {
                Log.Warning($"User profile not found for userId {userId}");
                throw new UserNotFoundException("User profile not found", ErrorCodes.UserProfileNotFound);
            }

            var userInfo = new UserInfo
            {
                PublicId = user.PublicId,
                Name = user.Name,
                DisplayName = profile.DisplayName,
                ProfilePictureUrl = MediaUrls.ProfilePicture(user.PublicId, profile.ProfilePictureVersion),
                TimeZone = profile.DefaultTimeZone.ToTimeZoneId(),
                Language = profile.DefaultLanguage.ToLanguageCode(),
                Currency = profile.DefaultCurrency.ToCurrencyCode(),
                IdentityColor = profile.IdentityColor,
                LastSeenAt = profile.LastSeenAt,
                OnboardingCompletedAt = profile.OnboardingCompletedAt
            };

            return userInfo;
        }
        #endregion

        #region Kratos Integration
        /// <summary>
        /// Gets user info from a Kratos session combined with local user data.
        /// </summary>
        public UserInfo GetUserInfoFromKratosSession(Models.Kratos.KratosSession session, User user)
        {
            var traits = session.Identity.Traits;

            // The avatar and identity colour are the two fields here that do not come from the
            // session: neither is a Kratos trait, and the local profile is where they live.
            var profile = GetUserProfileByUserId(user.Id);

            return new UserInfo
            {
                PublicId = user.PublicId,
                Name = traits.Name ?? user.Name,
                DisplayName = traits.DisplayName ?? traits.Name ?? user.Name,
                ProfilePictureUrl = MediaUrls.ProfilePicture(user.PublicId, profile?.ProfilePictureVersion),
                TimeZone = traits.DefaultTimezone ?? "Europe/Budapest",
                Language = traits.DefaultLanguage ?? "hu",
                Currency = traits.DefaultCurrency ?? "HUF",
                IdentityColor = profile?.IdentityColor,
                LastSeenAt = profile?.LastSeenAt,
                OnboardingCompletedAt = profile?.OnboardingCompletedAt
            };
        }

        /// <summary>
        /// Creates a local user record from a Kratos identity.
        /// </summary>
        public async Task<User?> CreateUserFromKratosAsync(Models.Kratos.KratosIdentity identity, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

                var user = new User
                {
                    KratosIdentityId = identity.Id,
                    Email = identity.Traits.Email.ToLowerInvariant().Trim(),
                    Name = identity.Traits.Name ?? identity.Traits.Email.Split('@')[0],
                    Status = identity.State == "active" ? UserStatus.Active : UserStatus.PendingVerification,
                    FamilyId = identity.Traits.FamilyId,
                    LastLoginAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(user);
                await context.SaveChangesAsync(cancellationToken);

                // Create user profile from Kratos traits
                var profile = new UserProfile
                {
                    UserId = user.Id,
                    DisplayName = identity.Traits.DisplayName ?? identity.Traits.Name ?? user.Name,
                    DateOfBirth = !string.IsNullOrEmpty(identity.Traits.DateOfBirth) 
                        ? DateTime.TryParse(identity.Traits.DateOfBirth, out var dob) ? dob : null 
                        : null,
                    Gender = identity.Traits.Gender,
                    DefaultCurrency = ParseCurrency(identity.Traits.DefaultCurrency),
                    DefaultTimeZone = ParseTimeZone(identity.Traits.DefaultTimezone),
                    DefaultLanguage = ParseLanguage(identity.Traits.DefaultLanguage)
                };

                context.UserProfiles.Add(profile);

                // Create default notification preferences
                var notificationPrefs = new UserNotificationPreferences
                {
                    UserId = user.Id,
                    EmailNotificationsEnabled = true,
                    EmailWeeklySummaryEnabled = true,
                    PushNotificationsEnabled = true,
                    PushWeeklySummaryEnabled = true,
                    PushFamilyChatEnabled = true,
                    InAppNotificationsEnabled = true
                };

                context.UserNotificationPreferences.Add(notificationPrefs);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"Created local user {user.Id} for Kratos identity {identity.Id}");
                return user;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to create local user for Kratos identity {identity.Id}");
                return null;
            }
        }

        /// <summary>
        /// Builds KratosTraits from local User and UserProfile data.
        /// Used for syncing profile changes back to Kratos.
        /// Note: Optional fields are set to null (not empty string) so JsonIgnore works properly.
        /// </summary>
        public static Models.Kratos.KratosTraits BuildKratosTraitsFromProfile(User user, Entities.User.UserProfile profile)
        {
            return new Models.Kratos.KratosTraits
            {
                Email = user.Email,
                Name = user.Name,
                DisplayName = profile.DisplayName,
                // Deliberately null: avatars are served from GET /User/{publicId}/profile-picture
                // and are not a Kratos trait any more. Writing null here is also what clears the
                // legacy trait off identities that still carry one.
                ProfilePictureBase64 = null,
                DateOfBirth = profile.DateOfBirth?.ToString("yyyy-MM-dd"),
                Gender = string.IsNullOrEmpty(profile.Gender) ? null : profile.Gender,
                // Use ToKratosCurrencyEnum() for Kratos schema compatibility (maps unsupported currencies to EUR)
                DefaultCurrency = profile.DefaultCurrency.ToKratosCurrencyEnum(),
                // Use ToKratosTimezoneEnum() for Kratos schema compatibility
                DefaultTimezone = profile.DefaultTimeZone.ToKratosTimezoneEnum(),
                DefaultLanguage = profile.DefaultLanguage.ToLanguageCode(),
                FamilyId = user.FamilyId
            };
        }

        /// <summary>
        /// Syncs local user profile data to Kratos identity traits.
        /// Non-blocking: logs warning on failure but doesn't throw.
        /// </summary>
        public async Task SyncUserProfileToKratosAsync(User user, Entities.User.UserProfile profile, IKratosService kratosService, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(user.KratosIdentityId))
            {
                Log.Debug($"User {user.Id} has no Kratos identity ID, skipping Kratos sync");
                return;
            }

            try
            {
                var traits = BuildKratosTraitsFromProfile(user, profile);
                var result = await kratosService.UpdateIdentityTraitsAsync(user.KratosIdentityId, traits, cancellationToken);
                
                if (result != null)
                {
                    Log.Debug($"Synced user {user.Id} profile to Kratos identity {user.KratosIdentityId}");
                }
                else
                {
                    Log.Warning($"Failed to sync user {user.Id} profile to Kratos identity {user.KratosIdentityId}");
                }
            }
            catch (Exception ex)
            {
                // Non-blocking: log warning but don't fail the request
                Log.Warning(ex, $"Error syncing user {user.Id} profile to Kratos: {ex.Message}");
            }
        }

        /// <summary>
        /// Syncs a local user record with Kratos identity data.
        /// </summary>
        public async Task<User?> SyncUserFromKratosAsync(Models.Kratos.KratosIdentity identity, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var user = await context.Users.FirstOrDefaultAsync(u => u.KratosIdentityId == identity.Id, cancellationToken);

                if (user == null)
                {
                    // Try to find by email and link
                    user = await context.Users.FirstOrDefaultAsync(u => u.Email == identity.Traits.Email.ToLowerInvariant().Trim(), cancellationToken);
                    
                    if (user == null)
                    {
                        return await CreateUserFromKratosAsync(identity, cancellationToken);
                    }

                    user.KratosIdentityId = identity.Id;
                }

                // Update user fields from Kratos
                user.Name = identity.Traits.Name ?? user.Name;
                user.Email = identity.Traits.Email.ToLowerInvariant().Trim();
                user.FamilyId = identity.Traits.FamilyId ?? user.FamilyId;
                user.Status = identity.State == "active" ? UserStatus.Active : user.Status;

                // Update profile if exists
                var profile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
                if (profile != null)
                {
                    profile.DisplayName = identity.Traits.DisplayName ?? identity.Traits.Name ?? profile.DisplayName;
                    profile.DefaultCurrency = ParseCurrency(identity.Traits.DefaultCurrency);
                    profile.DefaultTimeZone = ParseTimeZone(identity.Traits.DefaultTimezone);
                    profile.DefaultLanguage = ParseLanguage(identity.Traits.DefaultLanguage);
                    
                    if (!string.IsNullOrEmpty(identity.Traits.DateOfBirth) && DateTime.TryParse(identity.Traits.DateOfBirth, out var dob))
                    {
                        profile.DateOfBirth = dob;
                    }
                    
                    profile.Gender = identity.Traits.Gender ?? profile.Gender;
                }

                await context.SaveChangesAsync(cancellationToken);

                Log.Debug($"Synced user {user.Id} with Kratos identity {identity.Id}");
                return user;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to sync user with Kratos identity {identity.Id}");
                return null;
            }
        }

        /// <summary>
        /// Links an existing user to a Kratos identity.
        /// </summary>
        public async Task LinkUserToKratosAsync(int userId, string kratosIdentityId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

                if (user == null)
                {
                    Log.Warning($"Cannot link user {userId} to Kratos - user not found");
                    return;
                }

                user.KratosIdentityId = kratosIdentityId;
                await context.SaveChangesAsync(cancellationToken);

                Log.Information($"Linked user {userId} to Kratos identity {kratosIdentityId}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to link user {userId} to Kratos identity {kratosIdentityId}");
            }
        }

        /// <summary>
        /// Updates the last login time for a user.
        /// </summary>
        public async Task UpdateLastLoginAsync(int userId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

                if (user == null) return;

                user.LastLoginAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to update last login for user {userId}");
            }
        }

        private static Currency ParseCurrency(string? currencyCode)
        {
            return currencyCode?.ToUpperInvariant() switch
            {
                "HUF" => Currency.Huf,
                "EUR" => Currency.Eur,
                "USD" => Currency.Usd,
                _ => Currency.Huf
            };
        }

        private static UserTimeZone ParseTimeZone(string? timezoneId)
        {
            return timezoneId switch
            {
                "Europe/Budapest" => UserTimeZone.CentralEuropeStandardTime,
                "Europe/Berlin" => UserTimeZone.CentralEuropeStandardTime,
                "Europe/London" => UserTimeZone.GreenwichStandardTime,
                "America/New_York" => UserTimeZone.EasternStandardTime,
                "America/Los_Angeles" => UserTimeZone.PacificStandardTime,
                "UTC" => UserTimeZone.UTC,
                _ => UserTimeZone.CentralEuropeStandardTime
            };
        }

        private static Language ParseLanguage(string? languageCode)
        {
            return languageCode?.ToLowerInvariant() switch
            {
                "hu" => Language.Hungarian,
                "de" => Language.German,
                "en" => Language.English,
                _ => Language.Hungarian
            };
        }
        #endregion

        #region Notification Preferences Management
        public NotificationPreferencesResponse GetNotificationPreferencesAsync()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            var prefs = GetUserNotificationPreferencesByUserId(userId.Value);

            if (prefs == null)
            {
                Log.Warning($"Notification preferences not found for userId {userId}");
                throw new UserNotFoundException("Notification preferences not found", ErrorCodes.UserNotFound);
            }

            var response = new NotificationPreferencesResponse
            {
                EmailNotificationsEnabled = prefs.EmailNotificationsEnabled,
                EmailWeeklySummaryEnabled = prefs.EmailWeeklySummaryEnabled,
                PushNotificationsEnabled = prefs.PushNotificationsEnabled,
                PushWeeklySummaryEnabled = prefs.PushWeeklySummaryEnabled,
                PushFamilyChatEnabled = prefs.PushFamilyChatEnabled,
                InAppNotificationsEnabled = prefs.InAppNotificationsEnabled
            };

            return response;
        }

        public async Task UpdateNotificationPreferencesAsync(UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var prefs = await context.Set<UserNotificationPreferences>()
                    .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

                if (prefs == null)
                {
                    Log.Warning($"Notification preferences not found for userId {userId}");
                    throw new UserNotFoundException("Notification preferences not found", ErrorCodes.UserNotFound);
                }

                // Update only provided values (partial update)
                if (request.EmailNotificationsEnabled.HasValue)
                {
                    prefs.EmailNotificationsEnabled = request.EmailNotificationsEnabled.Value;
                }

                if (request.EmailWeeklySummaryEnabled.HasValue)
                {
                    prefs.EmailWeeklySummaryEnabled = request.EmailWeeklySummaryEnabled.Value;
                }

                if (request.PushNotificationsEnabled.HasValue)
                {
                    prefs.PushNotificationsEnabled = request.PushNotificationsEnabled.Value;
                }

                if (request.PushWeeklySummaryEnabled.HasValue)
                {
                    prefs.PushWeeklySummaryEnabled = request.PushWeeklySummaryEnabled.Value;
                }

                if (request.PushFamilyChatEnabled.HasValue)
                {
                    prefs.PushFamilyChatEnabled = request.PushFamilyChatEnabled.Value;
                }

                if (request.InAppNotificationsEnabled.HasValue)
                {
                    prefs.InAppNotificationsEnabled = request.InAppNotificationsEnabled.Value;
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} updated notification preferences");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error updating notification preferences for user {userId}");
                throw;
            }
        }
        #endregion

        #region Onboarding
        /// <summary>
        /// Records that the caller has finished or skipped the first-run tour, or - with
        /// <paramref name="completed"/> false - clears the flag so the tour runs again (#98).
        /// </summary>
        /// <remarks>
        /// Deliberately idempotent, and deliberately does not preserve an earlier timestamp when
        /// called twice with <c>true</c>: the value the UI cares about is only "is this null", and
        /// re-stamping is one fewer branch than reasoning about which completion counted.
        /// </remarks>
        public async Task SetOnboardingCompletedAsync(bool completed, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            using var context = _contextFactory.CreateDbContext();

            var profile = await context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

            if (profile == null)
            {
                Log.Warning($"User profile not found for userId {userId}");
                throw new UserNotFoundException("User profile not found", ErrorCodes.UserProfileNotFound);
            }

            profile.OnboardingCompletedAt = completed ? DateTime.UtcNow : null;
            await context.SaveChangesAsync(cancellationToken);

            Log.Information("User {UserId} set onboarding completed to {Completed}", userId, completed);
        }
        #endregion
    }
}
