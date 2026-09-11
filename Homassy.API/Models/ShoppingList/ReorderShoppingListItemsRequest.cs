using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    /// <summary>
    /// The manual (aisle) order of a list's items, as the ids in the order they should appear.
    /// Ids that are not on the named list are rejected. A subset is accepted — the rows that are sent
    /// are ordered relative to one another and nothing else is written — but rows left out keep their
    /// stored positions and may end up interleaved, so a client that wants a definite result sends
    /// every item on the list.
    /// </summary>
    public class ReorderShoppingListItemsRequest
    {
        [Required]
        public Guid ShoppingListPublicId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required")]
        public List<Guid> ItemPublicIds { get; set; } = [];
    }
}
