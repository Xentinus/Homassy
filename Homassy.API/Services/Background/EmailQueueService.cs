using System.Threading.Channels;
using Homassy.API.Models.Background;
using Serilog;

namespace Homassy.API.Services.Background;

public sealed class EmailQueueService : IEmailQueueService
{
    private readonly Channel<EmailTask> _queue;
    private readonly TimeSpan _queueTimeout = TimeSpan.FromSeconds(5);

    public EmailQueueService(int capacity = 200)
    {
        _queue = Channel.CreateBounded<EmailTask>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async ValueTask<bool> TryQueueEmailAsync(EmailTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        using var timeoutCts = new CancellationTokenSource(_queueTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await _queue.Writer.WriteAsync(task, linkedCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            Log.Warning($"Email queue full, timeout after {_queueTimeout.TotalSeconds}s for {task.Email}");
            return false;
        }
    }

    internal async ValueTask<EmailTask> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }

    internal int Count => _queue.Reader.Count;
}
