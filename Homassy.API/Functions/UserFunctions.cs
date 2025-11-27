using Homassy.API.Context;
using Homassy.API.Entities;
using Homassy.API.Models.User;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Functions
{
    public class UserFunctions
    {
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            var normalizedEmail = email.ToLowerInvariant().Trim();

            var context = new HomassyDbContext();
            return await context.Users
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted);
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            var context = new HomassyDbContext();
            return await context.Users.FindAsync(userId);
        }

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

        public int? GetUserIdByPublicId(Guid? publicId)
        {
            if (publicId == null)
                return null;

            var context = new HomassyDbContext();
            var user = context.Users
                .AsNoTracking()
                .FirstOrDefault(u => u.PublicId == publicId);

            return user?.Id;
        }
    }
}