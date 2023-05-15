using ApplicationCommandType = Myriad.Types.ApplicationCommand.ApplicationCommandType;

namespace PluralKit.Bot;

public partial class ApplicationCommandTree
{
    public static ApplicationCommand ProxiedMessageQuery = new(ApplicationCommandType.Message, "\U00002753 Message info");
    public static ApplicationCommand ProxiedMessageDelete = new(ApplicationCommandType.Message, "\U0000274c Delete message");
    public static ApplicationCommand ProxiedMessagePing = new(ApplicationCommandType.Message, "\U0001f514 Ping author");
}