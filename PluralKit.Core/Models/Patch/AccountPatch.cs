namespace PluralKit.Core
{
	public class AccountPatch: PatchObject
	{
		public Partial<bool> DisableAutoproxy { get; set; }

		public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
			.With("disable_autoproxy", DisableAutoproxy);
	}
}