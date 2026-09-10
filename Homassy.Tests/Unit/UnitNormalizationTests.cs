using Homassy.API.Functions;
using ProductUnit = Homassy.API.Enums.Unit;

namespace Homassy.Tests.Unit;

public class UnitNormalizationTests
{
    #region Mass and Volume - convertible families

    [Fact]
    public void Normalize_FiveHundredGrams_ConvertsToPointFiveKilogramAndIsNormalized()
    {
        var result = UnitNormalization.Normalize(500m, ProductUnit.Gram);

        Assert.Equal(0.5m, result.Value);
        Assert.Equal(ProductUnit.Kilogram, result.CanonicalUnit);
        Assert.Equal(NormalizationBasis.Mass, result.Basis);
        Assert.True(result.Normalized);
    }

    [Fact]
    public void Normalize_TwoKilograms_PassesThroughAsItsOwnCanonicalUnitAndIsNormalized()
    {
        var result = UnitNormalization.Normalize(2m, ProductUnit.Kilogram);

        Assert.Equal(2m, result.Value);
        Assert.Equal(ProductUnit.Kilogram, result.CanonicalUnit);
        Assert.Equal(NormalizationBasis.Mass, result.Basis);
        Assert.True(result.Normalized);
    }

    [Fact]
    public void Normalize_FifteenHundredMilliliters_ConvertsToOnePointFiveLiterAndIsNormalized()
    {
        var result = UnitNormalization.Normalize(1500m, ProductUnit.Milliliter);

        Assert.Equal(1.5m, result.Value);
        Assert.Equal(ProductUnit.Liter, result.CanonicalUnit);
        Assert.Equal(NormalizationBasis.Volume, result.Basis);
        Assert.True(result.Normalized);
    }

    #endregion

    #region Countable - no conversion to Mass or Volume exists

    [Fact]
    public void Normalize_ThreePacks_PassesThroughUnchangedAsCountableAndIsNotNormalized()
    {
        var result = UnitNormalization.Normalize(3m, ProductUnit.Pack);

        Assert.Equal(3m, result.Value);
        Assert.Equal(ProductUnit.Pack, result.CanonicalUnit);
        Assert.Equal(NormalizationBasis.Countable, result.Basis);
        Assert.False(result.Normalized);
    }

    [Fact]
    public void Normalize_ThreePieces_IsCountableAndIsNotNormalized()
    {
        var result = UnitNormalization.Normalize(3m, ProductUnit.Piece);

        Assert.Equal(NormalizationBasis.Countable, result.Basis);
        Assert.False(result.Normalized);
    }

    /// <summary>
    /// Meter/SquareMeter/CubicMeter and the cooking measures are the two "judgement call" families
    /// called out in the brief: they exist in the enum, are not Mass or Volume, and must land on
    /// Countable/Normalized=false exactly like Pack or Piece do - not throw, and not get some other
    /// basis invented for them.
    /// </summary>
    [Theory]
    [InlineData(ProductUnit.Meter)]
    [InlineData(ProductUnit.SquareMeter)]
    [InlineData(ProductUnit.CubicMeter)]
    [InlineData(ProductUnit.Teaspoon)]
    [InlineData(ProductUnit.Tablespoon)]
    [InlineData(ProductUnit.Cup)]
    public void Normalize_LengthAreaVolumeOfSpaceAndCookingUnits_AreCountableAndNotNormalized(ProductUnit unit)
    {
        var result = UnitNormalization.Normalize(4m, unit);

        Assert.Equal(unit, result.CanonicalUnit);
        Assert.Equal(NormalizationBasis.Countable, result.Basis);
        Assert.False(result.Normalized);
    }

    #endregion

    #region Zero and negative quantities

    /// <summary>
    /// Deliberately run against Kilogram, a Mass unit, rather than a Countable one such as Pack:
    /// Normalize(0, Pack) would report Normalized = false regardless of whether the zero/negative
    /// guard exists at all, since every Countable unit is unconditionally unnormalized - that
    /// assertion would pass even against a buggy implementation that forgot the guard entirely.
    /// Kilogram is the case that actually distinguishes "guarded" from "not guarded": without the
    /// guard, UnitFunctions.Convert(0, Kilogram, Kilogram) converts perfectly cleanly (it only
    /// multiplies/divides by fixed constants, never by the value itself) and would otherwise come
    /// back Normalized = true.
    /// </summary>
    [Fact]
    public void Normalize_ZeroMassQuantity_IsNotNormalized()
    {
        var result = UnitNormalization.Normalize(0m, ProductUnit.Kilogram);

        Assert.False(result.Normalized);
    }

    [Fact]
    public void Normalize_NegativeMassQuantity_IsNotNormalized()
    {
        var result = UnitNormalization.Normalize(-5m, ProductUnit.Kilogram);

        Assert.False(result.Normalized);
    }

    [Fact]
    public void Normalize_ZeroVolumeQuantity_IsNotNormalized()
    {
        var result = UnitNormalization.Normalize(0m, ProductUnit.Liter);

        Assert.False(result.Normalized);
    }

    [Fact]
    public void Normalize_NegativeVolumeQuantity_IsNotNormalized()
    {
        var result = UnitNormalization.Normalize(-1.5m, ProductUnit.Liter);

        Assert.False(result.Normalized);
    }

    #endregion

    #region Enum coverage - the regression guard for units added later

    /// <summary>
    /// The guard the brief asks for by name: a hand-listed set of units would stay green forever
    /// even after a new <see cref="ProductUnit"/> member is added and silently mis-normalized,
    /// because nothing would ever call <c>Normalize</c> with it. Looping
    /// <c>Enum.GetValues&lt;ProductUnit&gt;()</c> instead means a new member is exercised the
    /// moment it exists in the enum - it only has to not throw (from either <c>Normalize</c> or
    /// <c>SeriesKeyFor</c>), whatever basis it eventually gets classified under.
    /// </summary>
    [Fact]
    public void Normalize_EveryUnitEnumMember_NormalizesAndKeysWithoutThrowing()
    {
        foreach (var unit in Enum.GetValues<ProductUnit>())
        {
            var exception = Record.Exception(() =>
            {
                var normalized = UnitNormalization.Normalize(5m, unit);
                var key = UnitNormalization.SeriesKeyFor(normalized);
                Assert.False(string.IsNullOrWhiteSpace(key));
            });

            Assert.True(exception is null, $"Unit.{unit} threw: {exception}");
        }
    }

    #endregion

    #region SeriesKeyFor

    [Fact]
    public void SeriesKeyFor_GramAndKilogram_ShareTheSameKey()
    {
        var gram = UnitNormalization.Normalize(500m, ProductUnit.Gram);
        var kilogram = UnitNormalization.Normalize(2m, ProductUnit.Kilogram);

        Assert.Equal(UnitNormalization.SeriesKeyFor(gram), UnitNormalization.SeriesKeyFor(kilogram));
    }

    [Fact]
    public void SeriesKeyFor_PackAndBox_HaveDifferentKeys()
    {
        var pack = UnitNormalization.Normalize(3m, ProductUnit.Pack);
        var box = UnitNormalization.Normalize(3m, ProductUnit.Box);

        Assert.NotEqual(UnitNormalization.SeriesKeyFor(pack), UnitNormalization.SeriesKeyFor(box));
    }

    #endregion
}
