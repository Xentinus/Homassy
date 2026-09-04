using Homassy.API.Context;
using Homassy.API.Hubs;
using Microsoft.EntityFrameworkCore;

namespace Homassy.API.Functions
{
    /// <summary>
    /// The process-wide services a <c>Functions</c> class reaches for: a source of database
    /// contexts, the SignalR broadcast helpers, and a scope factory for the work that outlives
    /// the request that started it.
    /// </summary>
    /// <remarks>
    /// This is a parameter object, not a service locator — every member is a declared, typed
    /// dependency and nothing can be pulled out of it that is not listed here.
    ///
    /// It exists because the layer instantiates itself: <c>UserFunctions</c> ↔
    /// <c>FamilyFunctions</c> and <c>ProductFunctions</c> ↔ <c>AutomationFunctions</c> are
    /// mutually dependent, so those classes cannot take each other through the constructor and
    /// create each other with <c>new</c> instead. Passing the dependencies one by one would mean
    /// every class along the way declaring the union of everything its callees need — eight
    /// classes with four or five parameters each, re-cascading the moment one more of them
    /// broadcasts. One argument keeps the internal <c>new</c> sites stable.
    ///
    /// A class that needs nothing but a context — <c>ActivityFunctions</c>,
    /// <c>FamilyFunctions</c>, <c>FamilyJoinRequestFunctions</c>,
    /// <c>PushNotificationFunctions</c>, <c>UserFunctions</c> — deliberately takes
    /// <see cref="IDbContextFactory{TContext}"/> directly. Those five only ever construct each
    /// other, so the set is closed, and it keeps them usable from a host that has no hubs (the
    /// notifications service borrows exactly those two).
    /// </remarks>
    public sealed class FunctionsRuntime
    {
        public FunctionsRuntime(
            IDbContextFactory<HomassyDbContext> contextFactory,
            IServiceScopeFactory scopeFactory,
            InventoryRealtime inventory,
            MasterDataRealtime masterData,
            ShoppingListRealtime shoppingList)
        {
            ContextFactory = contextFactory;
            ScopeFactory = scopeFactory;
            Inventory = inventory;
            MasterData = masterData;
            ShoppingList = shoppingList;
        }

        /// <summary>One context per operation; see the two rules in <c>Homassy.API/CLAUDE.md</c>.</summary>
        public IDbContextFactory<HomassyDbContext> ContextFactory { get; }

        /// <summary>
        /// For work that must not borrow the request's scope, because it outlives it — the
        /// fire-and-forget low-stock notification in <see cref="AutomationFunctions"/>.
        /// </summary>
        public IServiceScopeFactory ScopeFactory { get; }

        public InventoryRealtime Inventory { get; }

        public MasterDataRealtime MasterData { get; }

        public ShoppingListRealtime ShoppingList { get; }
    }
}
