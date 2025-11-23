using Homassy.API.Enums;

namespace Homassy.API.Functions
{
    public class UnitFunctions
    {
        public static decimal Convert(decimal value, Unit fromUnit, Unit toUnit)
        {
            if (fromUnit == toUnit)
                return value;

            if (IsMassUnit(fromUnit) && IsMassUnit(toUnit))
                return ConvertMass(value, fromUnit, toUnit);

            if (IsVolumeUnit(fromUnit) && IsVolumeUnit(toUnit))
                return ConvertVolume(value, fromUnit, toUnit);

            if (IsPieceLike(fromUnit) && IsPieceLike(toUnit))
                return value;

            throw new InvalidOperationException($"Conversion from {fromUnit} to {toUnit} is not supported.");
        }

        static bool IsMassUnit(Unit unit)
        {
            switch (unit)
            {
                case Unit.Milligram:
                case Unit.Gram:
                case Unit.Kilogram:
                    return true;
                default:
                    return false;
            }
        }

        static bool IsVolumeUnit(Unit unit)
        {
            switch (unit)
            {
                case Unit.Milliliter:
                case Unit.Centiliter:
                case Unit.Deciliter:
                case Unit.Liter:
                    return true;
                default:
                    return false;
            }
        }

        static bool IsPieceLike(Unit unit)
        {
            switch (unit)
            {
                case Unit.Piece:
                    return true;
                default:
                    return false;
            }
        }

        static decimal ConvertMass(decimal value, Unit fromUnit, Unit toUnit)
        {
            var valueInGram = fromUnit switch
            {
                Unit.Milligram => value / 1000m,
                Unit.Gram => value,
                Unit.Kilogram => value * 1000m,
                _ => throw new InvalidOperationException($"Mass conversion from {fromUnit} is not supported.")
            };

            return toUnit switch
            {
                Unit.Milligram => valueInGram * 1000m,
                Unit.Gram => valueInGram,
                Unit.Kilogram => valueInGram / 1000m,
                _ => throw new InvalidOperationException($"Mass conversion to {toUnit} is not supported.")
            };
        }

        static decimal ConvertVolume(decimal value, Unit fromUnit, Unit toUnit)
        {
            var valueInMilliliter = fromUnit switch
            {
                Unit.Milliliter => value,
                Unit.Centiliter => value * 10m,
                Unit.Deciliter => value * 100m,
                Unit.Liter => value * 1000m,
                _ => throw new InvalidOperationException($"Volume conversion from {fromUnit} is not supported.")
            };

            return toUnit switch
            {
                Unit.Milliliter => valueInMilliliter,
                Unit.Centiliter => valueInMilliliter / 10m,
                Unit.Deciliter => valueInMilliliter / 100m,
                Unit.Liter => valueInMilliliter / 1000m,
                _ => throw new InvalidOperationException($"Volume conversion to {toUnit} is not supported.")
            };
        }
    }
}
