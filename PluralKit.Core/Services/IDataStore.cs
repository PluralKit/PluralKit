using System.Collections.Generic;
using System.Threading.Tasks;

using NodaTime;

namespace PluralKit.Core {
    public enum AutoproxyMode
    {
        Off = 1,
        Front = 2,
        Latch = 3,
        Member = 4
    }
    
    public class FullMessage
    {
        public PKMessage Message;
        public PKMember Member;
        public PKSystem System;
    }
    
    public struct PKMessage
    {
        public ulong Mid;
        public ulong? Guild; // null value means "no data" (ie. from before this field being added)
        public ulong Channel;
        public ulong Sender;
        public ulong? OriginalMid;
    }

    public struct SwitchListEntry
    {
        public ICollection<PKMember> Members;
        public Instant TimespanStart;
        public Instant TimespanEnd;
    }

    public struct FrontBreakdown
    {
        public Dictionary<PKMember, Duration> MemberSwitchDurations;
        public Duration NoFronterDuration;
        public Instant RangeStart;
        public Instant RangeEnd;
    }
    
    public struct SwitchMembersListEntry
    {
        public MemberId Member;
        public Instant Timestamp;
    }

    public interface IDataStore
    {
        /// <summary>
        /// Gets a system by its user-facing human ID.
        /// </summary>
        /// <returns>The <see cref="PKSystem"/> with the given human ID, or null if no system was found.</returns>
        Task<PKSystem> GetSystemByHid(string systemHid);
        
        /// <summary>
        /// Gets a system by one of its linked Discord account IDs. Multiple IDs can return the same system.
        /// </summary>
        /// <returns>The <see cref="PKSystem"/> with the given linked account, or null if no system was found.</returns>
        Task<PKSystem> GetSystemByAccount(ulong linkedAccount);
        
        /// <summary>
        /// Gets a system by its API token. 
        /// </summary>
        /// <returns>The <see cref="PKSystem"/> with the given API token, or null if no corresponding system was found.</returns> 
        Task<PKSystem> GetSystemByToken(string apiToken);
        
        /// <summary>
        /// Gets the Discord account IDs linked to a system.
        /// </summary>
        /// <returns>An enumerable of Discord account IDs linked to this system.</returns>
        Task<IEnumerable<ulong>> GetSystemAccounts(PKSystem system);

        /// <summary>
        /// Gets the member count of a system.
        /// </summary>
        /// <param name="includePrivate">Whether the returned count should include private members.</param>
        Task<int> GetSystemMemberCount(SystemId system, bool includePrivate);

        /// <summary>
        /// Gets a list of members with proxy tags that conflict with the given tags.
        ///
        /// A set of proxy tags A conflict with proxy tags B if both A's prefix and suffix
        /// are a "subset" of B's. In other words, if A's prefix *starts* with B's prefix
        /// and A's suffix *ends* with B's suffix, the tag pairs are considered conflicting.
        /// </summary>
        /// <param name="system">The system to check in.</param>
        Task<IEnumerable<PKMember>> GetConflictingProxies(PKSystem system, ProxyTag tag);
        
        /// <summary>
        /// Creates a system, auto-generating its corresponding IDs.
        /// </summary>
        /// <param name="systemName">An optional system name to set. If `null`, will not set a system name.</param>
        /// <returns>The created system model.</returns>
        Task<PKSystem> CreateSystem(string systemName);
        // TODO: throw exception if account is present (when adding) or account isn't present (when removing)
        
        /// <summary>
        /// Links a Discord account to a system.
        /// </summary>
        /// <exception>Throws an exception (TODO: which?) if the given account is already linked to a system.</exception>
        Task AddAccount(PKSystem system, ulong accountToAdd);
        
        /// <summary>
        /// Unlinks a Discord account from a system.
        ///
        /// Will *not* throw if this results in an orphaned system - this is the caller's responsibility to ensure.
        /// </summary>
        /// <exception>Throws an exception (TODO: which?) if the given account is not linked to the given system.</exception>
        Task RemoveAccount(PKSystem system, ulong accountToRemove);
        
        /// <summary>
        /// Saves the information within the given <see cref="PKSystem"/> struct to the data store.
        /// </summary>
        Task SaveSystem(PKSystem system);
        
        /// <summary>
        /// Deletes the given system from the database.
        /// </summary>
        /// <para>
        /// This will also delete all the system's members, all system switches, and every message that has been proxied
        /// by members in the system.
        /// </para>
        Task DeleteSystem(PKSystem system);

        /// <summary>
        /// Gets a system by its internal member ID.
        /// </summary>
        /// <returns>The <see cref="PKMember"/> with the given internal ID, or null if no member was found.</returns>
        Task<PKMember> GetMemberById(MemberId memberId);
        
        /// <summary>
        /// Gets a member by its user-facing human ID.
        /// </summary>
        /// <returns>The <see cref="PKMember"/> with the given human ID, or null if no member was found.</returns>
        Task<PKMember> GetMemberByHid(string memberHid);
        
        /// <summary>
        /// Gets a member by its member name within one system.
        /// </summary>
        /// <para>
        /// Member names are *usually* unique within a system (but not always), whereas member names
        /// are almost certainly *not* unique globally - therefore only intra-system lookup is
        /// allowed.
        /// </para> 
        /// <returns>The <see cref="PKMember"/> with the given name, or null if no member was found.</returns>
        Task<PKMember> GetMemberByName(PKSystem system, string name);

        /// <summary>
        /// Gets a member by its display name within one system.
        /// </summary>
        /// <returns>The <see cref="PKMember"/> with the given name, or null if no member was found.</returns>
        Task<PKMember> GetMemberByDisplayName(PKSystem system, string name);
        
        /// <summary>
        /// Gets all members inside a given system.
        /// </summary>
        /// <returns>An enumerable of <see cref="PKMember"/> structs representing each member in the system, in no particular order.</returns>
        IAsyncEnumerable<PKMember> GetSystemMembers(PKSystem system, bool orderByName = false);

        /// <summary>
        /// Creates a member, auto-generating its corresponding IDs.
        /// </summary>
        /// <param name="system">The system in which to create the member.</param>
        /// <param name="name">The name of the member to create.</param>
        /// <returns>The created system model.</returns>
        Task<PKMember> CreateMember(SystemId system, string name);
        
        /// <summary>
        /// Saves the information within the given <see cref="PKMember"/> struct to the data store.
        /// </summary>
        Task SaveMember(PKMember member);
        
        /// <summary>
        /// Deletes the given member from the database.
        /// </summary>
        /// <para>
        /// This will remove this member from any switches it's involved in, as well as all the messages
        /// proxied by this member.
        /// </para>
        Task DeleteMember(PKMember member);
        
        /// <summary>
        /// Gets a message and its information by its ID.
        /// </summary>
        /// <param name="id">The message ID to look up. This can be either the ID of the trigger message containing the proxy tags or the resulting proxied webhook message.</param>
        /// <returns>An extended message object, containing not only the message data itself but the associated system and member structs.</returns>
        Task<FullMessage> GetMessage(ulong id); // id is both original and trigger, also add return type struct

        /// <summary>
        /// Saves a posted message to the database.
        /// </summary>
        /// <param name="senderAccount">The ID of the account that sent the original trigger message.</param>
        /// <param name="guildId">The ID of the guild the message was posted to.</param>
        /// <param name="channelId">The ID of the channel the message was posted to.</param>
        /// <param name="postedMessageId">The ID of the message posted by the webhook.</param>
        /// <param name="triggerMessageId">The ID of the original trigger message containing the proxy tags.</param>
        /// <param name="proxiedMemberId">The member (and by extension system) that was proxied.</param>
        /// <returns></returns>
        Task AddMessage(IPKConnection conn, ulong senderAccount, ulong guildId, ulong channelId, ulong postedMessageId, ulong triggerMessageId, MemberId proxiedMemberId);
        
        /// <summary>
        /// Deletes a message from the data store.
        /// </summary>
        /// <param name="postedMessageId">The ID of the webhook message to delete.</param>
        Task DeleteMessage(ulong postedMessageId);

        /// <summary>
        /// Deletes messages from the data store in bulk.
        /// </summary>
        /// <param name="postedMessageIds">The IDs of the webhook messages to delete.</param>
        Task DeleteMessagesBulk(IReadOnlyCollection<ulong> postedMessageIds);

        /// <summary>
        /// Gets switches from a system.
        /// </summary>
        /// <returns>An enumerable of the *count* latest switches in the system, in latest-first order. May contain fewer elements than requested.</returns>
        IAsyncEnumerable<PKSwitch> GetSwitches(SystemId system);

        /// <summary>
        /// Gets the total amount of switches in a given system.
        /// </summary>
        Task<int> GetSwitchCount(PKSystem system);

        /// <summary>
        /// Gets the latest (temporally; closest to now) switch of a given system.
        /// </summary>
        Task<PKSwitch> GetLatestSwitch(SystemId system);

        /// <summary>
        /// Gets the members a given switch consists of.
        /// </summary>
        IAsyncEnumerable<PKMember> GetSwitchMembers(PKSwitch sw);

        /// <summary>
        /// Gets a list of fronters over a given period of time.
        /// </summary>
        /// <para>
        /// This list is returned as an enumerable of "switch members", each containing a timestamp
        /// and a member ID. <seealso cref="GetMemberById"/>
        ///
        /// Switches containing multiple members will be returned as multiple switch members each with the same
        /// timestamp, and a change in timestamp should be interpreted as the start of a new switch.
        /// </para>
        /// <returns>An enumerable of the aforementioned "switch members".</returns>
        Task<IEnumerable<SwitchListEntry>> GetPeriodFronters(PKSystem system, Instant periodStart, Instant periodEnd);

        /// <summary>
        /// Calculates a breakdown of a system's fronters over a given period, including how long each member has
        /// been fronting, and how long *no* member has been fronting. 
        /// </summary>
        /// <para>
        /// Switches containing multiple members will count the full switch duration for all members, meaning
        /// the total duration may add up to longer than the breakdown period.
        /// </para>
        /// <param name="system"></param>
        /// <param name="periodStart"></param>
        /// <param name="periodEnd"></param>
        /// <returns></returns>
        Task<FrontBreakdown> GetFrontBreakdown(PKSystem system, Instant periodStart, Instant periodEnd);

        /// <summary>
        /// Registers a switch with the given members in the given system.
        /// </summary>
        /// <exception>Throws an exception (TODO: which?) if any of the members are not in the given system.</exception>
        Task AddSwitch(SystemId system, IEnumerable<PKMember> switchMembers);

        /// <summary>
        /// Updates the timestamp of a given switch. 
        /// </summary>
        Task MoveSwitch(PKSwitch sw, Instant time);
        
        /// <summary>
        /// Deletes a given switch from the data store.
        /// </summary>
        Task DeleteSwitch(PKSwitch sw);

        /// <summary>
        /// Deletes all switches in a given system from the data store.
        /// </summary>
        Task DeleteAllSwitches(PKSystem system);
    }
}