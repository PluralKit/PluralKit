using Myriad.Types;

namespace Myriad.Extensions
{
    public static class UserExtensions
    {
        public static string AvatarUrl(this User user) => 
            $"https://cdn.discordapp.com/avatars/{user.Id}/{user.Avatar}.png";
    }
}