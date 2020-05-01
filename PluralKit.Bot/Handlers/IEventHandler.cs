using System.Threading.Tasks;

using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace PluralKit.Bot
{
    public interface IEventHandler<in T> where T: DiscordEventArgs
    {
        Task Handle(T evt);

        DiscordChannel ErrorChannelFor(T evt) => null;
    }
}