using Homassy.API.Enums;

namespace Homassy.API.Extensions
{
    public static class UnitExtensions
    {
        public static string ToUnitCode(this Unit unit)
        {
            return unit.ToString();
        }

        public static Unit FromUnitCode(string unitCode)
        {
            if (Enum.TryParse<Unit>(unitCode, ignoreCase: true, out var unit))
            {
                return unit;
            }

            // Default fallback
            return Unit.Piece;
        }
    }
}
