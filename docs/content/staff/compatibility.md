# Compatibility with other bots
Many servers use *logger bots* for keeping track of edited and deleted messages, nickname changes, and other server events.
Because PluralKit deletes messages as part of proxying, this can often clutter up these logs. 

## Bots with PluralKit support
Some moderation bots have official PluralKit support, and properly handle excluding proxy deletes, as well as add PK-specific information to relevant log messages:

- [**Catalogger**](https://catalogger.starshines.xyz/docs)
- [**Aero**](https://aero.bot/) 
- [**CoreBot**](https://discord.gg/GAAj6DDrCJ)
- [**Quark**](https://quark.bot)

If your server uses an in-house bot for logging, you can use [the API](/api) to implement support yourself.

## Log cleanup
Another solution is for PluralKit to automatically delete log messages from other bots when they get posted.
PluralKit supports this through the **log cleanup** feature. To enable it, use the following command:

    pk;serverconfig logclean on
    
This requires you to have the *Manage Server* permission on the server. 

### Supported bots
At the moment, log cleanup works with the following bots:
- Annabelle (precise in embed format, fuzzy in inline format)
- [Auttaja](https://auttaja.io/) (precise)
- [blargbot](https://blargbot.xyz/) (precise)
- [Carl-bot](https://carl.gg/) (precise)
- [Circle](https://circlebot.xyz/) (fuzzy)
- [Dozer](https://github.com/frcdiscord/dozer) (precise)
- [Dyno](https://dyno.gg/) (precise)
- [GearBot](https://gearbot.rocks/) (fuzzy)
- [GenericBot](https://github.com/galenguyer/GenericBot) (precise)
- [Koira](https://koira.bot/) (precise)
- [Logger#6088](https://logger.bot/) (precise)
- [Logger#6278](https://loggerbot.chat/) (precise)
- [Mantaro](https://mantaro.site/) (precise)
- [Pancake](https://pancake.gg/) (fuzzy)
- [SafetyAtLast](https://www.safetyatlast.net/) (fuzzy)
- [Sapphire](https://sapph.xyz/) (precise, only in default format)
- [Skyra](https://www.skyra.pw/) (precise)
- [UnbelievaBoat](https://unbelievaboat.com/) (precise)
- Vanessa (fuzzy)
- [Vortex](https://github.com/jagrosh/Vortex/wiki) (fuzzy)

::: warning
In most cases, PluralKit will match log messages by the ID of the deleted message itself. However, some bots (marked with *(fuzzy)* above) don't include this in their logs. In this case, PluralKit will attempt to match based on other parameters, but there may be false positives. 

**For best results, use a bot marked *(precise)* in the above list.**
:::

If you want support for another logging bot, [let me know on the support server](https://discord.gg/PczBt78).

## Chat filter bots
If PluralKit detects that a proxy trigger message has already been deleted when it attempts to delete it itself, it'll also delete the trigger message.  This ensures compatibility in *most* cases with moderation bots that filter messages (eg. swear words, links, invites, etc). The bot will delete the original trigger message, and then PluralKit will clean up the proxied message as well.

Due to the timing aspect, this may not work 100% of the time, especially if there's server lag involved, but it's hopefully good enough.
