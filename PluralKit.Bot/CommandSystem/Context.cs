using System;
using System.Linq;
using System.Threading.Tasks;

using App.Metrics;

using Autofac;

using Discord;
using Discord.WebSocket;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Context
    {
        private ILifetimeScope _provider;

        private readonly DiscordShardedClient _client;
        private readonly SocketUserMessage _message;
        private readonly Parameters _parameters;

        private readonly IDataStore _data;
        private readonly PKSystem _senderSystem;
        private readonly IMetrics _metrics;

        private Command _currentCommand;

        public Context(ILifetimeScope provider, SocketUserMessage message, int commandParseOffset,
                       PKSystem senderSystem)
        {
            _client = provider.Resolve<DiscordShardedClient>();
            _message = message;
            _data = provider.Resolve<IDataStore>();
            _senderSystem = senderSystem;
            _metrics = provider.Resolve<IMetrics>();
            _provider = provider;
            _parameters = new Parameters(message.Content.Substring(commandParseOffset));
        }

        public IUser Author => _message.Author;
        public IMessageChannel Channel => _message.Channel;
        public IUserMessage Message => _message;
        public IGuild Guild => (_message.Channel as ITextChannel)?.Guild;
        public DiscordSocketClient Shard => _client.GetShardFor(Guild);
        public DiscordShardedClient Client => _client;
        public PKSystem System => _senderSystem;

        public string PopArgument() => _parameters.Pop();
        public string PeekArgument() => _parameters.Peek(); 
        public string RemainderOrNull(bool skipFlags = true) => _parameters.Remainder(skipFlags).Length == 0 ? null : _parameters.Remainder(skipFlags);
        public bool HasNext(bool skipFlags = true) => RemainderOrNull(skipFlags) != null;
        public string FullCommand => _parameters.FullCommand;

        public Task<IUserMessage> Reply(string text = null, Embed embed = null)
        {
            if (!this.BotHasPermission(ChannelPermission.SendMessages))
                // Will be "swallowed" during the error handler anyway, this message is never shown.
                throw new PKError("PluralKit does not have permission to send messages in this channel.");

            if (embed != null && !this.BotHasPermission(ChannelPermission.EmbedLinks))
                throw new PKError("PluralKit does not have permission to send embeds in this channel. Please ensure I have the **Embed Links** permission enabled.");
            
            return Channel.SendMessageAsync(text, embed: embed);
        }

        /// <summary>
        /// Checks if the next parameter is equal to one of the given keywords. Case-insensitive.
        /// </summary>
        public bool Match(ref string used, params string[] potentialMatches)
        {
            var arg = PeekArgument();
            foreach (var match in potentialMatches)
            {
                if (arg.Equals(match, StringComparison.InvariantCultureIgnoreCase))
                {
                    used = PopArgument();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the next parameter is equal to one of the given keywords. Case-insensitive.
        /// </summary>
        public bool Match(params string[] potentialMatches)
        {
            string used = null; // Unused and unreturned, we just yeet it
            return Match(ref used, potentialMatches);
        }

        public bool MatchFlag(params string[] potentialMatches)
        {
            // Flags are *ALWAYS PARSED LOWERCASE*. This means we skip out on a "ToLower" call here.
            // Can assume the caller array only contains lowercase *and* the set below only contains lowercase
            
            var flags = _parameters.Flags();
            return potentialMatches.Any(potentialMatch => flags.Contains(potentialMatch));
        }
        
        public async Task Execute<T>(Command commandDef, Func<T, Task> handler)
        {
            _currentCommand = commandDef;

            try
            {
                await handler(_provider.Resolve<T>());
                _metrics.Measure.Meter.Mark(BotMetrics.CommandsRun);
            }
            catch (PKSyntaxError e)
            {
                await Reply($"{Emojis.Error} {e.Message}\n**Command usage:**\n> pk;{commandDef.Usage}");
            }
            catch (PKError e)
            {
                await Reply($"{Emojis.Error} {e.Message}");
            }
            catch (TimeoutException)
            {
                // Got a complaint the old error was a bit too patronizing. Hopefully this is better?
                await Reply($"{Emojis.Error} Operation timed out, sorry. Try again, perhaps?");
            }
        }

        public async Task<IUser> MatchUser()
        {
            var text = PeekArgument();
            if (MentionUtils.TryParseUser(text, out var id))
                return await Shard.Rest.GetUserAsync(id); // TODO: this should properly fetch
            return null;
        }

        public bool MatchUserRaw(out ulong id)
        {
            id = 0;
            
            var text = PeekArgument();
            if (MentionUtils.TryParseUser(text, out var mentionId))
                id = mentionId;
            else if (ulong.TryParse(text, out var rawId))
                id = rawId;
            
            return id != 0;
        }

        public Task<PKSystem> PeekSystem() => MatchSystemInner();

        public async Task<PKSystem> MatchSystem()
        {
            var system = await MatchSystemInner();
            if (system != null) PopArgument();
            return system;
        }

        private async Task<PKSystem> MatchSystemInner()
        {
            var input = PeekArgument();

            // System references can take three forms:
            // - The direct user ID of an account connected to the system
            // - A @mention of an account connected to the system (<@uid>)
            // - A system hid

            // Direct IDs and mentions are both handled by the below method:
            if (input.TryParseMention(out var id))
                return await _data.GetSystemByAccount(id);

            // Finally, try HID parsing
            var system = await _data.GetSystemByHid(input);
            return system;
        }

        public async Task<PKMember> PeekMember()
        {
            var input = PeekArgument();

            // Member references can have one or two forms, depending on
            // whether you're in a system or not:
            // - A member hid
            // - A textual name of a member *in your own system*

            // First, if we have a system, try finding by member name in system
            if (_senderSystem != null && await _data.GetMemberByName(_senderSystem, input) is PKMember memberByName)
                return memberByName;

            // Then, try member HID parsing:
            if (await _data.GetMemberByHid(input) is PKMember memberByHid)
                return memberByHid;
            
            // We didn't find anything, so we return null.
            return null;
        }

        /// <summary>
        /// Attempts to pop a member descriptor from the stack, returning it if present. If a member could not be
        /// resolved by the next word in the argument stack, does *not* touch the stack, and returns null.
        /// </summary>
        public async Task<PKMember> MatchMember()
        {
            // First, peek a member
            var member = await PeekMember();

            // If the peek was successful, we've used up the next argument, so we pop that just to get rid of it.
            if (member != null) PopArgument();

            // Finally, we return the member value.
            return member;
        }

        public string CreateMemberNotFoundError(string input)
        {
            // TODO: does this belong here?
            if (input.Length == 5)
            {
                if (_senderSystem != null)
                    return $"Member with ID or name \"{input.SanitizeMentions()}\" not found.";
                return $"Member with ID \"{input.SanitizeMentions()}\" not found."; // Accounts without systems can't query by name
            }

            if (_senderSystem != null)
                return $"Member with name \"{input.SanitizeMentions()}\" not found. Note that a member ID is 5 characters long.";
            return $"Member not found. Note that a member ID is 5 characters long.";
        }

        public Context CheckSystem()
        {
            if (_senderSystem == null)
                throw Errors.NoSystemError;
            return this;
        }

        public Context CheckNoSystem()
        {
            if (_senderSystem != null)
                throw Errors.ExistingSystemError;
            return this;
        }

        public Context CheckOwnMember(PKMember member)
        {
            if (member.System != _senderSystem.Id)
                throw Errors.NotOwnMemberError;
            return this;
        }

        public GuildPermissions GetGuildPermissions(IUser user)
        {
            if (user is IGuildUser gu)
                return gu.GuildPermissions;
            if (Channel is SocketGuildChannel gc)
                return gc.GetUser(user.Id).GuildPermissions;
            return GuildPermissions.None;
        }

        public ChannelPermissions GetChannelPermissions(IUser user)
        {
            if (user is IGuildUser gu && Channel is IGuildChannel igc)
                return gu.GetPermissions(igc);
            if (Channel is SocketGuildChannel gc)
                return gc.GetUser(user.Id).GetPermissions(gc);
            return ChannelPermissions.DM;
        }
        
        public Context CheckAuthorPermission(GuildPermission permission, string permissionName)
        {
            if (!GetGuildPermissions(Author).Has(permission))
                throw new PKError($"You must have the \"{permissionName}\" permission in this server to use this command.");
            return this;
        }
        
        public Context CheckAuthorPermission(ChannelPermission permission, string permissionName)
        {
            if (!GetChannelPermissions(Author).Has(permission))
                throw new PKError($"You must have the \"{permissionName}\" permission in this server to use this command.");
            return this;
        }

        public Context CheckGuildContext()
        {
            if (Channel is IGuildChannel) return this;
            throw new PKError("This command can not be run in a DM.");
        }

        public LookupContext LookupContextFor(PKSystem target) => 
            System?.Id == target.Id ? LookupContext.ByOwner : LookupContext.ByNonOwner;

        public Context CheckSystemPrivacy(PKSystem target, PrivacyLevel level)
        {
            if (level.CanAccess(LookupContextFor(target))) return this;
            throw new PKError("You do not have permission to access this information.");
        }

        public ITextChannel MatchChannel()
        {
            if (!MentionUtils.TryParseChannel(PeekArgument(), out var channel)) return null;
            if (!(_client.GetChannel(channel) is ITextChannel textChannel)) return null;
            
            PopArgument();
            return textChannel;
        }
    }
}