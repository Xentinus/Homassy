using Homassy.API.Hubs;
using Microsoft.EntityFrameworkCore;
using Homassy.Data.Context;

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
    /// It exists so that adding one more broadcast to one class does not re-cascade a constructor
    /// parameter through every class that takes it. Without it each class along the way would
    /// declare the union of everything its callees need — eight classes with four or five
    /// parameters each, re-cascading the moment one more of them broadcasts.
    ///
    /// It used to have a second job, which it no longer has: the layer constructed itself with
    /// <c>new</c> at 143 sites because <c>UserFunctions</c> ↔ <c>FamilyFunctions</c> and
    /// <c>ProductFunctions</c> ↔ <c>AutomationFunctions</c> were mutually dependent, and one
    /// argument kept those <c>new</c> sites stable. Those cycles are gone (#133) — see
    /// <c>FamilyCache</c> and <c>LowStockAutomationFunctions</c> — and the classes take each
    /// other through their constructors now.
    ///
    /// A class that needs nothing but a context — <c>ActivityFunctions</c>,
    /// <c>FamilyFunctions</c>, <c>FamilyCache</c>, <c>FamilyJoinRequestFunctions</c>,
    /// <c>PushNotificationFunctions</c>, <c>UserFunctions</c> — deliberately takes
    /// <see cref="IDbContextFactory{TContext}"/> directly, which keeps them usable from a host
    /// that has no hubs.
    /// </remarks>
    public sealed class FunctionsRuntime
    {
        public FunctionsRuntime(
            IDbContextFactory<HomassyDbContext> contextFactory,
            IServiceScopeFactory scopeFactory,
            InventoryRealtime inventory,
            MasterDataRealtime masterData,
            ShoppingListRealtime shoppingList,
            FamilyChatRealtime familyChat)
        {
            ContextFactory = contextFactory;
            ScopeFactory = scopeFactory;
            Inventory = inventory;
            MasterData = masterData;
            ShoppingList = shoppingList;
            FamilyChat = familyChat;
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

        public FamilyChatRealtime FamilyChat { get; }
    }
}
