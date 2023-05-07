using ApplicationCommandType = Myriad.Types.ApplicationCommand.ApplicationCommandType;

namespace PluralKit.Bot;

public partial class ApplicationCommandTree
{
    public static ApplicationCommand ProxiedMessageQuery = new(ApplicationCommandType.Message, "Message info");
    public static ApplicationCommand ProxiedMessageDelete = new(ApplicationCommandType.Message, "Delete message");
    public static ApplicationCommand ProxiedMessagePing = new(ApplicationCommandType.Message, "Ping message author");
}