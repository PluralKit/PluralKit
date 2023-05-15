using Autofac;

using Serilog;

using Myriad.Gateway;
using Myriad.Types;
using System.Buffers;

using PluralKit.Core;

namespace PluralKit.Bot;

public class InteractionCreated: IEventHandler<InteractionCreateEvent>
{
    private readonly InteractionDispatchService _interactionDispatch;
    private readonly ApplicationCommandTree _commandTree;
    private readonly ILifetimeScope _services;
    private readonly ILogger _logger;

    public InteractionCreated(InteractionDispatchService interactionDispatch, ApplicationCommandTree commandTree,
                              ILifetimeScope services, ILogger logger)
    {
        _interactionDispatch = interactionDispatch;
        _commandTree = commandTree;
        _services = services;
        _logger = logger;
    }

    public async Task Handle(int shardId, InteractionCreateEvent evt)
    {
        var system = await _services.Resolve<ModelRepository>().GetSystemByAccount(evt.Member?.User.Id ?? evt.User!.Id);
        var ctx = new InteractionContext(_services, evt, system);

        switch (evt.Type)
        {
            case Interaction.InteractionType.MessageComponent:
                _logger.Information("Discord debug: got interaction with ID {id} from custom ID {custom_id}", evt.Id, evt.Data?.CustomId);
                var customId = evt.Data?.CustomId;
                if (customId == null) return;

                if (customId.Contains("help-menu"))
                    await Help.ButtonClick(ctx);
                else
                    await _interactionDispatch.Dispatch(customId, ctx);

                break;

            case Interaction.InteractionType.ApplicationCommand:
                var res = _commandTree.TryHandleCommand(ctx);
                if (res != null)
                {
                    await res;
                    return;
                }

                // got some unhandled command, log and ignore
                _logger.Warning(@"Unhandled ApplicationCommand interaction: {EventId} {CommandName}", evt.Id, evt.Data?.Name);
                break;
        };
    }
}