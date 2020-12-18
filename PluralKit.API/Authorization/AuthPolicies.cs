namespace PluralKit.API
{
    public class AuthPolicies
    {
        public const string EditSystem = nameof(EditSystem);
        public const string ViewSystemMembers = nameof(ViewSystemMembers);
        public const string ViewSystemGroups = nameof(ViewSystemGroups);
        public const string ViewFront = nameof(ViewFront);
        public const string ViewFrontHistory = nameof(ViewFrontHistory);
        
        public const string EditMember = nameof(EditMember);
        public const string DeleteMember = nameof(DeleteMember);
        
        public const string EditGroup = nameof(EditGroup);
        public const string DeleteGroup = nameof(DeleteGroup);
        
        public const string EditSwitch = nameof(EditSwitch);
        public const string DeleteSwitch = nameof(DeleteSwitch);

        public const string ViewGroupMembers = nameof(ViewGroupMembers);
    }
}