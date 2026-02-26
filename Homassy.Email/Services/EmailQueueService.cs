using System.Threading.Channels;
using Homassy.Email.Models;

namespace Homassy.Email.Services;

public sealed class EmailQueueService : IEmailQueueService
{
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

    public async ValueTask<bool> TryEnqueueAsync(EmailMessage message, CancellationToken ct = default)
        => await _channel.Writer.WaitToWriteAsync(ct) && _channel.Writer.TryWrite(message);

    public ValueTask<EmailMessage> DequeueAsync(CancellationToken ct)
        => _channel.Reader.ReadAsync(ct);

    public int Count => _channel.Reader.Count;
}
