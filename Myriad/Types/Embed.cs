namespace Myriad.Types
{
    public record Embed
    {
        public string? Title { get; init; }
        public string? Type { get; init; }
        public string? Description { get; init; }
        public string? Url { get; init; }
        public string? Timestamp { get; init; }
        public uint? Color { get; init; }
        public EmbedFooter? Footer { get; init; }
        public EmbedImage? Image { get; init; }
        public EmbedThumbnail? Thumbnail { get; init; }
        public EmbedVideo? Video { get; init; }
        public EmbedProvider? Provider { get; init; }
        public EmbedAuthor? Author { get; init; }
        public Field[]? Fields { get; init; }

        public record EmbedFooter(
            string Text,
            string? IconUrl = null,
            string? ProxyIconUrl = null
        );

        public record EmbedImage(
            string? Url,
            uint? Width = null,
            uint? Height = null
        );

        public record EmbedThumbnail(
            string? Url,
            string? ProxyUrl = null,
            uint? Width = null,
            uint? Height = null
        );

        public record EmbedVideo(
            string? Url,
            uint? Width = null,
            uint? Height = null
        );

        public record EmbedProvider(
            string? Name,
            string? Url
        );

        public record EmbedAuthor(
            string? Name = null,
            string? Url = null,
            string? IconUrl = null,
            string? ProxyIconUrl = null
        );

        public record Field(
            string Name,
            string Value,
            bool Inline = false
        );
    }
}