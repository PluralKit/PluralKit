using Myriad.Types;

namespace Myriad.Extensions;

public static class GuildExtensions
{
    public static int FileSizeLimit(this Guild guild)
    {
        switch (guild.PremiumTier)
        {
            default:
            case PremiumTier.NONE:
            case PremiumTier.TIER_1:
                return 8;
            case PremiumTier.TIER_2:
                return 50;
            case PremiumTier.TIER_3:
                return 100;
        }
    }
}