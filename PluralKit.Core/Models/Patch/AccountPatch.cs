namespace PluralKit.Core
{
    public class AccountPatch: PatchObject
    {
        public Partial<bool> AllowAutoproxy { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("allow_autoproxy", AllowAutoproxy);
    }
}