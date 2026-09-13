using Homassy.API.Context;
using Homassy.Data.Enums;
using Homassy.Data.Exceptions;
using Homassy.API.Extensions;
using Homassy.Data.Models.Common;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Homassy.Data.Context;
using Homassy.Data.Extensions;

namespace Homassy.API.Functions
{
    public class SelectValueFunctions
    {
        private readonly FunctionsRuntime _runtime;
        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;
        private readonly LocationFunctions _locationFunctions;
        private readonly ProductFunctions _productFunctions;
        private readonly ShoppingListFunctions _shoppingListFunctions;

        public SelectValueFunctions(FunctionsRuntime runtime, LocationFunctions locationFunctions, ProductFunctions productFunctions, ShoppingListFunctions shoppingListFunctions)
        {
            _runtime = runtime;
            _contextFactory = runtime.ContextFactory;
            _locationFunctions = locationFunctions;
            _productFunctions = productFunctions;
            _shoppingListFunctions = shoppingListFunctions;
        }

        public List<SelectValue> GetSelectValues(SelectValueType type)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();

            return type switch
            {
                SelectValueType.ShoppingLocation => GetShoppingLocationSelectValues(userId.Value, familyId),
                SelectValueType.StorageLocation => GetStorageLocationSelectValues(userId.Value, familyId),
                SelectValueType.Product => GetProductSelectValues(userId.Value, familyId),
                SelectValueType.ProductCatalog => GetProductCatalogSelectValues(),
                SelectValueType.ProductInventoryItem => GetProductInventoryItemSelectValues(userId.Value, familyId),
                SelectValueType.ShoppingList => GetShoppingListSelectValues(userId.Value, familyId),
                SelectValueType.Languages => GetLanguagesSelectValues(),
                SelectValueType.Currencies => GetCurrenciesSelectValues(),
                SelectValueType.TimeZones => GetTimeZonesSelectValues(),
                SelectValueType.ProductCategory => GetProductCategorySelectValues(),
                _ => throw new BadRequestException($"Invalid select value type: {type}")
            };
        }

        private List<SelectValue> GetShoppingLocationSelectValues(int userId, int? familyId)
        {
            var locations = _locationFunctions.GetShoppingLocationsByUserAndFamily(userId, familyId);

            return locations
                .Select(l => new SelectValue
                {
                    PublicId = l.PublicId,
                    Text = l.Name
                })
                .OrderBy(s => s.Text)
                .ToList();
        }

        private List<SelectValue> GetStorageLocationSelectValues(int userId, int? familyId)
        {
            var locations = _locationFunctions.GetStorageLocationsByUserAndFamily(userId, familyId);

            return locations
                .Select(l => new SelectValue
                {
                    PublicId = l.PublicId,
                    Text = l.Name
                })
                .OrderBy(s => s.Text)
                .ToList();
        }

        private List<SelectValue> GetProductSelectValues(int userId, int? familyId)
        {
            var products = _productFunctions.GetProductsByUserAndFamily(userId, familyId);

            return products
                .Select(p => new SelectValue
                {
                    PublicId = p.PublicId,
                    Text = $"{p.Brand} - {p.Name}"
                })
                .OrderBy(s => s.Text)
                .ToList();
        }

        private List<SelectValue> GetProductCatalogSelectValues()
        {
            var products = _productFunctions.GetCatalogProducts();

            return products
                .Select(p => new SelectValue
                {
                    PublicId = p.PublicId,
                    Text = $"{p.Brand} - {p.Name}"
                })
                .OrderBy(s => s.Text)
                .ToList();
        }

        private List<SelectValue> GetProductInventoryItemSelectValues(int userId, int? familyId)
        {
            var inventoryItems = _productFunctions.GetInventoryItemsByUserAndFamily(userId, familyId);

            return inventoryItems
                .Select(i =>
                {
                    var product = _productFunctions.GetProductById(i.ProductId);
                    var text = product != null
                        ? $"{product.Brand} - {product.Name}"
                        : $"Item {i.PublicId}";

                    return new SelectValue
                    {
                        PublicId = i.PublicId,
                        Text = text
                    };
                })
                .OrderBy(s => s.Text)
                .ToList();
        }

        private List<SelectValue> GetShoppingListSelectValues(int userId, int? familyId)
        {
            var shoppingLists = _shoppingListFunctions.GetShoppingListsByUserAndFamily(userId, familyId);

            return shoppingLists
                .Select(sl => new SelectValue
                {
                    PublicId = sl.PublicId,
                    Text = sl.Name
                })
                .OrderBy(s => s.Text)
                .ToList();
        }

        private static List<SelectValue> GetLanguagesSelectValues()
        {
            return Enum.GetValues(typeof(Language))
                       .Cast<Language>()
                       .Select(lang =>
                       new SelectValue
                       {
                           PublicId = Guid.NewGuid(),
                           Text = LanguageExtensions.ToLanguageCode(lang)
                       })
                       .OrderBy(s => s.Text)
                       .ToList();
        }

        private static List<SelectValue> GetCurrenciesSelectValues()
        {
            return Enum.GetValues(typeof(Currency))
                       .Cast<Currency>()
                       .Select(currency =>
                       new SelectValue
                       {
                           PublicId = Guid.NewGuid(),
                           Text = CurrencyExtensions.ToCurrencyCode(currency)
                       })
                       .OrderBy(s => s.Text)
                       .ToList();
        }

        private static List<SelectValue> GetTimeZonesSelectValues()
        {
            return Enum.GetValues(typeof(UserTimeZone))
                       .Cast<UserTimeZone>()
                       .Select(timezone =>
                       new SelectValue
                       {
                           PublicId = Guid.NewGuid(),
                           Text = UserTimeZoneExtensions.ToTimeZoneId(timezone)
                       })
                       .OrderBy(s => s.Text)
                       .ToList();
        }

        private static List<SelectValue> GetProductCategorySelectValues()
        {
            return Enum.GetValues(typeof(ProductCategory))
                       .Cast<ProductCategory>()
                       .Select(category =>
                       new SelectValue
                       {
                           PublicId = Guid.NewGuid(),
                           Text = ((int)category).ToString()
                       })
                       .OrderBy(s => int.Parse(s.Text))
                       .ToList();
        }
    }
}
