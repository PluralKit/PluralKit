using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace PluralKit.Bot
{
    public interface IEventHandler<in T> where T: DiscordEventArgs
    {
        Task Handle(DiscordClient shard, T evt);

        DiscordChannel ErrorChannelFor(T evt) => null;
    }
}