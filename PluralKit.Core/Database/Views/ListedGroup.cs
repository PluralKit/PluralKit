using Newtonsoft.Json;

namespace PluralKit.Core
{
    public class ListedGroup : PKGroup
    {
        [JsonIgnore] public int MemberCount { get; }
    }
}