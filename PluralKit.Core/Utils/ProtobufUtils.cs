using Google.Protobuf;

namespace PluralKit.Core;

public static class Proto
{
    private static readonly Dictionary<string, MessageParser> _parser = new();

    public static byte[] Marshal(this IMessage message) => message.ToByteArray();

    public static T Unmarshal<T>(this byte[] message) where T : IMessage<T>, new()
    {
        var type = typeof(T).ToString();
        if (_parser.ContainsKey(type))
        {
            return (T)_parser[type].ParseFrom(message);
        }

        _parser.Add(type, new MessageParser<T>(() => new T()));
        return Unmarshal<T>(message);
    }
}