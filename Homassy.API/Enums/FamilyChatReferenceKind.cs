namespace Homassy.API.Enums
{
    /// <summary>
    /// What a chat message can point at: a thing the family already has in the app.
    /// </summary>
    /// <remarks>
    /// A deliberately small set, and every member of it maps onto a <see cref="SelectValueType"/>
    /// the API already serves - so picking a product in the composer reads from the same list the
    /// product pickers everywhere else in the app read from, and a reference can be validated
    /// against exactly what the sender was allowed to see.
    /// <para>
    /// Inventory items are not here on purpose. They have no name of their own (they are a product
    /// plus a quantity plus a date), so a chip for one would have to invent a label - and "the milk
    /// that expires on Tuesday" is a sentence, which is what the message itself is for.
    /// </para>
    /// <para>
    /// <b>Numbering is permanent</b>, like every other persisted enum here: the value is what the
    /// <c>Kind</c> column stores.
    /// </para>
    /// </remarks>
    public enum FamilyChatReferenceKind
    {
        Product = 0,
        ShoppingLocation = 1,
        StorageLocation = 2,
        ShoppingList = 3
    }
}
