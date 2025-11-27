using Homassy.API.Context;
using Homassy.API.Entities;
using Homassy.API.Models.User;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Functions
{
    public class UserFunctions
    {
        private static readonly ConcurrentDictionary<int, User> _userCache = new();
        public static bool Inited = false;

        #region Cache Management
        public async Task InitializeCacheAsync()
        {
            var context = new HomassyDbContext();
            var users = await context.Users.AsNoTracking().ToListAsync();

            try
            {
                foreach (var user in users)
                {
                    _userCache[user.Id] = user;
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

        public async Task RefreshCacheAsync(int userId)
        {
            try
            {
                var context = new HomassyDbContext();
                var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
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
                user = context.Users.AsNoTracking().FirstOrDefault(u => u.Id == userId);
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
                user = context.Users.AsNoTracking().FirstOrDefault(u => u.PublicId == publicId);
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
                user = context.Users.AsNoTracking().FirstOrDefault(u => u.Email == normalizedEmail);
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
                    .AsNoTracking()
                    .Where(u => missingIds.Contains(u.Id))
                    .ToList();

                result.AddRange(dbUsers);
            }

            return result;
        }
        #endregion

        public async Task<User> CreateUserAsync(CreateUserRequest request)
        {
            var normalizedEmail = request.Email.ToLowerInvariant().Trim();

            var user = new User
            {
                Email = normalizedEmail,
                Name = request.Name.Trim(),
                DisplayName = request.DisplayName?.Trim() ?? request.Name.Trim(),
            };

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Users.Add(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return user;
        }

        public async Task SetVerificationCodeAsync(User user, string code, DateTime expiry)
        {
            user.VerificationCode = code;
            user.VerificationCodeExpiry = expiry;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task ClearVerificationCodeAsync(User user)
        {
            user.VerificationCode = null;
            user.VerificationCodeExpiry = null;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task SetRefreshTokenAsync(User user, string refreshToken, DateTime expiry)
        {
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = expiry;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task ClearRefreshTokenAsync(User user)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateLastLoginAsync(User user)
        {
            user.LastLoginAt = DateTime.UtcNow;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task CompleteAuthenticationAsync(User user, string refreshToken, DateTime refreshTokenExpiry)
        {
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = refreshTokenExpiry;
            user.VerificationCode = null;
            user.VerificationCodeExpiry = null;
            user.LastLoginAt = DateTime.UtcNow;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public bool IsVerificationCodeValid(User user, string code)
        {
            return user.VerificationCodeExpiry.HasValue &&
                   user.VerificationCodeExpiry > DateTime.UtcNow &&
                   !string.IsNullOrEmpty(user.VerificationCode);
        }

        public bool IsRefreshTokenValid(User user)
        {
            return user.RefreshTokenExpiry.HasValue &&
                   user.RefreshTokenExpiry > DateTime.UtcNow &&
                   !string.IsNullOrEmpty(user.RefreshToken);
        }

        public async Task SetEmailVerifiedAsync(User user)
        {
            user.IsEmailVerified = true;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateUserSettingsAsync(User user, UpdateUserSettingsRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                user.Email = request.Email.ToLowerInvariant().Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                user.Name = request.Name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                user.DisplayName = request.DisplayName.Trim();
            }

            if (request.DefaultCurrency.HasValue)
            {
                user.DefaultCurrency = request.DefaultCurrency.Value;
            }

            if (request.DefaultTimeZone.HasValue)
            {
                user.DefaultTimeZone = request.DefaultTimeZone.Value;
            }

            if (request.DefaultLanguage.HasValue)
            {
                user.DefaultLanguage = request.DefaultLanguage.Value;
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UploadProfilePictureAsync(User user, string profilePictureBase64)
        {
            user.ProfilePictureBase64 = profilePictureBase64;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteProfilePictureAsync(User user)
        {
            user.ProfilePictureBase64 = null;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}