namespace Myriad.Gateway.State
{
    public enum ShardState
    {
        Disconnected,
        Handshaking,
        Identifying,
        Connected,
        Reconnecting
    }
}