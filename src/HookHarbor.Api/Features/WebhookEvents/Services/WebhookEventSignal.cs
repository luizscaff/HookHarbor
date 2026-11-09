using System.Threading.Channels;

namespace HookHarbor.Api.Features.WebhookEvents.Services;

public class WebhookEventSignal
{
    private readonly Channel<Guid> _channel = Channel
        .CreateBounded<Guid>(
            new BoundedChannelOptions(1_000)
            { 
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public bool TryPublish(Guid webhookEventId)
    {
        return _channel.Writer.TryWrite(webhookEventId);
    }
}
