using Homassy.API.Context;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Models.Auth;
using Homassy.API.Models.Background;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Homassy.API.Models.User;
using Homassy.API.Security;
using Homassy.API.Services;
using Homassy.API.Services.Background;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Functions
{
    public class UserFunctions
    {
        private static readonly ConcurrentDictionary<int, User> _userCache = new();
        private static readonly ConcurrentDictionary<int, UserProfile> _userProfileCache = new();
        private static readonly ConcurrentDictionary<int, UserAuthentication> _userAuthCache = new();
        private static readonly ConcurrentDictionary<int, UserNotificationPreferences> _userNotificationPrefsCache = new();

        private static IEmailQueueService? _emailQueueService;
        private readonly AccountLockoutService _lockoutService;

        public static bool Inited = false;

        public UserFunctions()
        {
            _lockoutService = new AccountLockoutService();
        }

        public UserFunctions(AccountLockoutService lockoutService)
        {
            _lockoutService = lockoutService;
        }

        public static void SetEmailQueueService(IEmailQueueService emailQueueService)
        {
            _emailQueueService = emailQueueService;
        }

        #region Cache Management
        public async Task InitializeCacheAsync(CancellationToken cancellationToken = default)
        {
            var context = new HomassyDbContext();
            var users =  await context.Users
                .ToListAsync(cancellationToken);

            var profiles = await context.UserProfiles
                .ToListAsync(cancellationToken);

            var authentications = await context.UserAuthentications
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

                foreach (var authentication in authentications)
                {
                    _userAuthCache[authentication.UserId] = authentication;
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
                var context = new HomassyDbContext();
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
                var context = new HomassyDbContext();
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

        public async Task RefreshUserAuthCacheAsync(int recordId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var userAuth = await context.UserAuthentications
                    .FirstOrDefaultAsync(u => u.Id == recordId, cancellationToken);

                if (userAuth != null)
                {
                    var existsInCache = _userAuthCache.ContainsKey(userAuth.UserId);

                    if (existsInCache)
                    {
                        _userAuthCache[userAuth.UserId] = userAuth;
                        Log.Debug($"Refreshed user authentication {userAuth.UserId} in cache.");
                    }
                    else
                    {
                        _userAuthCache[userAuth.UserId] = userAuth;
                        Log.Debug($"Added user authentication {userAuth.UserId} to cache.");
                    }
                }
                else
                {
                    var cacheEntry = _userAuthCache.FirstOrDefault(kvp => kvp.Value.Id == recordId);
                    if (cacheEntry.Value != null)
                    {
                        _userAuthCache.TryRemove(cacheEntry.Key, out _);
                        Log.Debug($"Removed deleted user authentication record {recordId} (userId: {cacheEntry.Key}) from cache.");
                    }
                    else
                    {
                        Log.Debug($"User authentication record {recordId} not found in DB or cache.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for user authentication record {recordId}.");
                throw;
            }
        }

        public async Task RefreshUserNotificationCacheAsync(int recordId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
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
                var context = new HomassyDbContext();
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
                var context = new HomassyDbContext();
                profile = context.UserProfiles
                    .FirstOrDefault(p => p.UserId == userId);
            }

            return profile;
        }

        public UserAuthentication? GetUserAuthenticationByUserId(int? userId)
        {
            if (userId == null) return null;
            UserAuthentication? auth = null;

            if (Inited)
            {
                _userAuthCache.TryGetValue((int)userId, out auth);
            }

            if (auth == null)
            {
                var context = new HomassyDbContext();
                auth = context.UserAuthentications
                    .FirstOrDefault(a => a.UserId == userId);
            }

            return auth;
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
                var context = new HomassyDbContext();
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
                    // Compose user data from all caches
                    _userProfileCache.TryGetValue((int)userId, out var profile);
                    _userAuthCache.TryGetValue((int)userId, out var auth);
                    _userNotificationPrefsCache.TryGetValue((int)userId, out var notificationPrefs);
                    
                    user.Profile = profile;
                    user.Authentication = auth;
                    user.NotificationPreferences = notificationPrefs;
                }
            }
            
            if (user == null)
            {
                var context = new HomassyDbContext();
                user = context.Users
                    .Include(i => i.Profile)
                    .Include(i => i.Authentication)
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
                    // Compose user data from all caches
                    _userProfileCache.TryGetValue(user.Id, out var profile);
                    _userAuthCache.TryGetValue(user.Id, out var auth);
                    _userNotificationPrefsCache.TryGetValue(user.Id, out var notificationPrefs);
                    user.Profile = profile;
                    user.Authentication = auth;
                    user.NotificationPreferences = notificationPrefs;
                }
            }

            if (user == null)
            {
                var context = new HomassyDbContext();
                user = context.Users
                    .Include(i => i.Profile)
                    .Include(i => i.Authentication)
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
                    // Compose user data from all caches
                    _userProfileCache.TryGetValue(user.Id, out var profile);
                    _userAuthCache.TryGetValue(user.Id, out var auth);
                    _userNotificationPrefsCache.TryGetValue(user.Id, out var notificationPrefs);
                    user.Profile = profile;
                    user.Authentication = auth;
                    user.NotificationPreferences = notificationPrefs;
                }
            }

            if (user == null)
            {
                var context = new HomassyDbContext();
                user = context.Users
                    .Include(i => i.Profile)
                    .Include(i => i.Authentication)
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
                var context = new HomassyDbContext();
                user = context.Users
                    .FirstOrDefault(u => u.PublicId == publicId);
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
                var context = new HomassyDbContext();
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
                var context = new HomassyDbContext();
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
                        // Compose user data from all caches
                        _userProfileCache.TryGetValue(id, out var profile);
                        _userAuthCache.TryGetValue(id, out var auth);
                        _userNotificationPrefsCache.TryGetValue(id, out var notificationPrefs);

                        user.Profile = profile;
                        user.Authentication = auth;
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
                var context = new HomassyDbContext();
                var dbUsers = context.Users
                    .Include(i => i.Profile)
                    .Include(i => i.Authentication)
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
                var context = new HomassyDbContext();
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
                    Name = u.Name,
                    DisplayName = profile?.DisplayName ?? u.Name,
                    ProfilePictureBase64 = profile?.ProfilePictureBase64,
                    TimeZone = profile?.DefaultTimeZone.ToTimeZoneId() ?? string.Empty,
                    Language = profile?.DefaultLanguage.ToLanguageCode() ?? string.Empty,
                    Currency = profile?.DefaultCurrency.ToCurrencyCode() ?? string.Empty
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
                var context = new HomassyDbContext();
                var dbProfiles = context.UserProfiles
                    .Where(p => missingIds.Contains(p.UserId))
                    .ToList();

                result.AddRange(dbProfiles);
            }

            return result;
        }

        public UserAuthentication? GetUserAuthByUserId(int? userId)
        {
            if (userId == null) return null;
            UserAuthentication? auth = null;

            if (Inited)
            {
                _userAuthCache.TryGetValue((int)userId, out auth);
            }

            if (auth == null)
            {
                var context = new HomassyDbContext();
                auth = context.UserAuthentications
                    .FirstOrDefault(a => a.UserId == userId);
            }

            return auth;
        }

        public List<UserAuthentication> GetUserAuthsByUserIds(List<int?> userIds)
        {
            if (userIds == null || !userIds.Any()) return new List<UserAuthentication>();

            var validIds = userIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<UserAuthentication>();

            var result = new List<UserAuthentication>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_userAuthCache.TryGetValue(id, out var auth))
                    {
                        result.Add(auth);
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
                var context = new HomassyDbContext();
                var dbAuths = context.UserAuthentications
                    .Where(a => missingIds.Contains(a.UserId))
                    .ToList();

                result.AddRange(dbAuths);
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
                var context = new HomassyDbContext();
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
                var context = new HomassyDbContext();
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

            var authentication = new UserAuthentication
            {
                User = user
            };

            var notificationPreferences = new UserNotificationPreferences
            {
                User = user
            };

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                context.Users.Add(user);
                context.Set<UserProfile>().Add(profile);
                context.Set<UserAuthentication>().Add(authentication);
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

        public async Task RegisterAsync(CreateUserRequest request, Language? browserLanguage = null, UserTimeZone? browserTimeZone = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            {
                throw new BadRequestException("Invalid email address", ErrorCodes.ValidationInvalidEmail);
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new BadRequestException("Name is required", ErrorCodes.ValidationNameRequired);
            }

            var normalizedEmail = request.Email.ToLowerInvariant().Trim();

            var existingUser = GetUserByEmailAddress(normalizedEmail);
            if (existingUser != null)
            {
                Log.Information($"Registration attempt for existing email {normalizedEmail}");
                return;
            }

            var user = await CreateUserAsync(request, browserLanguage, browserTimeZone, cancellationToken);

            var code = EmailService.GenerateVerificationCode();
            var expirationMinutes = int.Parse(ConfigService.GetValue("EmailVerification:CodeExpirationMinutes"));
            var expiry = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var userAuth = await context.Set<UserAuthentication>().FirstOrDefaultAsync(a => a.UserId == user.Id, cancellationToken);

                if (userAuth == null)
                {
                    Log.Warning($"UserAuthentication not found for user {user.Id}");
                    throw new UserNotFoundException("User authentication data not found", ErrorCodes.UserAuthNotFound);
                }

                userAuth.VerificationCode = code;
                userAuth.VerificationCodeExpiry = expiry;

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error setting verification code for user {user.Id}");
                throw;
            }

            var profile = GetUserProfileByUserId(user.Id);
            var timezone = profile?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime;
            var language = profile?.DefaultLanguage ?? browserLanguage ?? Language.English;

            await SendEmailAsync(new EmailTask(user.Email, code, timezone, language, EmailType.Registration));

            Log.Information($"New user registered: {normalizedEmail}, registration email with verification code sent");
        }

        public async Task<FamilyInfo> JoinFamilyAsync(JoinFamilyRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UnauthorizedException("Invalid authentication", ErrorCodes.AuthUnauthorized);
            }

            var familyId = SessionInfo.GetFamilyId();
            if (familyId.HasValue)
            {
                throw new BadRequestException("You are already a member of a family. Please leave your current family first.", ErrorCodes.FamilyAlreadyMember);
            }

            if (string.IsNullOrWhiteSpace(request.ShareCode))
            {
                throw new BadRequestException("Share code is required", ErrorCodes.ValidationShareCodeRequired);
            }

            var family = new FamilyFunctions().GetFamilyByShareCode(request.ShareCode);
            if (family == null)
            {
                throw new FamilyNotFoundException("Family not found with the provided share code", ErrorCodes.FamilyInvalidShareCode);
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var user = GetUserById(userId);

                if (user == null)
                {
                    Log.Warning($"User not found for userId {userId.Value}");
                    throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
                }

                user.FamilyId = family.Id;

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} joined family {family.Id}");

                // Record activity
                try
                {
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        family.Id,
                        Enums.ActivityType.FamilyJoin,
                        family.Id,
                        family.Name,
                        null,
                        null,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record FamilyJoin activity for family {family.Name}");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error joining family for user {userId.Value}");
                throw;
            }

            var response = new FamilyInfo
            {
                Name = family.Name,
                ShareCode = family.ShareCode
            };

            return response;
        }

        public async Task RemoveUserFromFamilyAsync(CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UnauthorizedException("Invalid authentication", ErrorCodes.AuthUnauthorized);
            }

            var familyId = SessionInfo.GetFamilyId();
            if (!familyId.HasValue)
            {
                throw new BadRequestException("You are not a member of any family", ErrorCodes.FamilyNotMember);
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var user = GetUserById(userId);

                if (user == null)
                {
                    Log.Warning($"User not found for userId {userId}");
                    throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
                }

                // Cache family name BEFORE user leaves
                var family = new FamilyFunctions().GetFamilyById(familyId);
                var familyName = family?.Name ?? "Unknown Family";

                var familyIdToLog = user.FamilyId;
                user.FamilyId = null;

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} left family {familyIdToLog}");

                // Record activity with cached family name
                if (familyIdToLog.HasValue)
                {
                    try
                    {
                        await new ActivityFunctions().RecordActivityAsync(
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

        #region Authentication Management
        public async Task RequestVerificationCodeAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                throw new BadRequestException("Invalid email address", ErrorCodes.ValidationInvalidEmail);
            }

            var normalizedEmail = email.ToLowerInvariant().Trim();

            var user = GetAllUserDataByEmail(normalizedEmail);

            if (user == null || user.Authentication == null)
            {
                Log.Information($"User not found for {normalizedEmail}");
                return;
            }

            var code = EmailService.GenerateVerificationCode();
            var expirationMinutes = int.Parse(ConfigService.GetValue("EmailVerification:CodeExpirationMinutes"));
            var expiry = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var userAuth = await context.Set<UserAuthentication>().FirstOrDefaultAsync(a => a.UserId == user.Id, cancellationToken);

                if (userAuth == null)
                {
                    Log.Warning($"UserAuthentication not found for user {user.Id}");
                    throw new UserNotFoundException("User authentication data not found", ErrorCodes.UserAuthNotFound);
                }

                userAuth.VerificationCode = code;
                userAuth.VerificationCodeExpiry = expiry;

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error setting verification code for user {user.Id}");
                throw;
            }

            var profile = user.Profile ?? GetUserProfileByUserId(user.Id);
            var timezone = profile?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime;
            var language = profile?.DefaultLanguage ?? Language.English;

            await SendEmailAsync(new EmailTask(user.Email, code, timezone, language, EmailType.Verification));
        }

        public async Task<AuthResponse> VerifyCodeAsync(string email, string code, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            {
                throw new BadRequestException("Email and code are required", ErrorCodes.ValidationEmailCodeRequired);
            }

            var normalizedEmail = email.ToLowerInvariant().Trim();
            var trimmedCode = code.Trim();

            var user = GetAllUserDataByEmail(normalizedEmail);

            if (user == null || user.Authentication == null)
            {
                Log.Warning($"User not found for email {normalizedEmail}");
                throw new InvalidCredentialsException("Invalid email or code", ErrorCodes.AuthInvalidCredentials);
            }

            var auth = user.Authentication;

            if (_lockoutService.IsLockedOut(auth.LockedOutUntil))
            {
                Log.Warning($"Account locked for {normalizedEmail} until {auth.LockedOutUntil}");
                throw new AccountLockedException(auth.LockedOutUntil!.Value);
            }

            if (auth.VerificationCodeExpiry == null || auth.VerificationCodeExpiry < DateTime.UtcNow)
            {
                Log.Warning($"Expired verification code for {normalizedEmail}");
                throw new ExpiredCredentialsException("Invalid or expired code", ErrorCodes.AuthExpiredCredentials);
            }

            if (!SecureCompare.ConstantTimeEquals(auth.VerificationCode, trimmedCode))
            {
                await RecordFailedLoginAttemptAsync(user.Id, auth.FailedLoginAttempts + 1, cancellationToken);
                Log.Warning($"Invalid verification code for {normalizedEmail}");
                throw new InvalidCredentialsException("Invalid or expired code", ErrorCodes.AuthInvalidCredentials);
            }

            var isFirstLogin = user.Status == UserStatus.PendingVerification;

            var accessToken = JwtService.GenerateAccessToken(user);
            var refreshToken = JwtService.GenerateRefreshToken();
            var tokenFamily = JwtService.GenerateTokenFamily();
            var accessTokenExpiry = JwtService.GetAccessTokenExpiration();
            var refreshTokenExpiry = JwtService.GetRefreshTokenExpiration();

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var userAuth = await context.Set<UserAuthentication>().FirstOrDefaultAsync(a => a.UserId == user.Id, cancellationToken);
                var userEntity = await context.Users.FindAsync([user.Id], cancellationToken);

                if (userAuth == null || userEntity == null)
                {
                    Log.Warning($"User or UserAuthentication not found for user {user.Id}");
                    throw new UserNotFoundException("User authentication data not found", ErrorCodes.UserAuthNotFound);
                }

                if (isFirstLogin)
                {
                    userEntity.Status = UserStatus.Active;
                    Log.Information($"Email verified for new user {normalizedEmail}");
                }

                userAuth.AccessToken = accessToken;
                userAuth.AccessTokenExpiry = accessTokenExpiry;
                userAuth.RefreshToken = refreshToken;
                userAuth.RefreshTokenExpiry = refreshTokenExpiry;
                userAuth.TokenFamily = tokenFamily;
                userAuth.PreviousRefreshToken = null;
                userAuth.PreviousRefreshTokenExpiry = null;
                userAuth.VerificationCode = null;
                userAuth.VerificationCodeExpiry = null;
                userAuth.FailedLoginAttempts = 0;
                userAuth.LastFailedLoginAt = null;
                userAuth.LockedOutUntil = null;
                userEntity.LastLoginAt = DateTime.UtcNow;

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error completing authentication for user {user.Id}");
                throw;
            }

            Log.Information($"User {normalizedEmail} successfully authenticated with new token family");

            var profile = user.Profile ?? GetUserProfileByUserId(user.Id);

            var authResponse = new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessTokenExpiry,
                RefreshTokenExpiresAt = refreshTokenExpiry,
                User = new UserInfo
                {
                    Name = user.Name,
                    DisplayName = profile?.DisplayName ?? user.Name,
                    ProfilePictureBase64 = profile?.ProfilePictureBase64,
                    TimeZone = (profile?.DefaultTimeZone ?? UserTimeZone.CentralEuropeStandardTime).ToTimeZoneId(),
                    Language = (profile?.DefaultLanguage ?? Language.Hungarian).ToLanguageCode(),
                    Currency = (profile?.DefaultCurrency ?? Currency.Huf).ToCurrencyCode()
                }
            };

            return authResponse;
        }

        private async Task RecordFailedLoginAttemptAsync(int userId, int newFailedAttempts, CancellationToken cancellationToken)
        {
            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var userAuth = await context.Set<UserAuthentication>().FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

                if (userAuth == null)
                {
                    return;
                }

                userAuth.FailedLoginAttempts = newFailedAttempts;
                userAuth.LastFailedLoginAt = DateTime.UtcNow;

                if (_lockoutService.ShouldLockout(newFailedAttempts))
                {
                    userAuth.LockedOutUntil = _lockoutService.CalculateLockoutExpiry(newFailedAttempts);
                    Log.Warning($"Account locked for user {userId} until {userAuth.LockedOutUntil} after {newFailedAttempts} failed attempts");
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error recording failed login attempt for user {userId}");
            }
        }

        public async Task<RefreshTokenResponse> RefreshTokenAsync(Guid userPublicId, string currentRefreshToken, CancellationToken cancellationToken = default)
        {
            // Validate input parameters
            if (userPublicId == Guid.Empty)
            {
                Log.Warning("Invalid user public ID provided for token refresh");
                throw new BadRequestException("Invalid user ID", ErrorCodes.ValidationInvalidRequest);
            }

            if (string.IsNullOrWhiteSpace(currentRefreshToken))
            {
                throw new BadRequestException("Refresh token is required", ErrorCodes.ValidationRefreshTokenRequired);
            }

            // Get user by public ID (not from SessionInfo)
            var user = GetUserByPublicId(userPublicId);

            if (user == null)
            {
                Log.Warning($"User not found for publicId {userPublicId} during token refresh");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            var userId = user.Id;

            // Get full user data including authentication
            var fullUserData = GetAllUserDataById(userId);

            if (fullUserData == null || fullUserData.Authentication == null)
            {
                Log.Warning($"User authentication data not found for userId {userId}");
                throw new UserNotFoundException("User authentication not found", ErrorCodes.UserAuthNotFound);
            }

            var auth = fullUserData.Authentication;

            var isCurrentToken = SecureCompare.ConstantTimeEquals(auth.RefreshToken, currentRefreshToken);

            var isPreviousToken = !string.IsNullOrEmpty(auth.PreviousRefreshToken) &&
                                  SecureCompare.ConstantTimeEquals(auth.PreviousRefreshToken, currentRefreshToken) &&
                                  auth.PreviousRefreshTokenExpiry != null &&
                                  auth.PreviousRefreshTokenExpiry > DateTime.UtcNow;

            if (!isCurrentToken && !isPreviousToken)
            {
                if (auth.TokenFamily.HasValue)
                {
                    Log.Warning($"Potential token theft detected for user {userId}. Token reuse attempted. Invalidating all tokens.");
                    await InvalidateAllUserTokensAsync(userId, cancellationToken);
                    throw new InvalidCredentialsException("Invalid refresh token. All sessions invalidated for security.", ErrorCodes.AuthTokenTheftDetected);
                }

                Log.Warning($"Invalid refresh token for user {userId}");
                throw new InvalidCredentialsException("Invalid refresh token", ErrorCodes.AuthInvalidRefreshToken);
            }

            if (isCurrentToken && (auth.RefreshTokenExpiry == null || auth.RefreshTokenExpiry < DateTime.UtcNow))
            {
                Log.Warning($"Expired refresh token for user {userId}");
                throw new ExpiredCredentialsException("Expired refresh token", ErrorCodes.AuthExpiredCredentials);
            }

            var newAccessToken = JwtService.GenerateAccessToken(user);
            var newRefreshToken = JwtService.GenerateRefreshToken();
            var accessTokenExpiry = JwtService.GetAccessTokenExpiration();
            var refreshTokenExpiry = JwtService.GetRefreshTokenExpiration();
            var previousTokenGracePeriod = JwtService.GetPreviousRefreshTokenGracePeriod();

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var userAuth = await context.Set<UserAuthentication>().FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

                if (userAuth == null)
                {
                    Log.Warning($"UserAuthentication not found for user {userId}");
                    throw new UserNotFoundException("User authentication data not found", ErrorCodes.UserAuthNotFound);
                }

                if (isCurrentToken)
                {
                    userAuth.PreviousRefreshToken = auth.RefreshToken;
                    userAuth.PreviousRefreshTokenExpiry = previousTokenGracePeriod;
                }

                userAuth.AccessToken = newAccessToken;
                userAuth.AccessTokenExpiry = accessTokenExpiry;
                userAuth.RefreshToken = newRefreshToken;
                userAuth.RefreshTokenExpiry = refreshTokenExpiry;

                if (userAuth.TokenFamily == null)
                {
                    userAuth.TokenFamily = JwtService.GenerateTokenFamily();
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error refreshing token for user {userId}");
                throw;
            }

            Log.Information($"Token rotated for user {userId}");

            var refreshResponse = new RefreshTokenResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                AccessTokenExpiresAt = accessTokenExpiry,
                RefreshTokenExpiresAt = refreshTokenExpiry
            };

            return refreshResponse;
        }

        private async Task InvalidateAllUserTokensAsync(int userId, CancellationToken cancellationToken = default)
        {
            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var userAuth = await context.Set<UserAuthentication>().FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

                if (userAuth != null)
                {
                    userAuth.AccessToken = null;
                    userAuth.AccessTokenExpiry = null;
                    userAuth.RefreshToken = null;
                    userAuth.RefreshTokenExpiry = null;
                    userAuth.PreviousRefreshToken = null;
                    userAuth.PreviousRefreshTokenExpiry = null;
                    userAuth.TokenFamily = null;

                    await context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    Log.Warning($"All tokens invalidated for user {userId} due to potential token theft");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error invalidating tokens for user {userId}");
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
                var family = new FamilyFunctions().GetFamilyById(user.FamilyId.Value);
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
                Email = user.Email,
                Name = user.Name,
                DisplayName = profile.DisplayName,
                ProfilePictureBase64 = profile.ProfilePictureBase64,
                TimeZone = profile.DefaultTimeZone.ToTimeZoneId(),
                Language = profile.DefaultLanguage.ToLanguageCode(),
                Currency = profile.DefaultCurrency.ToCurrencyCode(),
                Family = familyInfo
            };

            return profileResponse;
        }

        public async Task UpdateUserProfileAsync(UpdateUserSettingsRequest request, CancellationToken cancellationToken = default)
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

            var context = new HomassyDbContext();
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

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} updated their settings");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error updating settings for user {userId}");
                throw;
            }
        }

        public async Task UploadProfilePictureAsync(string profilePictureBase64, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            if (string.IsNullOrWhiteSpace(profilePictureBase64))
            {
                throw new BadRequestException("Profile picture data is required", ErrorCodes.ValidationProfilePictureRequired);
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var profile = await context.Set<UserProfile>().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

                if (profile == null)
                {
                    Log.Warning($"UserProfile not found for user {userId}");
                    throw new UserNotFoundException("UserProfile not found", ErrorCodes.UserProfileNotFound);
                }

                profile.ProfilePictureBase64 = profilePictureBase64;

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} uploaded profile picture");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error uploading profile picture for user {userId}");
                throw;
            }
        }

        public async Task DeleteProfilePictureAsync(CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var profile = await context.Set<UserProfile>().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

                if (profile == null)
                {
                    Log.Warning($"UserProfile not found for user {userId}");
                    throw new UserNotFoundException("UserProfile not found", ErrorCodes.UserProfileNotFound);
                }

                if (string.IsNullOrEmpty(profile.ProfilePictureBase64))
                {
                    throw new BadRequestException("No profile picture to delete", ErrorCodes.UserNoProfilePicture);
                }

                profile.ProfilePictureBase64 = null;

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} deleted profile picture");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error deleting profile picture for user {userId}");
                throw;
            }
        }
        #endregion

        #region Logout
        public async Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var userAuth = await context.Set<UserAuthentication>().FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

                if (userAuth == null)
                {
                    Log.Warning($"UserAuthentication not found for user {userId}");
                    throw new UserNotFoundException("User authentication data not found", ErrorCodes.UserAuthNotFound);
                }

                userAuth.AccessToken = null;
                userAuth.AccessTokenExpiry = null;
                userAuth.RefreshToken = null;
                userAuth.RefreshTokenExpiry = null;
                userAuth.TokenFamily = null;
                userAuth.PreviousRefreshToken = null;
                userAuth.PreviousRefreshTokenExpiry = null;

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error during logout for user {userId}");
                throw;
            }

            Log.Information($"User {userId} logged out");
        }

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
                Name = user.Name,
                DisplayName = profile.DisplayName,
                ProfilePictureBase64 = profile.ProfilePictureBase64,
                TimeZone = profile.DefaultTimeZone.ToTimeZoneId(),
                Language = profile.DefaultLanguage.ToLanguageCode(),
                Currency = profile.DefaultCurrency.ToCurrencyCode()
            };

            return userInfo;
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

            var context = new HomassyDbContext();
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

        #region Email Helpers
        private static async Task SendEmailAsync(EmailTask task)
        {
            var queued = false;

            if (_emailQueueService != null)
            {
                queued = await _emailQueueService.TryQueueEmailAsync(task);
            }

            if (!queued)
            {
                Log.Debug($"Sending email synchronously to {task.Email} (queue unavailable or full)");
                switch (task.Type)
                {
                    case EmailType.Verification:
                        await EmailService.SendVerificationCodeAsync(task.Email, task.Code, task.TimeZone, task.Language);
                        break;
                    case EmailType.Registration:
                        await EmailService.SendRegistrationEmailAsync(task.Email, task.Email, task.Code, task.TimeZone, task.Language);
                        break;
                }
            }
        }
        #endregion
    }
}
