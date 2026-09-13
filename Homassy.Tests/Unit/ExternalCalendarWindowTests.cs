using Homassy.API.Functions;

namespace Homassy.Tests.Unit;

/// <summary>
/// The recurrence-expansion window used to be built from <c>DateTime.Now</c>, which reads the
/// container's local clock while every value the boundaries are compared against — parsed
/// DTSTART/DTEND, the rest of the calendar pipeline — is UTC. With no TZ set the two are the same
/// value and the bug is invisible, so these tests pin the boundaries to UTC by construction rather
/// than relying on the ambient zone of whatever machine runs them (#86).
/// </summary>
public class ExternalCalendarWindowTests
{
    [Fact]
    public void ExpansionWindow_ReturnsUtcBoundaries()
    {
        var (start, end) = ExternalCalendarFunctions.ExpansionWindow(DateTime.UtcNow);

        Assert.Equal(DateTimeKind.Utc, start.Kind);
        Assert.Equal(DateTimeKind.Utc, end.Kind);
    }

    [Fact]
    public void ExpansionWindow_DerivesBothBoundariesFromTheSameInstant()
    {
        var now = new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc);

        var (start, end) = ExternalCalendarFunctions.ExpansionWindow(now);

        Assert.Equal(now.AddMonths(-ExternalCalendarFunctions.ExpansionWindowMonthsBack), start);
        Assert.Equal(now.AddMonths(ExternalCalendarFunctions.ExpansionWindowMonthsForward), end);
    }

    /// <summary>
    /// The same absolute instant, handed over as a local or unspecified value, must not be accepted:
    /// that is exactly the shape <c>DateTime.Now</c> produces, and taking it would put the
    /// container's offset on one side of every window comparison.
    /// </summary>
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void ExpansionWindow_RejectsAnInstantThatIsNotUtc(DateTimeKind kind)
    {
        var notUtc = DateTime.SpecifyKind(new DateTime(2026, 3, 29, 1, 30, 0), kind);

        Assert.Throws<ArgumentException>(() => ExternalCalendarFunctions.ExpansionWindow(notUtc));
    }

    /// <summary>
    /// A daylight-saving switch moves a local clock but not a UTC one, so the boundaries around one
    /// must land on the wall-clock time the instant asks for, not an hour either side of it.
    /// 2026-03-29 01:30 UTC is inside the European switch; 2026-10-25 01:30 UTC is inside the
    /// autumn one.
    /// </summary>
    [Theory]
    [InlineData(2026, 3, 29)]
    [InlineData(2026, 10, 25)]
    public void ExpansionWindow_IsUnaffectedByADaylightSavingSwitch(int year, int month, int day)
    {
        var now = new DateTime(year, month, day, 1, 30, 0, DateTimeKind.Utc);

        var (start, end) = ExternalCalendarFunctions.ExpansionWindow(now);

        Assert.Equal(new TimeSpan(1, 30, 0), start.TimeOfDay);
        Assert.Equal(new TimeSpan(1, 30, 0), end.TimeOfDay);
    }
}
