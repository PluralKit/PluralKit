using PluralKit.Core;

namespace PluralKit.Core
{
    public abstract class PatchObject<TKey, TObj>
    {
        public abstract UpdateQueryBuilder Apply(UpdateQueryBuilder b);
    }
}