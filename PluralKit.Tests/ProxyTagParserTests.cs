#nullable enable
using PluralKit.Bot;
using PluralKit.Core;

using Xunit;

namespace PluralKit.Tests;

public class ProxyTagParserTests
{
    internal static ProxyMatch AssertMatch(IEnumerable<ProxyMember> members, string input, string? name = null,
                                           string? prefix = null, string? suffix = null, string? content = null)
    {
        Assert.True(new ProxyTagParser().TryMatch(members, input, out var result));
        if (name != null) Assert.Equal(name, result.Member.Name);
        if (prefix != null) Assert.Equal(prefix, result.ProxyTags?.Prefix);
        if (suffix != null) Assert.Equal(suffix, result.ProxyTags?.Suffix);
        if (content != null) Assert.Equal(content, result.Content);
        return result;
    }

    internal static void AssertNoMatch(IEnumerable<ProxyMember> members, string? input)
    {
        Assert.False(new ProxyTagParser().TryMatch(members, input, out _));
    }

    public class Basics
    {
        private readonly ProxyMember[] members =
        {
            new("John", new ProxyTag("[", "]")),
            new("Bob", new ProxyTag("{", "}"), new ProxyTag("<", ">")),
            new("Prefixed", new ProxyTag("A:", "")),
            new("Tagless")
        };

        [Fact]
        public void StringWithoutAnyTagsMatchesNothing() =>
            // Note that we have "Tagless" with no proxy tags
            AssertNoMatch(members, "string without any tags");

        [Theory]
        [InlineData("[john's tags]")]
        [InlineData("{bob's tags}")]
        [InlineData("A:tag with prefix")]
        public void StringWithTagsMatch(string input) =>
            AssertMatch(members, input);

        [Theory]
        [InlineData("[john's tags]", "John")]
        [InlineData("{bob's tags}", "Bob")]
        [InlineData("A:tag with prefix", "Prefixed")]
        public void MatchReturnsCorrespondingMember(string input, string expectedName) =>
            AssertMatch(members, input, expectedName);

        [Theory]
        [InlineData("[text inside]", "text inside")]
        [InlineData("A:text after", "text after")]
        public void ContentBetweenTagsIsExtracted(string input, string expectedContent) =>
            AssertMatch(members, input, content: expectedContent);

        [Theory]
        [InlineData("[john's tags]", "[", "]")]
        [InlineData("{bob's tags}", "{", "}")]
        [InlineData("<also bob's tags>", "<", ">")]
        public void ReturnedTagMatchesInput(string input, string expectedPrefix, string expectedSuffix) =>
            AssertMatch(members, input, prefix: expectedPrefix, suffix: expectedSuffix);

        [Theory]
        [InlineData("[tags at the start] but more text here")]
        [InlineData("text at the start [but tags after]")]
        [InlineData("something A:prefix")]
        public void TagsOnlyMatchAtTheStartAndEnd(string input) =>
            AssertNoMatch(members, input);

        [Theory]
        [InlineData("[   text     ]", "   text     ")]
        [InlineData("A: text", " text")]
        public void WhitespaceInContentShouldNotBeTrimmed(string input, string expectedContent) =>
            AssertMatch(members, input, content: expectedContent);
    }

    public class MentionPrefix
    {
        private readonly ProxyMember[] members =
        {
            new("John", new ProxyTag("[", "]")),
            new("Suffix only", new ProxyTag("", "-Q"))
        };

        public void MentionAtStartGetsMovedIntoTags() =>
            AssertMatch(members, "<@466378653216014359>[some text]", content: "some text");

        public void SpacesBetweenMentionAndTagsAreAllowed() =>
            AssertMatch(members, "<@466378653216014359> [some text]", content: "some text");

        public void MentionMovingTakesPrecedenceOverTagMatching() =>
            // (as opposed to content: "<@466378653216014359> some text")
            // which would also be valid, but the tags should be moved first
            AssertMatch(members, "<@466378653216014359> some text -Q", content: "some text");

        public void AlternateMentionSyntaxAlsoAccepted() =>
            AssertMatch(members, "<@466378653216014359> [some text]", content: "some text");
    }

    public class Specificity
    {
        private readonly ProxyMember[] members =
        {
            new("Level One", new ProxyTag("[", "]")),
            new("Level Two", new ProxyTag("[[", "]]")),
            new("Level Three", new ProxyTag("[[[", "]]]"))
        };

        [Theory]
        [InlineData("[just one]", "Level One")]
        [InlineData("[[getting deeper]]", "Level Two")]
        [InlineData("[[[way too deep]]]", "Level Three")]
        [InlineData("[[unmatched brackets]]]", "Level Two")]
        [InlineData("[more unmatched brackets]]]]]]", "Level One")]
        public void MostSpecificTagsAreMatched(string input, string expectedName) =>
            AssertMatch(members, input, expectedName);
    }

    public class EmptyInput
    {
        private readonly ProxyMember[] members = { new("Something", new ProxyTag("[", "]")) };

        [Theory]
        [InlineData("")]
        [InlineData("some text")]
        [InlineData("{bogus tags, idk}")]
        public void NoMembersMatchNothing(string input) =>
            AssertNoMatch(new ProxyMember[] { }, input);

        [Fact]
        public void EmptyStringMatchesNothing() =>
            AssertNoMatch(members, "");

        [Fact]
        public void NullStringMatchesNothing() =>
            AssertNoMatch(members, null);
    }

    public class TagSpaceHandling
    {
        private readonly ProxyMember[] members =
        {
            new("Tags without spaces", new ProxyTag("[", "]")),
            new("Tags with spaces", new ProxyTag("{ ", " }")),
            new("Spaced prefix tag", new ProxyTag("A: ", ""))
        };


        [Fact]
        public void TagsWithoutSpacesAlwaysMatch()
        {
            AssertMatch(members, "[no spaces inside tags]");
            AssertMatch(members, "[ spaces inside tags ]");
        }

        [Fact]
        public void TagsWithSpacesOnlyMatchWithSpaces()
        {
            AssertMatch(members, "{ spaces in here }");
            AssertNoMatch(members, "{no spaces}");

            AssertMatch(members, "A: text here");
            AssertNoMatch(members, "A:same text without spaces");
        }

        [Fact]
        public void SpacesBeforePrefixOrAfterSuffixAlsoCount()
        {
            AssertNoMatch(members, " A: text here");
            AssertNoMatch(members, "{ something something }  ");
        }

        [Fact]
        public void TagsWithSpacesStillMatchWithoutSpacesIfTheContentIsEmpty()
        {
            AssertMatch(members, "A:");
            AssertMatch(members, "{}");
        }
    }
}