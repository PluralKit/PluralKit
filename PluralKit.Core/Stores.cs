using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using NodaTime;

namespace PluralKit {
    public class SystemStore {
        private IDbConnection conn;

        public SystemStore(IDbConnection conn) {
            this.conn = conn;
        }
        
        public async Task<PKSystem> Create(string systemName = null) {
            // TODO: handle HID collision case
            var hid = Utils.GenerateHid();
            return await conn.QuerySingleAsync<PKSystem>("insert into systems (hid, name) values (@Hid, @Name) returning *", new { Hid = hid, Name = systemName });
        }

        public async Task Link(PKSystem system, ulong accountId) {
            await conn.ExecuteAsync("insert into accounts (uid, system) values (@Id, @SystemId)", new { Id = accountId, SystemId = system.Id });
        }
        
        public async Task Unlink(PKSystem system, ulong accountId) {
            await conn.ExecuteAsync("delete from accounts where uid = @Id and system = @SystemId", new { Id = accountId, SystemId = system.Id });
        }

        public async Task<PKSystem> GetByAccount(ulong accountId) {
            return await conn.QuerySingleOrDefaultAsync<PKSystem>("select systems.* from systems, accounts where accounts.system = systems.id and accounts.uid = @Id", new { Id = accountId });
        }

        public async Task<PKSystem> GetByHid(string hid) {
            return await conn.QuerySingleOrDefaultAsync<PKSystem>("select * from systems where systems.hid = @Hid", new { Hid = hid.ToLower() });
        }

        public async Task<PKSystem> GetByToken(string token) {
            return await conn.QuerySingleOrDefaultAsync<PKSystem>("select * from systems where token = @Token", new { Token = token });
        }
        
        public async Task<PKSystem> GetById(int id)
        {
            return await conn.QuerySingleOrDefaultAsync<PKSystem>("select * from systems where id = @Id", new { Id = id });
        }

        public async Task Save(PKSystem system) {
            await conn.ExecuteAsync("update systems set name = @Name, description = @Description, tag = @Tag, avatar_url = @AvatarUrl, token = @Token, ui_tz = @UiTz where id = @Id", system);
        }

        public async Task Delete(PKSystem system) {
            await conn.ExecuteAsync("delete from systems where id = @Id", system);
        }        

        public async Task<IEnumerable<ulong>> GetLinkedAccountIds(PKSystem system)
        {
            return await conn.QueryAsync<ulong>("select uid from accounts where system = @Id", new { Id = system.Id });
        }
    }

    public class MemberStore {
        private IDbConnection conn;

        public MemberStore(IDbConnection conn) {
            this.conn = conn;
        }

        public async Task<PKMember> Create(PKSystem system, string name) {
            // TODO: handle collision
            var hid = Utils.GenerateHid();
            return await conn.QuerySingleAsync<PKMember>("insert into members (hid, system, name) values (@Hid, @SystemId, @Name) returning *", new {
                Hid = hid,
                SystemID = system.Id,
                Name = name
            });
        }

        public async Task<PKMember> GetByHid(string hid) {
            return await conn.QuerySingleOrDefaultAsync<PKMember>("select * from members where hid = @Hid", new { Hid = hid.ToLower() });
        }

        public async Task<PKMember> GetByName(PKSystem system, string name) {
            // QueryFirst, since members can (in rare cases) share names
            return await conn.QueryFirstOrDefaultAsync<PKMember>("select * from members where lower(name) = lower(@Name) and system = @SystemID", new { Name = name, SystemID = system.Id });
        }

        public async Task<ICollection<PKMember>> GetUnproxyableMembers(PKSystem system) {
            return (await GetBySystem(system))
                .Where((m) => {
                    var proxiedName = $"{m.Name} {system.Tag}";
                    return proxiedName.Length > 32 || proxiedName.Length < 2;
                }).ToList();
        }

        public async Task<IEnumerable<PKMember>> GetBySystem(PKSystem system) {
            return await conn.QueryAsync<PKMember>("select * from members where system = @SystemID", new { SystemID = system.Id });
        }

        public async Task Save(PKMember member) {
            await conn.ExecuteAsync("update members set name = @Name, description = @Description, color = @Color, avatar_url = @AvatarUrl, birthday = @Birthday, pronouns = @Pronouns, prefix = @Prefix, suffix = @Suffix where id = @Id", member);
        }

        public async Task Delete(PKMember member) {
            await conn.ExecuteAsync("delete from members where id = @Id", member);
        }

        public async Task<int> MessageCount(PKMember member)
        {
            return await conn.QuerySingleAsync<int>("select count(*) from messages where member = @Id", member);
        }
    }

    public class MessageStore {
        public struct PKMessage
        {
            public ulong Mid;
            public ulong Channel;
            public ulong Sender;
        }
        public class StoredMessage
        {
            public PKMessage Message;
            public PKMember Member;
            public PKSystem System;
        }

        private IDbConnection _connection;

        public MessageStore(IDbConnection connection) {
            this._connection = connection;
        }

        public async Task Store(ulong senderId, ulong messageId, ulong channelId, PKMember member) {
            await _connection.ExecuteAsync("insert into messages(mid, channel, member, sender) values(@MessageId, @ChannelId, @MemberId, @SenderId)", new {
                MessageId = messageId,
                ChannelId = channelId,
                MemberId = member.Id,
                SenderId = senderId
            });      
        }

        public async Task<StoredMessage> Get(ulong id)
        {
            return (await _connection.QueryAsync<PKMessage, PKMember, PKSystem, StoredMessage>("select messages.*, members.*, systems.* from messages, members, systems where mid = @Id and messages.member = members.id and systems.id = members.system", (msg, member, system) => new StoredMessage
            {
                Message = msg,
                System = system,
                Member = member
            }, new { Id = id })).FirstOrDefault();
        }
        
        public async Task Delete(ulong id) {
            await _connection.ExecuteAsync("delete from messages where mid = @Id", new { Id = id });
        }
    }

    public class SwitchStore
    {
        private IDbConnection _connection;

        public SwitchStore(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task RegisterSwitch(PKSystem system, IEnumerable<PKMember> members)
        {
            // Use a transaction here since we're doing multiple executed commands in one
            using (var tx = _connection.BeginTransaction())
            {
                // First, we insert the switch itself
                var sw = await _connection.QuerySingleAsync<PKSwitch>("insert into switches(system) values (@System) returning *",
                    new {System = system.Id});
                
                // Then we insert each member in the switch in the switch_members table
                // TODO: can we parallelize this or send it in bulk somehow?
                foreach (var member in members)
                {
                    await _connection.ExecuteAsync(
                        "insert into switch_members(switch, member) values(@Switch, @Member)",
                        new {Switch = sw.Id, Member = member.Id});
                }
                
                // Finally we commit the tx, since the using block will otherwise rollback it
                tx.Commit();
            }
        }

        public async Task<IEnumerable<PKSwitch>> GetSwitches(PKSystem system, int count)
        {
            // TODO: refactor the PKSwitch data structure to somehow include a hydrated member list
            // (maybe when we get caching in?)
            return await _connection.QueryAsync<PKSwitch>("select * from switches where system = @System order by timestamp desc limit @Count", new {System = system.Id, Count = count});
        }

        public async Task<IEnumerable<PKMember>> GetSwitchMembers(PKSwitch sw)
        {
            return await _connection.QueryAsync<PKMember>(
                "select * from switch_members, members where switch_members.member = members.id and switch_members.switch = @Switch",
                new {Switch = sw.Id});
        }

        public async Task<PKSwitch> GetLatestSwitch(PKSystem system) => (await GetSwitches(system, 1)).FirstOrDefault();

        public async Task MoveSwitch(PKSwitch sw, Instant time)
        {
            await _connection.ExecuteAsync("update switches set timestamp = @Time where id = @Id",
                new {Time = time, Id = sw.Id});
        }

        public async Task DeleteSwitch(PKSwitch sw)
        {
            await _connection.ExecuteAsync("delete from switches where id = @Id", new {Id = sw.Id});
        }
    }
}