using Myriad.Gateway;

namespace PluralKit.Bot;

public interface IEventHandler<in T> where T : IGatewayEvent
{
    Task Handle(int shardId, T evt);

    ulong? ErrorChannelFor(T evt, ulong userId) => null;
}