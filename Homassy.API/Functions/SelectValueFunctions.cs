using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Models.Common;
using Serilog;

namespace Homassy.API.Functions
{
    public class SelectValueFunctions
    {
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
                SelectValueType.ProductInventoryItem => GetProductInventoryItemSelectValues(userId.Value, familyId),
                SelectValueType.ShoppingList => GetShoppingListSelectValues(userId.Value, familyId),
                SelectValueType.Languages => GetLanguagesSelectValues(),
                SelectValueType.Currencies => GetCurrenciesSelectValues(),
                SelectValueType.TimeZones => GetTimeZonesSelectValues(),
                _ => throw new BadRequestException($"Invalid select value type: {type}")
            };
        }

        private static List<SelectValue> GetShoppingLocationSelectValues(int userId, int? familyId)
        {
            var locationFunctions = new LocationFunctions();
            var locations = locationFunctions.GetShoppingLocationsByUserAndFamily(userId, familyId);

            return locations
                .Select(l => new SelectValue
                {
                    PublicId = l.PublicId,
                    Text = l.Name
                })
                .OrderBy(s => s.Text)
                .ToList();
        }

        private static List<SelectValue> GetStorageLocationSelectValues(int userId, int? familyId)
        {
            var locationFunctions = new LocationFunctions();
            var locations = locationFunctions.GetStorageLocationsByUserAndFamily(userId, familyId);

            return locations
                .Select(l => new SelectValue
                {
                    PublicId = l.PublicId,
                    Text = l.Name
                })
                .OrderBy(s => s.Text)
                .ToList();
        }

        private static List<SelectValue> GetProductSelectValues(int userId, int? familyId)
        {
            var productFunctions = new ProductFunctions();
            var products = productFunctions.GetProductsByUserAndFamily(userId, familyId);

            return products
                .Select(p => new SelectValue
                {
                    PublicId = p.PublicId,
                    Text = $"{p.Brand} - {p.Name}"
                })
                .OrderBy(s => s.Text)
                .ToList();
        }

        private static List<SelectValue> GetProductInventoryItemSelectValues(int userId, int? familyId)
        {
            var productFunctions = new ProductFunctions();
            var inventoryItems = productFunctions.GetInventoryItemsByUserAndFamily(userId, familyId);

            return inventoryItems
                .Select(i =>
                {
                    var product = productFunctions.GetProductById(i.ProductId);
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

        private static List<SelectValue> GetShoppingListSelectValues(int userId, int? familyId)
        {
            var shoppingListFunctions = new ShoppingListFunctions();
            var shoppingLists = shoppingListFunctions.GetShoppingListsByUserAndFamily(userId, familyId);

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
    }
}
