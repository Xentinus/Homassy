using Homassy.API.Context;
using Homassy.API.Entities.Family;
using Homassy.API.Functions;
using Homassy.API.Security;
using Microsoft.Extensions.Configuration;

namespace Homassy.Tests.Unit;

/// <summary>
/// Validation at the model boundary only covers new and edited rows. A row written before the
/// check existed — or one whose host has since been re-pointed at an internal address — must
/// stop being fetched on the timer rather than be trusted because it is already in the table.
/// </summary>
public class ExternalCalendarSyncGuardTests
{
    /// <summary>Fails the test if the sync path ever gets as far as issuing a request.</summary>
    private sealed class ForbiddenHandler : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException($"The sync path attempted to fetch {request.RequestUri}");
        }
    }

    private static HomassyDbContext CreateContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=homassy;Username=test;Password=test"
            })
            .Build();

        HomassyDbContext.SetConfiguration(configuration);

        // Never connects: the guard rejects the URL before anything reaches the database.
        return new HomassyDbContext();
    }

    private static FamilyExternalCalendar CalendarWith(string url) => new()
    {
        Name = "Stored before the guard existed",
        ICalUrl = url,
        IsEnabled = true
    };

    [Theory]
    [InlineData("http://homassy-kratos:4434/admin/identities")]
    [InlineData("http://homassy-api:8080/api/v1.0/internal/inventory/broadcast")]
    [InlineData("http://169.254.169.254/latest/meta-data/iam/security-credentials/")]
    [InlineData("http://10.0.0.5:5432/")]
    [InlineData("https://localhost/feed.ics")]
    [InlineData("file:///etc/passwd")]
    public async Task SyncCalendarAsync_WithAnInternalUrl_RefusesWithoutFetching(string url)
    {
        var handler = new ForbiddenHandler();
        using var httpClient = new HttpClient(handler);
        using var context = CreateContext();
        var calendar = CalendarWith(url);

        await ExternalCalendarFunctions.SyncCalendarAsync(calendar, context, httpClient, CancellationToken.None);

        Assert.False(handler.WasCalled);
        Assert.NotNull(calendar.LastSyncError);
        Assert.Null(calendar.LastSyncedAt);
        Assert.Null(calendar.CachedEventsJson);
    }

    [Fact]
    public async Task SyncCalendarAsync_WithAHostThatResolvesToLoopback_RefusesWithoutFetching()
    {
        var handler = new ForbiddenHandler();
        using var httpClient = new HttpClient(handler);
        using var context = CreateContext();

        // "localhost" is a single label, so screen it as an FQDN that still resolves to 127.0.0.1
        // to exercise the DNS re-check rather than the syntactic one.
        var calendar = CalendarWith("https://localhost.localdomain/feed.ics");

        await ExternalCalendarFunctions.SyncCalendarAsync(calendar, context, httpClient, CancellationToken.None);

        Assert.False(handler.WasCalled);
        Assert.NotNull(calendar.LastSyncError);
    }

    [Fact]
    public void AllowInsecureScheme_DefaultsToFalse()
    {
        // Program.cs sets this from IWebHostEnvironment; outside Development plain http feeds
        // must not be accepted, and the default has to be the safe one.
        Assert.False(ExternalUrlGuard.AllowInsecureScheme);
    }
}
