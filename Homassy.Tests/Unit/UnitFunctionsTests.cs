using Homassy.API.Functions;
using ProductUnit = Homassy.API.Enums.Unit;

namespace Homassy.Tests.Unit;

public class UnitFunctionsTests
{
    #region Same Unit Conversion
    [Fact]
    public void Convert_SameUnit_ReturnsSameValue()
    {
        // Arrange & Act
        var result = UnitFunctions.Convert(100m, ProductUnit.Gram, ProductUnit.Gram);

        // Assert
        Assert.Equal(100m, result);
    }

    [Theory]
    [InlineData(ProductUnit.Milligram)]
    [InlineData(ProductUnit.Gram)]
    [InlineData(ProductUnit.Kilogram)]
    [InlineData(ProductUnit.Milliliter)]
    [InlineData(ProductUnit.Liter)]
    [InlineData(ProductUnit.Piece)]
    public void Convert_SameUnit_AllUnits_ReturnsSameValue(ProductUnit unit)
    {
        // Arrange
        var value = 42.5m;

        // Act
        var result = UnitFunctions.Convert(value, unit, unit);

        // Assert
        Assert.Equal(value, result);
    }
    #endregion

    #region Mass Conversions
    [Theory]
    [InlineData(1000, ProductUnit.Gram, ProductUnit.Kilogram, 1)]
    [InlineData(1, ProductUnit.Kilogram, ProductUnit.Gram, 1000)]
    [InlineData(500, ProductUnit.Milligram, ProductUnit.Gram, 0.5)]
    [InlineData(1, ProductUnit.Gram, ProductUnit.Milligram, 1000)]
    [InlineData(2.5, ProductUnit.Kilogram, ProductUnit.Milligram, 2500000)]
    [InlineData(5000000, ProductUnit.Milligram, ProductUnit.Kilogram, 5)]
    public void Convert_MassUnits_ReturnsCorrectValue(
        decimal value, ProductUnit fromUnit, ProductUnit toUnit, decimal expected)
    {
        // Act
        var result = UnitFunctions.Convert(value, fromUnit, toUnit);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_GramToGram_ReturnsExactValue()
    {
        // Arrange
        var value = 123.456m;

        // Act
        var result = UnitFunctions.Convert(value, ProductUnit.Gram, ProductUnit.Gram);

        // Assert
        Assert.Equal(value, result);
    }
    #endregion

    #region Volume Conversions
    [Theory]
    [InlineData(1000, ProductUnit.Milliliter, ProductUnit.Liter, 1)]
    [InlineData(1, ProductUnit.Liter, ProductUnit.Milliliter, 1000)]
    [InlineData(10, ProductUnit.Centiliter, ProductUnit.Deciliter, 1)]
    [InlineData(1, ProductUnit.Deciliter, ProductUnit.Centiliter, 10)]
    [InlineData(100, ProductUnit.Centiliter, ProductUnit.Liter, 1)]
    [InlineData(2, ProductUnit.Liter, ProductUnit.Deciliter, 20)]
    [InlineData(500, ProductUnit.Milliliter, ProductUnit.Centiliter, 50)]
    [InlineData(5, ProductUnit.Deciliter, ProductUnit.Milliliter, 500)]
    public void Convert_VolumeUnits_ReturnsCorrectValue(
        decimal value, ProductUnit fromUnit, ProductUnit toUnit, decimal expected)
    {
        // Act
        var result = UnitFunctions.Convert(value, fromUnit, toUnit);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_LiterToLiter_ReturnsExactValue()
    {
        // Arrange
        var value = 2.5m;

        // Act
        var result = UnitFunctions.Convert(value, ProductUnit.Liter, ProductUnit.Liter);

        // Assert
        Assert.Equal(value, result);
    }
    #endregion

    #region Piece Conversions
    [Fact]
    public void Convert_PieceToPiece_ReturnsSameValue()
    {
        // Arrange
        var value = 10m;

        // Act
        var result = UnitFunctions.Convert(value, ProductUnit.Piece, ProductUnit.Piece);

        // Assert
        Assert.Equal(value, result);
    }
    #endregion

    #region Invalid Conversions
    [Theory]
    [InlineData(ProductUnit.Gram, ProductUnit.Liter)]
    [InlineData(ProductUnit.Liter, ProductUnit.Kilogram)]
    [InlineData(ProductUnit.Piece, ProductUnit.Gram)]
    [InlineData(ProductUnit.Milliliter, ProductUnit.Milligram)]
    [InlineData(ProductUnit.Kilogram, ProductUnit.Deciliter)]
    public void Convert_IncompatibleUnits_ThrowsInvalidOperationException(ProductUnit fromUnit, ProductUnit toUnit)
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            UnitFunctions.Convert(100m, fromUnit, toUnit));
    }

    [Fact]
    public void Convert_MassToVolume_ThrowsWithMeaningfulMessage()
    {
        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            UnitFunctions.Convert(100m, ProductUnit.Gram, ProductUnit.Liter));

        // Assert
        Assert.Contains("not supported", exception.Message);
    }
    #endregion

    #region Edge Cases
    [Fact]
    public void Convert_ZeroValue_ReturnsZero()
    {
        // Act
        var result = UnitFunctions.Convert(0m, ProductUnit.Gram, ProductUnit.Kilogram);

        // Assert
        Assert.Equal(0m, result);
    }

    [Fact]
    public void Convert_VerySmallValue_ReturnsCorrectResult()
    {
        // Arrange
        var smallValue = 0.001m;

        // Act
        var result = UnitFunctions.Convert(smallValue, ProductUnit.Kilogram, ProductUnit.Gram);

        // Assert
        Assert.Equal(1m, result);
    }

    [Fact]
    public void Convert_VeryLargeValue_ReturnsCorrectResult()
    {
        // Arrange
        var largeValue = 1000000m;

        // Act
        var result = UnitFunctions.Convert(largeValue, ProductUnit.Gram, ProductUnit.Kilogram);

        // Assert
        Assert.Equal(1000m, result);
    }

    [Fact]
    public void Convert_DecimalPrecision_IsPreserved()
    {
        // Arrange
        var value = 1.23456789m;

        // Act
        var resultInKg = UnitFunctions.Convert(value, ProductUnit.Gram, ProductUnit.Kilogram);
        var resultBackInGram = UnitFunctions.Convert(resultInKg, ProductUnit.Kilogram, ProductUnit.Gram);

        // Assert
        Assert.Equal(value, resultBackInGram);
    }
    #endregion

    #region Round Trip Conversions
    [Theory]
    [InlineData(ProductUnit.Milligram, ProductUnit.Gram)]
    [InlineData(ProductUnit.Gram, ProductUnit.Kilogram)]
    [InlineData(ProductUnit.Milligram, ProductUnit.Kilogram)]
    public void Convert_MassRoundTrip_ReturnsOriginalValue(ProductUnit fromUnit, ProductUnit toUnit)
    {
        // Arrange
        var originalValue = 123.456m;

        // Act
        var intermediate = UnitFunctions.Convert(originalValue, fromUnit, toUnit);
        var result = UnitFunctions.Convert(intermediate, toUnit, fromUnit);

        // Assert
        Assert.Equal(originalValue, result);
    }

    [Theory]
    [InlineData(ProductUnit.Milliliter, ProductUnit.Liter)]
    [InlineData(ProductUnit.Centiliter, ProductUnit.Deciliter)]
    [InlineData(ProductUnit.Milliliter, ProductUnit.Deciliter)]
    public void Convert_VolumeRoundTrip_ReturnsOriginalValue(ProductUnit fromUnit, ProductUnit toUnit)
    {
        // Arrange
        var originalValue = 789.012m;

        // Act
        var intermediate = UnitFunctions.Convert(originalValue, fromUnit, toUnit);
        var result = UnitFunctions.Convert(intermediate, toUnit, fromUnit);

        // Assert
        Assert.Equal(originalValue, result);
    }
    #endregion
}
