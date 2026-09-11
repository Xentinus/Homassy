using Homassy.API.Functions;

namespace Homassy.Tests.Unit;

/// <summary>
/// The manual-order scheme behind drag-and-drop reordering (#113). The property that matters is the
/// one the endpoint's cost depends on: a single move must plan a single row write, and the planned
/// values must always put the rows in the order that was asked for.
/// </summary>
public class SparseOrderingTests
{
    /// <summary>
    /// Applies a plan to the values it was planned from and returns what the rows would then hold, so
    /// a test can assert on the resulting order rather than on the plan's internals.
    /// </summary>
    private static List<int> Apply(IReadOnlyList<int> currentOrders, IReadOnlyDictionary<int, int> changes)
    {
        var result = currentOrders.ToList();
        foreach (var (index, sortOrder) in changes)
        {
            result[index] = sortOrder;
        }
        return result;
    }

    private static void AssertStrictlyIncreasing(IReadOnlyList<int> values)
    {
        for (var i = 1; i < values.Count; i++)
        {
            Assert.True(values[i] > values[i - 1],
                $"Position {i} ({values[i]}) must be greater than position {i - 1} ({values[i - 1]})");
        }
    }

    #region Append

    [Fact]
    public void Append_EmptyList_StartsAtOneGap()
    {
        Assert.Equal(SparseOrdering.Gap, SparseOrdering.Append(null));
    }

    [Fact]
    public void Append_NonEmptyList_LandsOneGapPastTheHighest()
    {
        Assert.Equal(4000, SparseOrdering.Append(3000));
    }

    [Fact]
    public void Append_AtTheCeiling_SaturatesInsteadOfWrapping()
    {
        // Wrapping would put the new row at the *front* of the list, which is the one outcome an
        // append must never produce.
        Assert.Equal(int.MaxValue, SparseOrdering.Append(int.MaxValue - 1));
    }

    #endregion

    #region PlanReorder — cost

    [Fact]
    public void PlanReorder_UnchangedOrder_WritesNothing()
    {
        var changes = SparseOrdering.PlanReorder([1000, 2000, 3000, 4000]);

        Assert.Empty(changes);
    }

    [Fact]
    public void PlanReorder_EmptyList_WritesNothing()
    {
        Assert.Empty(SparseOrdering.PlanReorder([]));
    }

    [Fact]
    public void PlanReorder_MoveOneRowDown_WritesOneRow()
    {
        // [A B C D] with A dragged between C and D.
        var currentOrders = new[] { 2000, 3000, 1000, 4000 };

        var changes = SparseOrdering.PlanReorder(currentOrders);

        Assert.Single(changes);
        Assert.True(changes.ContainsKey(2));
        AssertStrictlyIncreasing(Apply(currentOrders, changes));
    }

    [Fact]
    public void PlanReorder_MoveOneRowToTheFront_WritesOneRow()
    {
        // The head is the case a naive left-to-right pass gets wrong: it would keep the dragged row's
        // old value and rewrite everything after it instead.
        var currentOrders = new[] { 4000, 1000, 2000, 3000 };

        var changes = SparseOrdering.PlanReorder(currentOrders);

        Assert.Single(changes);
        Assert.True(changes.ContainsKey(0));
        AssertStrictlyIncreasing(Apply(currentOrders, changes));
    }

    [Fact]
    public void PlanReorder_MoveOneRowToTheEnd_WritesOneRow()
    {
        var currentOrders = new[] { 2000, 3000, 4000, 1000 };

        var changes = SparseOrdering.PlanReorder(currentOrders);

        Assert.Single(changes);
        Assert.True(changes.ContainsKey(3));
        AssertStrictlyIncreasing(Apply(currentOrders, changes));
    }

    [Fact]
    public void PlanReorder_ListThatHasNeverBeenDragged_NumbersItFromScratch()
    {
        // Every pre-existing row is at 0 (the migration's default), so the first drag is the one
        // reorder that genuinely has to write the whole list.
        var currentOrders = new[] { 0, 0, 0, 0 };

        var changes = SparseOrdering.PlanReorder(currentOrders);

        var result = Apply(currentOrders, changes);
        AssertStrictlyIncreasing(result);
        Assert.Equal(3, changes.Count);
    }

    #endregion

    #region PlanReorder — correctness

    [Fact]
    public void PlanReorder_ReversedList_ProducesTheRequestedOrder()
    {
        var currentOrders = new[] { 5000, 4000, 3000, 2000, 1000 };

        var changes = SparseOrdering.PlanReorder(currentOrders);

        AssertStrictlyIncreasing(Apply(currentOrders, changes));
    }

    [Fact]
    public void PlanReorder_ExhaustedGap_RenumbersTheWholeList()
    {
        // Two positions left adjacent by a long run of moves into the same spot, with a row asked to
        // land between them: there is no integer to give it, so the list is renumbered onto fresh gaps
        // instead. Only a run *between* two kept rows can run out of room — the two ends of the list
        // always have somewhere to go.
        var currentOrders = new[] { 1000, 5000, 1001 };

        var changes = SparseOrdering.PlanReorder(currentOrders);

        Assert.Equal(currentOrders.Length, changes.Count);
        var result = Apply(currentOrders, changes);
        AssertStrictlyIncreasing(result);
        Assert.Equal(new[] { 1000, 2000, 3000 }, result);
    }

    [Fact]
    public void PlanReorder_MoveToFrontOfTheLowestPossiblePosition_RenumbersRatherThanUnderflowing()
    {
        var currentOrders = new[] { 1000, int.MinValue };

        var changes = SparseOrdering.PlanReorder(currentOrders);

        AssertStrictlyIncreasing(Apply(currentOrders, changes));
    }

    [Fact]
    public void PlanReorder_MoveToEndOfTheHighestPossiblePosition_RenumbersRatherThanOverflowing()
    {
        var currentOrders = new[] { int.MaxValue, 1000 };

        var changes = SparseOrdering.PlanReorder(currentOrders);

        AssertStrictlyIncreasing(Apply(currentOrders, changes));
    }

    [Theory]
    [InlineData(7, 11)]
    [InlineData(20, 3)]
    [InlineData(50, 97)]
    public void PlanReorder_ManyRandomOrders_AlwaysProduceTheRequestedOrder(int size, int seed)
    {
        var random = new Random(seed);
        var orders = Enumerable.Range(1, size).Select(i => i * SparseOrdering.Gap).ToList();

        // Repeated shuffles, each planned against the positions the previous one left behind — this is
        // what drives values into the tight gaps the renumber path exists for.
        for (var round = 0; round < 25; round++)
        {
            orders = orders.OrderBy(_ => random.Next()).ToList();
            var changes = SparseOrdering.PlanReorder(orders);
            orders = Apply(orders, changes);
            AssertStrictlyIncreasing(orders);
        }
    }

    #endregion
}
