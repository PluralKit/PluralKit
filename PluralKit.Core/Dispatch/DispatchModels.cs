using System.Net;
using System.Net.Sockets;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PluralKit.Core;

public enum DispatchEvent
{
    PING,
    UPDATE_SYSTEM,
    UPDATE_SETTINGS,
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
    DELETE_SWITCH,
    DELETE_ALL_SWITCHES,
    SUCCESSFUL_IMPORT,
    UPDATE_AUTOPROXY,
}

public struct UpdateDispatchData
{
    public DispatchEvent Event;
    public string SystemId;
    public string? EntityId;
    public string SigningToken;
    public JObject? EventData;
}

public static class DispatchExt
{
    public static string GetPayloadBody(this UpdateDispatchData data)
    {
        var o = new JObject();

        o.Add("type", data.Event.ToString());
        o.Add("signing_token", data.SigningToken);
        o.Add("system_id", data.SystemId);
        o.Add("id", data.EntityId);
        o.Add("data", data.EventData);

        return JsonConvert.SerializeObject(o);
    }

    public static string GetPingBody(string systemId, string token)
    {
        var o = new JObject();

        o.Add("type", "PING");
        o.Add("signing_token", token);
        o.Add("system_id", systemId);

        return JsonConvert.SerializeObject(o);
    }

    private static List<IPNetwork> _privateNetworks = new()
    {
        IPNetwork.Parse("10.0.0.0/8"), // 10/8
        IPNetwork.Parse("192.168.0.0/16"), // 192.168/16
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
    };

    public static async Task<bool> ValidateUri(string url)
    {
        IPHostEntry host = null;

        try
        {
            var uri = new Uri(url);
            if (uri.Scheme != "https") return false;
            host = await Dns.GetHostEntryAsync(uri.DnsSafeHost);
        }
        catch (Exception)
        {
            return false;
        }

        if (host == null || host.AddressList.Length == 0)
            return false;

#pragma warning disable CS0618

        foreach (var address in host.AddressList.Where(address =>
                     address.AddressFamily is AddressFamily.InterNetwork))
        {
            if (_privateNetworks.Any(net => net.Contains(address)))
                return false;
        }

        if (host.AddressList.Any(address => address.IsIPv6LinkLocal))
            return false;

        // we only support IPv4 in prod :(
        return host.AddressList.Any(address => address.AddressFamily is AddressFamily.InterNetwork);
    }
}