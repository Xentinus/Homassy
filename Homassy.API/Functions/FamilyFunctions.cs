using Homassy.API.Context;
using Homassy.API.Entities;
using Homassy.API.Models.Family;
using Microsoft.EntityFrameworkCore;

namespace Homassy.API.Functions
{
    public class FamilyFunctions
    {
        public async Task<Family> CreateFamilyAsync(CreateFamilyRequest request)
        {
            var family = new Family
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                FamilyPictureBase64 = request.FamilyPictureBase64
            };

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Families.Add(family);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return family;
        }

        public async Task<Family?> GetFamilyByIdAsync(int familyId)
        {
            var context = new HomassyDbContext();
            return await context.Families
                .FirstOrDefaultAsync(f => f.Id == familyId);
        }

        public async Task<Family?> GetFamilyByShareCodeAsync(string shareCode)
        {
            var normalizedShareCode = shareCode.ToUpperInvariant().Trim();

            var context = new HomassyDbContext();
            return await context.Families
                .FirstOrDefaultAsync(f => f.ShareCode == normalizedShareCode);
        }

        public async Task AddUserToFamilyAsync(User user, int familyId)
        {
            user.FamilyId = familyId;

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

        public async Task RemoveUserFromFamilyAsync(User user)
        {
            user.FamilyId = null;

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

        public async Task UpdateFamilyAsync(Family family, UpdateFamilyRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                family.Name = request.Name.Trim();
            }

            if (request.Description != null)
            {
                family.Description = string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : request.Description.Trim();
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(family);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UploadFamilyPictureAsync(Family family, string familyPictureBase64)
        {
            family.FamilyPictureBase64 = familyPictureBase64;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(family);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteFamilyPictureAsync(Family family)
        {
            family.FamilyPictureBase64 = null;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(family);
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
