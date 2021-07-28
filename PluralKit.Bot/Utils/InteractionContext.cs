using System;
using System.Threading.Tasks;

using Autofac;

using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Types;

namespace PluralKit.Bot
{
    public class InteractionContext
    {
        private readonly InteractionCreateEvent _evt;
        private readonly ILifetimeScope _services;
        
        public InteractionContext(InteractionCreateEvent evt, ILifetimeScope services)
        {
            _evt = evt;
            _services = services;
        }

        public ulong ChannelId => _evt.ChannelId;
        public ulong? MessageId => _evt.Message?.Id;
        public GuildMember? Member => _evt.Member;
        public User User => _evt.Member?.User ?? _evt.User;
        public string Token => _evt.Token;
        public string? CustomId => _evt.Data?.CustomId;
        public InteractionCreateEvent Event => _evt;

        public async Task Reply(string content)
        {
            await Respond(InteractionResponse.ResponseType.ChannelMessageWithSource,
                new InteractionApplicationCommandCallbackData
                {
                    Content = content,
                    Flags = Message.MessageFlags.Ephemeral
                });
        }

        public async Task Ignore()
        {
            await Respond(InteractionResponse.ResponseType.DeferredUpdateMessage, new InteractionApplicationCommandCallbackData
            {
                // Components = _evt.Message.Components
            });
        }

        public async Task Acknowledge()
        {
            await Respond(InteractionResponse.ResponseType.UpdateMessage, new InteractionApplicationCommandCallbackData
            {
                Components = Array.Empty<MessageComponent>()
            });
        }

        public async Task Respond(InteractionResponse.ResponseType type, InteractionApplicationCommandCallbackData? data)
        { 
            var rest = _services.Resolve<DiscordApiClient>();
            await rest.CreateInteractionResponse(_evt.Id, _evt.Token, new InteractionResponse {Type = type, Data = data});
        }
    }
}