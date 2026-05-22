using System.Threading.Channels;
using UTB.Minute.Contracts;

namespace UTB.Minute.WebApi;

public sealed class OrderEventStream
{
    private readonly List<Channel<OrderChangedEvent>> subscribers = [];
    private readonly object gate = new();

    public async Task PublishAsync(OrderChangedEvent orderEvent)
    {
        Channel<OrderChangedEvent>[] snapshot;
        lock (gate)
        {
            snapshot = subscribers.ToArray();
        }

        foreach (var subscriber in snapshot)
        {
            await subscriber.Writer.WriteAsync(orderEvent);
        }
    }

    public async IAsyncEnumerable<OrderChangedEvent> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<OrderChangedEvent>();
        lock (gate)
        {
            subscribers.Add(channel);
        }

        try
        {
            while (true)
            {
                bool hasItems;
                try
                {
                    hasItems = await channel.Reader.WaitToReadAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (!hasItems)
                {
                    yield break;
                }

                while (channel.Reader.TryRead(out var orderEvent))
                {
                    yield return orderEvent;
                }
            }
        }
        finally
        {
            lock (gate)
            {
                subscribers.Remove(channel);
            }
        }
    }
}
