using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Autofac;

using Myriad.Rest.Types.Requests;
using Myriad.Types;

using NodaTime;

namespace PluralKit.Bot.Interactive
{
    public abstract class BaseInteractive
    {
        private readonly Context _ctx;
        private readonly List<Button> _buttons = new();
        private readonly TaskCompletionSource _tcs = new();
        private bool _running;
        
        protected BaseInteractive(Context ctx)
        {
            _ctx = ctx;
        }

        public Duration Timeout { get; set; } = Duration.FromMinutes(5);

        protected Button AddButton(Func<InteractionContext, Task> handler, string? label = null, ButtonStyle style = ButtonStyle.Secondary, bool disabled = false)
        {
            var dispatch = _ctx.Services.Resolve<InteractionDispatchService>();
            var customId = dispatch.Register(handler, Timeout);
            
            var button = new Button
            {
                Label = label,
                Style = style,
                Disabled = disabled,
                CustomId = customId
            };
            _buttons.Add(button);
            return button;
        }

        protected async Task Update(InteractionContext ctx, string? content = null, Embed? embed = null)
        {
            await ctx.Respond(InteractionResponse.ResponseType.UpdateMessage,
                new InteractionApplicationCommandCallbackData
                {
                    Content = content,
                    Embeds = embed != null ? new[] { embed } : null,
                    Components = GetComponents()
                });
        }

        protected async Task Finish(InteractionContext ctx)
        {
            foreach (var button in _buttons) 
                button.Disabled = true;
            await Update(ctx);
            
            _tcs.TrySetResult();
        }

        protected async Task<Message> Send(string? content = null, Embed? embed = null)
        {
            return await _ctx.Rest.CreateMessage(_ctx.Channel.Id, new MessageRequest
            {
                Content = content,
                Embed = embed,
                Components = GetComponents()
            });
        }

        public MessageComponent[] GetComponents()
        {
            return new MessageComponent[]
            {
                new()
                {
                    Type = ComponentType.ActionRow,
                    Components = _buttons.Select(b => b.ToMessageComponent()).ToArray()
                }
            };
        }

        public void Setup(Context ctx)
        {
            var dispatch = ctx.Services.Resolve<InteractionDispatchService>();
            foreach (var button in _buttons) 
                button.CustomId = dispatch.Register(button.Handler, Timeout);
        }

        public abstract Task Start();
        
        public async Task Run()
        {
            if (_running)
                throw new InvalidOperationException("Action is already running");
            _running = true;

            await Start();
            
            var cts = new CancellationTokenSource(Timeout.ToTimeSpan());
            cts.Token.Register(() => _tcs.TrySetException(new TimeoutException("Action timed out")));

            try
            {
                await _tcs.Task;
            }
            finally
            {
                Cleanup();
            }
        }

        private void Cleanup()
        {
            var dispatch = _ctx.Services.Resolve<InteractionDispatchService>();
            foreach (var button in _buttons) 
                dispatch.Unregister(button.CustomId!);
        }
    }
}