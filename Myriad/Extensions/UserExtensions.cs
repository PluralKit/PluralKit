using Myriad.Types;

namespace Myriad.Extensions
{
    public static class UserExtensions
    {
        public static string Mention(this User user) => $"<@{user.Id}>";

        public static string AvatarUrl(this User user, string? format = "png", int? size = 128) =>
            $"https://cdn.discordapp.com/avatars/{user.Id}/{user.Avatar}.{format}?size={size}";
    }
}