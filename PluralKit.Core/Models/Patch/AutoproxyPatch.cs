namespace PluralKit.Core
{
	public class AutoproxyPatch: PatchObject
	{

        public Partial<AutoproxyMode> AutoproxyMode { get; set; }
        public Partial<MemberId?> AutoproxyMember { get; set; }
		public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("mode", AutoproxyMode)
            .With("member", AutoproxyMember);
	}
}