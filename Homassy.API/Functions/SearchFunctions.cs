using Homassy.API.Constants;
using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Models.Search;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Functions
{
    /// <summary>
    /// The one query behind the command palette: every searchable type, ranked and capped,
    /// answered in a single round trip.
    /// </summary>
    /// <remarks>
    /// Everything but automations is read from the Functions layer's in-memory caches, which is
    /// what makes the endpoint cheap enough to call on every (debounced) keystroke — a warm cache
    /// means no database round trip at all. It is also what makes matching accent-insensitive:
    /// <see cref="StringExtensions.NormalizeForSearch"/> strips diacritics, so a Hungarian term
    /// typed without its accents still finds the row, which a SQL <c>ILIKE</c> would not.
    ///
    /// Ownership scoping is deliberately delegated to the existing <c>Get…ByUserAndFamily</c>
    /// accessors rather than re-expressed here: a second copy of "whose rows are these" is the
    /// kind of duplication that drifts into a leak.
    /// </remarks>
    public class SearchFunctions
    {
        private readonly FunctionsRuntime _runtime;
        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;

        public SearchFunctions(FunctionsRuntime runtime)
        {
            _runtime = runtime;
            _contextFactory = runtime.ContextFactory;
        }

        /// <summary>Shortest query worth answering — one letter matches most of a catalogue.</summary>
        public const int MinQueryLength = 2;

        /// <summary>Hits returned per group unless the caller asks for fewer.</summary>
        public const int DefaultLimit = 5;

        /// <summary>Upper bound on the per-group limit, so one caller cannot ask for the catalogue.</summary>
        public const int MaxLimit = 20;

        /// <summary>
        /// How many matches per type are collected before the limit is applied. Also the point at
        /// which a group stops counting and reports <see cref="SearchResultGroup.HasMore"/> — the
        /// client then says "show all" instead of "show all N", which is what the list page is for.
        /// </summary>
        private const int MaxScan = 50;

        public async Task<GlobalSearchResponse> SearchAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var normalized = query.NormalizeForSearch();
            limit = Math.Clamp(limit, 1, MaxLimit);

            var response = new GlobalSearchResponse { Query = query };
            if (normalized.Length < MinQueryLength)
                return response;

            var productFunctions = new ProductFunctions(_runtime);
            var locationFunctions = new LocationFunctions(_runtime);
            var shoppingListFunctions = new ShoppingListFunctions(_runtime);

            AddGroup(response, SearchResultKind.Product, limit,
                SearchProducts(productFunctions, normalized));
            AddGroup(response, SearchResultKind.InventoryItem, limit,
                SearchInventoryItems(productFunctions, locationFunctions, userId.Value, familyId, normalized));
            AddGroup(response, SearchResultKind.ShoppingList, limit,
                SearchShoppingLists(shoppingListFunctions, productFunctions, userId.Value, familyId, normalized));
            AddGroup(response, SearchResultKind.ShoppingLocation, limit,
                SearchShoppingLocations(locationFunctions, userId.Value, familyId, normalized));
            AddGroup(response, SearchResultKind.StorageLocation, limit,
                SearchStorageLocations(locationFunctions, userId.Value, familyId, normalized));
            AddGroup(response, SearchResultKind.Automation, limit,
                await SearchAutomationsAsync(productFunctions, userId.Value, familyId, normalized, cancellationToken));

            return response;
        }

        #region Per-type searches
        private static List<RankedHit> SearchProducts(ProductFunctions productFunctions, string normalized)
        {
            // Catalogue-wide, not "what this family has stock of": the commonest thing to look up
            // is something you have run out of. A Product carries no owner, so there is nothing to
            // scope it by — the same reasoning as the chat's attachment picker.
            var hits = new List<RankedHit>();

            foreach (var product in productFunctions.GetCatalogProducts())
            {
                var rank = BestRank(normalized, product.Name, product.Brand, product.Barcode);
                if (rank < 0) continue;

                hits.Add(new RankedHit(rank, product.Name, new SearchResultItem
                {
                    PublicId = product.PublicId,
                    Kind = SearchResultKind.Product,
                    Title = product.Name,
                    Subtitle = product.Brand,
                    ImageUrl = MediaUrls.ProductImage(product.PublicId, product.ProductPictureVersion)
                }));

                if (hits.Count > MaxScan) break;
            }

            return hits;
        }

        private static List<RankedHit> SearchInventoryItems(
            ProductFunctions productFunctions,
            LocationFunctions locationFunctions,
            int userId,
            int? familyId,
            string normalized)
        {
            var hits = new List<RankedHit>();

            foreach (var item in productFunctions.GetInventoryItemsByUserAndFamily(userId, familyId))
            {
                var product = productFunctions.GetProductById(item.ProductId);
                if (product == null) continue;

                var rank = BestRank(normalized, product.Name, product.Brand);
                if (rank < 0) continue;

                var storageLocation = locationFunctions.GetStorageLocationById(item.StorageLocationId);

                hits.Add(new RankedHit(rank, product.Name, new SearchResultItem
                {
                    PublicId = item.PublicId,
                    Kind = SearchResultKind.InventoryItem,
                    Title = product.Name,
                    Subtitle = storageLocation?.Name ?? product.Brand,
                    ImageUrl = MediaUrls.ProductImage(product.PublicId, product.ProductPictureVersion),
                    Color = storageLocation?.Color,
                    // An inventory item has no page of its own — it is opened from its product.
                    ParentPublicId = product.PublicId
                }));

                if (hits.Count > MaxScan) break;
            }

            return hits;
        }

        private static List<RankedHit> SearchShoppingLists(
            ShoppingListFunctions shoppingListFunctions,
            ProductFunctions productFunctions,
            int userId,
            int? familyId,
            string normalized)
        {
            var hits = new List<RankedHit>();

            // A list matches on its own name, and also on what is still on it: "milk" should find
            // the list milk is waiting on, which is the question a palette is usually asked.
            var items = familyId.HasValue
                ? shoppingListFunctions.GetShoppingListItemsByFamilyId(familyId.Value)
                : shoppingListFunctions.GetShoppingListItemsByUserId(userId);

            var matchedItemByListId = new Dictionary<int, (int Rank, string Label)>();

            foreach (var item in items)
            {
                if (item.PurchasedAt.HasValue) continue;

                var label = item.CustomName ?? productFunctions.GetProductById(item.ProductId)?.Name;
                if (string.IsNullOrWhiteSpace(label)) continue;

                var itemRank = BestRank(normalized, label);
                if (itemRank < 0) continue;

                if (!matchedItemByListId.TryGetValue(item.ShoppingListId, out var existing) || itemRank < existing.Rank)
                    matchedItemByListId[item.ShoppingListId] = (itemRank, label);
            }

            foreach (var list in shoppingListFunctions.GetShoppingListsByUserAndFamily(userId, familyId))
            {
                var nameRank = BestRank(normalized, list.Name);
                var hasItemMatch = matchedItemByListId.TryGetValue(list.Id, out var itemMatch);

                if (nameRank < 0 && !hasItemMatch) continue;

                // A name match wins the ordering; an item-only match is a weaker signal, so it is
                // pushed behind every name match rather than competing with them.
                var rank = nameRank >= 0 ? nameRank : itemMatch.Rank + RankCount;

                hits.Add(new RankedHit(rank, list.Name, new SearchResultItem
                {
                    PublicId = list.PublicId,
                    Kind = SearchResultKind.ShoppingList,
                    Title = list.Name,
                    Subtitle = hasItemMatch ? itemMatch.Label : list.Description,
                    Color = list.Color
                }));

                if (hits.Count > MaxScan) break;
            }

            return hits;
        }

        private static List<RankedHit> SearchShoppingLocations(
            LocationFunctions locationFunctions,
            int userId,
            int? familyId,
            string normalized)
        {
            var hits = new List<RankedHit>();

            foreach (var location in locationFunctions.GetShoppingLocationsByUserAndFamily(userId, familyId))
            {
                var rank = BestRank(normalized, location.Name, location.City, location.Address);
                if (rank < 0) continue;

                hits.Add(new RankedHit(rank, location.Name, new SearchResultItem
                {
                    PublicId = location.PublicId,
                    Kind = SearchResultKind.ShoppingLocation,
                    Title = location.Name,
                    Subtitle = location.City ?? location.Address,
                    Color = location.Color
                }));

                if (hits.Count > MaxScan) break;
            }

            return hits;
        }

        private static List<RankedHit> SearchStorageLocations(
            LocationFunctions locationFunctions,
            int userId,
            int? familyId,
            string normalized)
        {
            var hits = new List<RankedHit>();

            foreach (var location in locationFunctions.GetStorageLocationsByUserAndFamily(userId, familyId))
            {
                var rank = BestRank(normalized, location.Name, location.Description);
                if (rank < 0) continue;

                hits.Add(new RankedHit(rank, location.Name, new SearchResultItem
                {
                    PublicId = location.PublicId,
                    Kind = SearchResultKind.StorageLocation,
                    Title = location.Name,
                    Subtitle = location.Description,
                    Color = location.Color
                }));

                if (hits.Count > MaxScan) break;
            }

            return hits;
        }

        /// <summary>
        /// Automations are the one searchable type with no cache, so this is the endpoint's only
        /// query. It starts from the product ids the term already matched in memory and looks them
        /// up on <c>ItemAutomations.ProductId</c>, which is indexed — no scan of the automation
        /// table, and nothing to match in SQL.
        /// </summary>
        private async Task<List<RankedHit>> SearchAutomationsAsync(
            ProductFunctions productFunctions,
            int userId,
            int? familyId,
            string normalized,
            CancellationToken cancellationToken)
        {
            var matchedProducts = productFunctions.GetCatalogProducts()
                .Select(p => new { p.Id, Rank = BestRank(normalized, p.Name, p.Brand), Product = p })
                .Where(x => x.Rank >= 0)
                .OrderBy(x => x.Rank)
                .Take(MaxScan)
                .ToList();

            if (matchedProducts.Count == 0)
                return [];

            var productIds = matchedProducts.Select(x => x.Id).ToList();

            using var context = _contextFactory.CreateForReading();

            var query = context.ItemAutomations
                .Where(a => a.ProductId.HasValue && productIds.Contains(a.ProductId.Value));

            query = familyId.HasValue
                ? query.Where(a => a.UserId == userId || a.FamilyId == familyId)
                : query.Where(a => a.UserId == userId);

            var automations = await query
                .OrderByDescending(a => a.IsEnabled)
                .Take(MaxScan + 1)
                .ToListAsync(cancellationToken);

            var matchByProductId = matchedProducts.ToDictionary(x => x.Id, x => x);

            return automations
                .Where(a => matchByProductId.ContainsKey(a.ProductId!.Value))
                .Select(a =>
                {
                    var match = matchByProductId[a.ProductId!.Value];
                    return new RankedHit(match.Rank, match.Product.Name, new SearchResultItem
                    {
                        PublicId = a.PublicId,
                        Kind = SearchResultKind.Automation,
                        Title = match.Product.Name,
                        Subtitle = match.Product.Brand,
                        ImageUrl = MediaUrls.ProductImage(match.Product.PublicId, match.Product.ProductPictureVersion)
                    });
                })
                .ToList();
        }
        #endregion

        #region Ranking
        /// <summary>A hit plus what orders it: the rank first, then the title, alphabetically.</summary>
        private readonly record struct RankedHit(int Rank, string SortKey, SearchResultItem Item);

        /// <summary>
        /// How many rank buckets <see cref="RankOf"/> can return — the offset by which a shopping
        /// list that only matched through one of its items is pushed behind the name matches.
        /// </summary>
        private const int RankCount = 4;

        /// <summary>The best (lowest) rank the term reaches across any of the given fields, or -1 for no match.</summary>
        private static int BestRank(string normalizedQuery, params string?[] fields)
        {
            var best = -1;

            foreach (var field in fields)
            {
                var rank = RankOf(normalizedQuery, field);
                if (rank < 0) continue;
                if (best < 0 || rank < best) best = rank;
                if (best == 0) break;
            }

            return best;
        }

        /// <summary>
        /// 0 exact, 1 prefix, 2 word prefix, 3 anywhere, -1 no match. Lower sorts first, so typing
        /// "mil" puts "Milk" above "Buttermilk" without the client having to re-rank.
        /// </summary>
        private static int RankOf(string normalizedQuery, string? field)
        {
            var haystack = field.NormalizeForSearch();
            if (haystack.Length == 0) return -1;

            if (haystack == normalizedQuery) return 0;
            if (haystack.StartsWith(normalizedQuery, StringComparison.Ordinal)) return 1;

            var at = haystack.IndexOf(normalizedQuery, StringComparison.Ordinal);
            if (at < 0) return -1;

            // A match that starts a word reads as a hit; one inside a word is noise.
            return char.IsLetterOrDigit(haystack[at - 1]) ? 3 : 2;
        }

        private static void AddGroup(
            GlobalSearchResponse response,
            SearchResultKind kind,
            int limit,
            List<RankedHit> hits)
        {
            if (hits.Count == 0) return;

            response.Groups.Add(new SearchResultGroup
            {
                Kind = kind,
                TotalCount = Math.Min(hits.Count, MaxScan),
                HasMore = hits.Count > MaxScan,
                Items = hits
                    .OrderBy(h => h.Rank)
                    .ThenBy(h => h.SortKey, StringComparer.CurrentCultureIgnoreCase)
                    .Take(limit)
                    .Select(h => h.Item)
                    .ToList()
            });
        }
        #endregion
    }
}
