using Myriad.Types;

namespace Myriad.Extensions
{
    public static class ChannelExtensions
    {
        public static string Mention(this Channel channel) => $"<#{channel.Id}>";
    }
}