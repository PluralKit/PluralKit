using NodaTime;

namespace PluralKit.Core;

public class PKApiKey
{
    public Guid Id { get; private set; }
    public SystemId System { get; private set; }
    public string Kind { get; private set; }
    public string[] Scopes { get; private set; }
    public Guid? App { get; private set; }
    public string Name { get; private set; }

    public Instant Created { get; private set; }
}