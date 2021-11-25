using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

namespace PluralKit.Core
{
    public enum DispatchEvent
    {
        PING,
        UPDATE_SYSTEM,
        CREATE_MEMBER,
        UPDATE_MEMBER,
        DELETE_MEMBER,
        CREATE_GROUP,
        UPDATE_GROUP,
        UPDATE_GROUP_MEMBERS,
        DELETE_GROUP,
        LINK_ACCOUNT,
        UNLINK_ACCOUNT,
        UPDATE_SYSTEM_GUILD,
        UPDATE_MEMBER_GUILD,
        CREATE_MESSAGE,
        CREATE_SWITCH,
        UPDATE_SWITCH,
        UPDATE_SWITCH_MEMBERS,
        DELETE_SWITCH,
        DELETE_ALL_SWITCHES,
        SUCCESSFUL_IMPORT,
    }

    public struct UpdateDispatchData
    {
        public DispatchEvent Event;
        public string SystemId;
        public string? EntityId;
        public ulong? GuildId;
        public string SigningToken;
        public JObject? EventData;
    }

    public static class DispatchExt
    {
        public static StringContent GetPayloadBody(this UpdateDispatchData data)
        {
            var o = new JObject();

            o.Add("type", data.Event.ToString());
            o.Add("signing_token", data.SigningToken);
            o.Add("system_id", data.SystemId);
            o.Add("id", data.EntityId);
            o.Add("guild_id", data.GuildId);
            o.Add("data", data.EventData);

            return new StringContent(JsonConvert.SerializeObject(o));
        }

        public static JObject ToDispatchJson(this PKMessage msg, string memberRef)
        {
            var o = new JObject();

            o.Add("timestamp", Instant.FromUnixTimeMilliseconds((long)(msg.Mid >> 22) + 1420070400000).FormatExport());
            o.Add("id", msg.Mid.ToString());
            o.Add("original", msg.OriginalMid.ToString());
            o.Add("sender", msg.Sender.ToString());
            o.Add("channel", msg.Channel.ToString());
            o.Add("member", memberRef);

            return o;
        }

        public static async Task<bool> ValidateUri(string url)
        {
            IPHostEntry host = null;

            try
            {
                var uri = new Uri(url);
                host = await Dns.GetHostEntryAsync(uri.DnsSafeHost);
            }
            catch (Exception)
            {
                return false;
            }

            if (host == null || host.AddressList.Length == 0)
                return false;

#pragma warning disable CS0618

            foreach (var address in host.AddressList.Where(address => address.AddressFamily is AddressFamily.InterNetwork))
            {
                if ((address.Address & 0x7f000000) == 0x7f000000) // 127.0/8
                    return false;
                if ((address.Address & 0x0a000000) == 0x0a000000) // 10.0/8
                    return false;
                if ((address.Address & 0xa9fe0000) == 0xa9fe0000) // 169.254/16
                    return false;
                if ((address.Address & 0xac100000) == 0xac100000) // 172.16/12
                    return false;
            }

            if (host.AddressList.Any(address => address.IsIPv6LinkLocal))
                return false;

            // we only support IPv4 in prod :(
            return host.AddressList.Any(address => address.AddressFamily is AddressFamily.InterNetwork);
        }
    }
}