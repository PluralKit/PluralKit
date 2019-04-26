using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;

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

        public async Task<PKSystem> GetByAccount(ulong accountId) {
            return await conn.QuerySingleAsync<PKSystem>("select systems.* from systems, accounts where accounts.system = system.id and accounts.uid = @Id", new { Id = accountId });
        }

        public async Task<PKSystem> GetByHid(string hid) {
            return await conn.QuerySingleAsync<PKSystem>("select * from systems where systems.hid = @Hid", new { Hid = hid.ToLower() });
        }

        public async Task<PKSystem> GetByToken(string token) {
            return await conn.QuerySingleAsync<PKSystem>("select * from systems where token = @Token", new { Token = token });
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
            return await conn.QuerySingleAsync("insert into members (hid, system, name) values (@Hid, @SystemId, @Name) returning *", new {
                Hid = hid,
                SystemID = system.Id,
                Name = name
            });
        }

        public async Task<PKMember> GetByHid(string hid) {
            return await conn.QuerySingleAsync("select * from members where hid = @Hid", new { Hid = hid.ToLower() });
        }

        public async Task<PKMember> GetByName(string name) {
            return await conn.QuerySingleAsync("select * from members where lower(name) = lower(@Name)", new { Name = name });
        }

        public async Task<PKMember> GetByNameConstrained(PKSystem system, string name) {
            return await conn.QuerySingleAsync("select * from members where lower(name) = @Name and system = @SystemID", new { Name = name, SystemID = system.Id });
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
            await conn.UpdateAsync(member);
        }

        public async Task Delete(PKMember member) {
            await conn.DeleteAsync(member);
        }
    }

    public class MessageStore {
        public class StoredMessage {
            public ulong Mid;
            public ulong ChannelId;
            public ulong SenderId;
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

        public async Task<StoredMessage> Get(ulong id) {
            return (await _connection.QueryAsync<StoredMessage, PKMember, PKSystem, StoredMessage>("select * from messages, members, systems where mid = @Id and messages.member = members.id and systems.id = members.system", (msg, member, system) => {
                msg.System = system;
                msg.Member = member;
                return msg;
            }, new { Id = id })).First();
        }
        
        public async Task Delete(ulong id) {
            await _connection.ExecuteAsync("delete from messages where mid = @Id", new { Id = id });
        }
    }
}