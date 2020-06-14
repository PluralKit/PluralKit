#nullable enable
using PluralKit.Bot;
using PluralKit.Core;

using Xunit;

namespace PluralKit.Tests
{
    public class ProxyTagParserTests
    {
        private ProxyTagParser parser = new ProxyTagParser();
        private ProxyMember[] members = {
            new ProxyMember("Tagless"), 
            new ProxyMember("John", new ProxyTag("[", "]")),
            new ProxyMember("Curly", new ProxyTag("{", "}")),
            new ProxyMember("Specific", new ProxyTag("{{", "}}")),
            new ProxyMember("SuperSpecific", new ProxyTag("{{{", "}}}")),
            new ProxyMember("Manytags", new ProxyTag("-", "-"), new ProxyTag("<", ">")),
            new ProxyMember("Lopsided", new ProxyTag("-", "")),
            new ProxyMember("Othersided", new ProxyTag("", "-"))
        };

        [Fact]
        public void EmptyStringMatchesNothing() =>
            Assert.False(parser.TryMatch(members, "", out _));

        [Fact]
        public void NullStringMatchesNothing() =>
            Assert.False(parser.TryMatch(members, null, out _));

        [Fact]
        public void PlainStringMatchesNothing() =>
            // Note that we have "Tagless" with no proxy tags
            Assert.False(parser.TryMatch(members, "string without any of the tags", out _));

        [Fact]
        public void StringWithBasicTagsMatch() =>
            Assert.True(parser.TryMatch(members, "[these are john's tags]", out _));

        [Theory]
        [InlineData("[these are john's tags]", "John")]
        [InlineData("-lopsided tags on the left", "Lopsided")]
        [InlineData("lopsided tags on the right-", "Othersided")]
        public void MatchReturnsCorrectMember(string input, string expectedName)
        {
            parser.TryMatch(members, input, out var result);
            Assert.Equal(expectedName, result.Member.Name);
        }

        [Fact]
        public void MatchReturnsCorrectContent()
        {
            parser.TryMatch(members, "[these are john's tags]", out var result);
            Assert.Equal("these are john's tags", result.Content);
        }

        [Theory]
        [InlineData("{just curly}", "Curly", "just curly")]
        [InlineData("{{getting deeper}}", "Specific", "getting deeper")]
        [InlineData("{{{way too deep}}}", "SuperSpecific", "way too deep")]
        [InlineData("{{unmatched brackets}}}", "Specific", "unmatched brackets}")]
        [InlineData("{more unmatched brackets}}}}}", "Curly", "more unmatched brackets}}}}")]
        public void MostSpecificTagsAreMatched(string input, string expectedName, string expectedContent)
        {
            Assert.True(parser.TryMatch(members, input, out var result));
            Assert.Equal(expectedName, result.Member.Name);
            Assert.Equal(expectedContent, result.Content);
        }

        [Theory]
        [InlineData("")]
        [InlineData("some text")]
        [InlineData("{bogus tags, idk}")]
        public void NoMembersMatchNothing(string input) => 
            Assert.False(parser.TryMatch(new ProxyMember[]{}, input, out _));
    }
}