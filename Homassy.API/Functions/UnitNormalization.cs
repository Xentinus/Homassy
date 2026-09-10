using Homassy.API.Enums;

namespace Homassy.API.Functions
{
    /// <summary>
    /// Which family a <see cref="Unit"/> belongs to for normalization purposes. <see cref="Mass"/>
    /// and <see cref="Volume"/> each have one canonical unit every member of the family converts to
    /// via <see cref="UnitFunctions.Convert"/> - Kilogram and Liter respectively - so any two values
    /// normalized under the same basis are directly comparable, whatever unit they were purchased
    /// in. <see cref="Countable"/> covers every unit with no such conversion: a value normalized
    /// under it is comparable only to another value with the exact same
    /// <see cref="NormalizedQuantity.CanonicalUnit"/>, never across units - see
    /// <see cref="UnitNormalization"/>'s remarks for why that restriction exists at all.
    /// </summary>
    public enum NormalizationBasis
    {
        Mass,
        Volume,
        Countable
    }

    /// <summary>
    /// A purchased quantity re-expressed in its family's canonical unit, so a 500 g jar and a 2 kg
    /// bag of the same product land on values that are directly comparable instead of needing a
    /// caller to know every unit pair's own conversion factor.
    /// </summary>
    /// <param name="Value">
    /// The source quantity re-expressed in <paramref name="CanonicalUnit"/> terms - the two are
    /// always paired consistently, whatever <paramref name="Normalized"/> ends up being. A caller
    /// must still check <see cref="Normalized"/> before using this for a price-per-unit comparison;
    /// this field alone does not promise the value is safe to compare against another product's.
    /// </param>
    /// <param name="CanonicalUnit">
    /// <see cref="Unit.Kilogram"/> for every <see cref="NormalizationBasis.Mass"/> quantity,
    /// <see cref="Unit.Liter"/> for every <see cref="NormalizationBasis.Volume"/> one, and the
    /// original purchased unit itself for <see cref="NormalizationBasis.Countable"/> - there is no
    /// other unit a Pack or a Piece could canonicalize to.
    /// </param>
    /// <param name="Basis">The family the original unit belongs to - see <see cref="NormalizationBasis"/>.</param>
    /// <param name="Normalized">
    /// <see langword="false"/> whenever comparing this <see cref="Value"/> against another
    /// product's would be meaningless or dangerous: for a <see cref="NormalizationBasis.Countable"/>
    /// unit (no conversion to mass or volume exists at all - comparing a Pack's price to a
    /// Kilogram's is the exact mistake this type exists to prevent), or for a source quantity that
    /// was zero or negative (a caller dividing a price by <see cref="Value"/> - Task 13 - would
    /// otherwise divide by zero, or produce a nonsensical negative per-unit price).
    /// <see langword="true"/> only for a strictly positive Mass or Volume quantity.
    /// </param>
    public readonly record struct NormalizedQuantity(
        decimal Value,
        Unit CanonicalUnit,
        NormalizationBasis Basis,
        bool Normalized);

    /// <summary>
    /// Turns a purchased quantity - "500", Gram - into a value comparable across pack sizes -
    /// "0.5", Kilogram - so two purchases of the same product, one bought by the gram and one by
    /// the kilogram, land in the same per-kilogram series instead of two unrelated ones. This one
    /// rule is what lets a later comparison (Task 13) answer "is the bigger pack actually cheaper?"
    /// - and, just as important, refuse to answer at all when the two purchases were never
    /// comparable in the first place, such as a Pack against a Kilogram.
    ///
    /// <para>
    /// <b>Pure and static on purpose</b>, the same way <see cref="SeriesZeroFill"/> is: no
    /// <c>DbContext</c>, no clock, nothing but its parameters - every case below is a plain
    /// input/output fact, provable without a database. That is what lets
    /// <c>UnitNormalizationTests</c> assert every one of them directly, in-process.
    /// </para>
    /// </summary>
    public static class UnitNormalization
    {
        /// <summary>
        /// Normalizes <paramref name="quantity"/> - the amount of <paramref name="unit"/> a single
        /// purchase line records - into its family's canonical unit. All the actual mass and volume
        /// arithmetic is delegated to <see cref="UnitFunctions.Convert"/>, which stays the sole
        /// authority on those conversion factors; this method's only job is deciding which family
        /// <paramref name="unit"/> belongs to (see <see cref="BasisFor"/>) and whether the result
        /// can be trusted for comparison at all.
        /// </summary>
        public static NormalizedQuantity Normalize(decimal quantity, Unit unit)
        {
            switch (BasisFor(unit))
            {
                case NormalizationBasis.Mass:
                    return NormalizeConvertible(quantity, unit, Unit.Kilogram, NormalizationBasis.Mass);

                case NormalizationBasis.Volume:
                    return NormalizeConvertible(quantity, unit, Unit.Liter, NormalizationBasis.Volume);

                default:
                    // Countable: there is nothing to convert to, so the "canonical" unit is the
                    // purchased unit itself, and Value passes through per 1 of it - 3 Pack stays 3,
                    // it is not divided down to "price of a single pack" here (that division needs a
                    // price, which this pure quantity-only function never sees; it is Task 13's job
                    // once it has one). Always Normalized = false: even an ordinary, strictly
                    // positive Countable quantity is still not comparable to a Mass or Volume one,
                    // which is the entire reason this basis exists.
                    return new NormalizedQuantity(quantity, unit, NormalizationBasis.Countable, false);
            }
        }

        /// <summary>
        /// The key a caller (Task 13) buckets comparable series under: the same key for every unit
        /// that shares a <see cref="NormalizedQuantity.CanonicalUnit"/> - Gram and Kilogram both
        /// resolve to <c>"per-kg"</c> - and a distinct key for every unit that does not, so a Pack
        /// and a Box (both <see cref="NormalizationBasis.Countable"/>, each canonical only to
        /// itself) never collide into one series. Deliberately keyed on
        /// <see cref="NormalizedQuantity.CanonicalUnit"/> rather than <see cref="NormalizedQuantity.Basis"/>
        /// alone - two different Countable units must still be told apart, which a basis-only key
        /// could never do.
        /// </summary>
        public static string SeriesKeyFor(NormalizedQuantity q)
        {
            return q.CanonicalUnit switch
            {
                Unit.Kilogram => "per-kg",
                Unit.Liter => "per-l",
                _ => $"per-{q.CanonicalUnit.ToString().ToLowerInvariant()}"
            };
        }

        /// <summary>
        /// Handles the two families <see cref="UnitFunctions.Convert"/> actually supports. The
        /// conversion itself never throws or misbehaves for a zero or negative
        /// <paramref name="quantity"/> - every mass/volume conversion factor in
        /// <see cref="UnitFunctions"/> is a fixed constant (1000, 100, 10, ...), so it only ever
        /// multiplies or divides by that constant, never by <paramref name="quantity"/> itself.
        /// <see cref="NormalizedQuantity.Normalized"/> is the only field that depends on the sign
        /// of <paramref name="quantity"/>; <see cref="NormalizedQuantity.Value"/> and
        /// <see cref="NormalizedQuantity.CanonicalUnit"/> are always the true converted amount, so
        /// the two never disagree with each other even when the result is flagged unusable.
        /// </summary>
        private static NormalizedQuantity NormalizeConvertible(decimal quantity, Unit unit, Unit canonicalUnit, NormalizationBasis basis)
        {
            var converted = UnitFunctions.Convert(quantity, unit, canonicalUnit);
            return new NormalizedQuantity(converted, canonicalUnit, basis, quantity > 0);
        }

        /// <summary>
        /// Classifies <paramref name="unit"/> into the family <see cref="Normalize"/> normalizes it
        /// under. Mass and Volume are hand-listed because each member must map to exactly one
        /// shared canonical unit; everything else falls to <see langword="default"/> rather than
        /// being hand-listed too, which is deliberate - see the remarks on that branch.
        /// </summary>
        private static NormalizationBasis BasisFor(Unit unit)
        {
            switch (unit)
            {
                case Unit.Milligram:
                case Unit.Gram:
                case Unit.Kilogram:
                    return NormalizationBasis.Mass;

                case Unit.Milliliter:
                case Unit.Centiliter:
                case Unit.Deciliter:
                case Unit.Liter:
                    return NormalizationBasis.Volume;

                // Decision, not an oversight: Meter/SquareMeter/CubicMeter and the cooking measures
                // Teaspoon/Tablespoon/Cup all exist in the Unit enum, but UnitFunctions.Convert has
                // no conversion factors for any of them at all - not even Meter to Centimeter, which
                // falls through IsMassUnit/IsVolumeUnit/IsPieceLike exactly as a cross-family pair
                // would and throws InvalidOperationException. Even if it did, "price per teaspoon"
                // or "price per square metre" is not a comparison this milestone was asked to
                // support. They are deliberately classified Countable (Normalized = false) here
                // rather than given a Length/Area/Volume-of-space basis of their own. If a future
                // task wants that - and adds the matching conversions to UnitFunctions first - this
                // is the one place to revisit, not something to change incidentally while touching
                // something else.
                case Unit.Meter:
                case Unit.SquareMeter:
                case Unit.CubicMeter:
                case Unit.Teaspoon:
                case Unit.Tablespoon:
                case Unit.Cup:
                    return NormalizationBasis.Countable;

                // Everything else - Piece, Pack, Box, Bottle, Can, Jar, Bag, Centimeter, Millimeter,
                // and any unit added to this enum after this file was written - has no conversion to
                // Kilogram or Liter (see UnitFunctions.Convert), so it falls back to Countable rather
                // than getting its own hand-listed case. That is deliberate, not laziness:
                // UnitNormalizationTests loops over Enum.GetValues<Unit>(), so a unit added later
                // without ever touching this switch still gets a safe, explicit "not normalized"
                // answer here instead of silently falling through to Mass or Volume by accident.
                default:
                    return NormalizationBasis.Countable;
            }
        }
    }
}
