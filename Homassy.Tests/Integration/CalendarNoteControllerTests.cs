using System.Net;
using System.Net.Http.Json;
using Homassy.API.Models.Calendar;
using Homassy.API.Models.Family;
using Homassy.Data.Models.Common;
using Homassy.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

/// <summary>
/// Day notes on the family calendar (#60): CRUD, family scoping, and the fold into the aggregated
/// calendar response.
/// </summary>
public class CalendarNoteControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private const string NotesEndpoint = "/api/v1.0/calendar/notes";

    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly TestAuthHelper _authHelper;

    public CalendarNoteControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient();
        _output = output;
        _authHelper = new TestAuthHelper(factory, _client);
    }

    [Fact]
    public async Task CreateCalendarNote_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(NotesEndpoint, new CreateCalendarNoteRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Title = "School closed"
        });

        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCalendarNotes_WithoutToken_ReturnsUnauthorized()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await _client.PostAsJsonAsync($"{NotesEndpoint}/range", new GetCalendarNotesRequest
        {
            StartDate = today,
            EndDate = today
        });

        _output.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A note belongs to a family, so a user who is not in one has nowhere to put it. Refused at the
    /// edge rather than quietly stored as a personal note nobody else can see.
    /// </summary>
    [Fact]
    public async Task CreateCalendarNote_WithoutFamily_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("note-nofamily");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.PostAsJsonAsync(NotesEndpoint, new CreateCalendarNoteRequest
            {
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Title = "Gas meter reading"
            });

            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("CALNOTE-0003", body);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    [Fact]
    public async Task CalendarNote_FullLifecycle_CreateReadUpdateDelete()
    {
        string? testEmail = null;
        Guid? noteId = null;

        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("note-crud");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Note Family");

            var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));

            _output.WriteLine("=== Create ===");
            var createResponse = await _client.PostAsJsonAsync(NotesEndpoint, new CreateCalendarNoteRequest
            {
                Date = day,
                Title = "School closed",
                Content = "No classes, the whole week."
            });

            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<CalendarNoteInfo>>();
            Assert.NotNull(created?.Data);
            noteId = created.Data.PublicId;

            Assert.Equal(day, created.Data.Date);
            Assert.Equal("School closed", created.Data.Title);
            Assert.False(created.Data.ReminderSent);
            Assert.Null(created.Data.ReminderAt);
            Assert.NotEqual(Guid.Empty, created.Data.CreatedByPublicId);

            _output.WriteLine("=== Read back in range ===");
            var notes = await GetNotesAsync(day, day);
            Assert.Contains(notes, n => n.PublicId == noteId);

            _output.WriteLine("=== Update, arming a reminder ===");
            var reminderAt = DateTime.UtcNow.AddDays(3);
            var updateResponse = await _client.PutAsJsonAsync($"{NotesEndpoint}/{noteId}", new UpdateCalendarNoteRequest
            {
                Date = day,
                Title = "School closed all week",
                Content = "No classes.",
                ReminderAt = reminderAt
            });

            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<CalendarNoteInfo>>();
            Assert.NotNull(updated?.Data);
            Assert.Equal("School closed all week", updated.Data.Title);
            Assert.NotNull(updated.Data.ReminderAt);
            Assert.NotNull(updated.Data.LastEditedByPublicId);

            _output.WriteLine("=== Delete ===");
            var deleteResponse = await _client.DeleteAsync($"{NotesEndpoint}/{noteId}");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            Assert.DoesNotContain(await GetNotesAsync(day, day), n => n.PublicId == noteId);
            noteId = null;
        }
        finally
        {
            if (noteId.HasValue)
                await _client.DeleteAsync($"{NotesEndpoint}/{noteId}");

            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// A reminder whose moment is long past can never fire, so it is refused rather than stored as
    /// something the worker would silently drop.
    /// </summary>
    [Fact]
    public async Task CreateCalendarNote_WithAReminderInThePast_ReturnsBadRequest()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("note-stale");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Stale Reminder Family");

            var response = await _client.PostAsJsonAsync(NotesEndpoint, new CreateCalendarNoteRequest
            {
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Title = "Too late",
                ReminderAt = DateTime.UtcNow.AddDays(-10)
            });

            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("CALNOTE-0004", body);
        }
        finally
        {
            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    /// <summary>
    /// Notes arrive in the same date-range query as every other event type, so the calendar needs no
    /// second request to paint a day that has one.
    /// </summary>
    [Fact]
    public async Task GetCalendarEvents_IncludesDayNotes()
    {
        string? testEmail = null;
        Guid? noteId = null;

        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("note-aggregated");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);
            await CreateFamilyAsync("Aggregated Family");

            var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
            var createResponse = await _client.PostAsJsonAsync(NotesEndpoint, new CreateCalendarNoteRequest
            {
                Date = day,
                Title = "Bin day"
            });

            var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<CalendarNoteInfo>>();
            Assert.NotNull(created?.Data);
            noteId = created.Data.PublicId;

            var eventsResponse = await _client.PostAsJsonAsync("/api/v1.0/calendar", new GetCalendarEventsRequest
            {
                StartDate = day.AddDays(-1),
                EndDate = day.AddDays(1)
            });

            Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);
            var events = await eventsResponse.Content.ReadFromJsonAsync<ApiResponse<List<CalendarEventInfo>>>();
            Assert.NotNull(events?.Data);

            var noteEvent = events.Data.FirstOrDefault(e => e.PublicId == noteId);
            Assert.NotNull(noteEvent);
            Assert.Equal(CalendarEventType.DayNote, noteEvent.EventType);
            Assert.Equal("Bin day", noteEvent.Title);
            // A note is about a day, not a moment.
            Assert.True(noteEvent.IsAllDay);
        }
        finally
        {
            if (noteId.HasValue)
                await _client.DeleteAsync($"{NotesEndpoint}/{noteId}");

            _authHelper.ClearAuthToken();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }

    private async Task<List<CalendarNoteInfo>> GetNotesAsync(DateOnly start, DateOnly end)
    {
        var response = await _client.PostAsJsonAsync($"{NotesEndpoint}/range", new GetCalendarNotesRequest
        {
            StartDate = start,
            EndDate = end
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<List<CalendarNoteInfo>>>();
        return content?.Data ?? [];
    }

    private async Task CreateFamilyAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/family/create", new CreateFamilyRequest { Name = name });
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Family create status: {response.StatusCode} {body}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
