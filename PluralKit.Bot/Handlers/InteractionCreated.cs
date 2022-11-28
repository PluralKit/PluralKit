using Autofac;

using Serilog;

using Myriad.Gateway;
using Myriad.Types;

namespace PluralKit.Bot;

public class InteractionCreated: IEventHandler<InteractionCreateEvent>
{
    private readonly InteractionDispatchService _interactionDispatch;
    private readonly ILifetimeScope _services;
    private readonly ILogger _logger;

    public InteractionCreated(InteractionDispatchService interactionDispatch, ILifetimeScope services, ILogger logger)
    {
        _interactionDispatch = interactionDispatch;
        _services = services;
        _logger = logger;
    }

    public async Task Handle(int shardId, InteractionCreateEvent evt)
    {
        if (evt.Type == Interaction.InteractionType.MessageComponent)
        {
            _logger.Information($"Discord debug: got interaction with ID {evt.Id} from custom ID {evt.Data?.CustomId}");
            var customId = evt.Data?.CustomId;
            if (customId == null) return;

            var ctx = new InteractionContext(evt, _services);

            if (customId.Contains("help-menu"))
                await Help.ButtonClick(ctx);
            else
                await _interactionDispatch.Dispatch(customId, ctx);
        }
    }
}