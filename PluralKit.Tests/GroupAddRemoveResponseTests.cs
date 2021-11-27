using PluralKit.Bot;
using PluralKit.Core;

using Xunit;

namespace PluralKit.Tests;

public class GroupAddRemoveResponseTests
{
    private static readonly Func<Groups.AddRemoveOperation, int, int, int, int, string>
        func = GroupMemberUtils.GenerateResponse;

    private static readonly Groups.AddRemoveOperation addOp = Groups.AddRemoveOperation.Add;
    private static readonly Groups.AddRemoveOperation removeOp = Groups.AddRemoveOperation.Remove;
    private static readonly string success = Emojis.Success;
    private static readonly string failure = Emojis.Error;

    public class AddOp
    {
        public class MemberGroup
        {
            [Fact]
            public void Success()
                => Assert.Equal(
                    $"{success} Member added to group.",
                    func(addOp, 1, 1, 1, 0)
                );

            [Fact]
            public void Failure()
                => Assert.Equal(
                    $"{failure} Member not added to group (member already in group).",
                    func(addOp, 1, 1, 0, 1)
                );
        }

        public class MemberGroups
        {
            [Fact]
            public void Success()
                => Assert.Equal(
                    $"{success} Member added to groups.",
                    func(addOp, 1, 2, 2, 0)
                );

            [Fact]
            public void PartialSuccess1()
                => Assert.Equal(
                    $"{success} Member added to 2 groups (member already in 1 group).",
                    func(addOp, 1, 3, 2, 1)
                );

            [Fact]
            public void PartialSuccess2()
                => Assert.Equal(
                    $"{success} Member added to 1 group (member already in 2 groups).",
                    func(addOp, 1, 3, 1, 2)
                );

            [Fact]
            public void Failure()
                => Assert.Equal(
                    $"{failure} Member not added to groups (member already in groups).",
                    func(addOp, 1, 2, 0, 2)
                );
        }

        public class MembersGroup
        {
            [Fact]
            public void Success()
                => Assert.Equal(
                    $"{success} Members added to group.",
                    func(addOp, 2, 1, 2, 0)
                );

            [Fact]
            public void PartialSuccess1()
                => Assert.Equal(
                    $"{success} 2 members added to group (1 member already in group).",
                    func(addOp, 3, 1, 2, 1)
                );

            [Fact]
            public void PartialSuccess2()
                => Assert.Equal(
                    $"{success} 1 member added to group (2 members already in group).",
                    func(addOp, 3, 1, 1, 2)
                );

            [Fact]
            public void Failure()
                => Assert.Equal(
                    $"{failure} Members not added to group (members already in group).",
                    func(addOp, 2, 1, 0, 2)
                );
        }
    }

    public class RemoveOp
    {
        public class MemberGroup
        {
            [Fact]
            public void Success()
                => Assert.Equal(
                    $"{success} Member removed from group.",
                    func(removeOp, 1, 1, 1, 0)
                );

            [Fact]
            public void Failure()
                => Assert.Equal(
                    $"{failure} Member not removed from group (member already not in group).",
                    func(removeOp, 1, 1, 0, 1)
                );
        }

        public class MemberGroups
        {
            [Fact]
            public void Success()
                => Assert.Equal(
                    $"{success} Member removed from groups.",
                    func(removeOp, 1, 3, 3, 0)
                );

            [Fact]
            public void PartialSuccess1()
                => Assert.Equal(
                    $"{success} Member removed from 1 group (member already not in 2 groups).",
                    func(removeOp, 1, 3, 1, 2)
                );

            [Fact]
            public void PartialSuccess2()
                => Assert.Equal(
                    $"{success} Member removed from 2 groups (member already not in 1 group).",
                    func(removeOp, 1, 3, 2, 1)
                );

            [Fact]
            public void Failure()
                => Assert.Equal(
                    $"{failure} Member not removed from groups (member already not in groups).",
                    func(removeOp, 1, 3, 0, 3)
                );
        }

        public class MembersGroup
        {
            [Fact]
            public void Success()
                => Assert.Equal(
                    $"{success} Members removed from group.",
                    func(removeOp, 2, 1, 2, 0)
                );

            [Fact]
            public void PartialSuccess1()
                => Assert.Equal(
                    $"{success} 1 member removed from group (2 members already not in group).",
                    func(removeOp, 3, 1, 1, 2)
                );

            [Fact]
            public void PartialSuccess2()
                => Assert.Equal(
                    $"{success} 2 members removed from group (1 member already not in group).",
                    func(removeOp, 3, 1, 2, 1)
                );

            [Fact]
            public void Failure()
                => Assert.Equal(
                    $"{failure} Members not removed from group (members already not in group).",
                    func(removeOp, 2, 1, 0, 2)
                );
        }
    }
}