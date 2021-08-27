using System.Collections.Generic;

using Myriad.Types;

namespace Myriad.Builders
{
    public class EmbedBuilder
    {
        private Embed _embed = new();
        private readonly List<Embed.Field> _fields = new();

        public EmbedBuilder Title(string? title)
        {
            _embed = _embed with { Title = title };
            return this;
        }

        public EmbedBuilder Description(string? description)
        {
            _embed = _embed with { Description = description };
            return this;
        }

        public EmbedBuilder Url(string? url)
        {
            _embed = _embed with { Url = url };
            return this;
        }

        public EmbedBuilder Color(uint? color)
        {
            _embed = _embed with { Color = color };
            return this;
        }

        public EmbedBuilder Footer(Embed.EmbedFooter? footer)
        {
            _embed = _embed with
            {
                Footer = footer
            };
            return this;
        }

        public EmbedBuilder Image(Embed.EmbedImage? image)
        {
            _embed = _embed with
            {
                Image = image
            };
            return this;
        }


        public EmbedBuilder Thumbnail(Embed.EmbedThumbnail? thumbnail)
        {
            _embed = _embed with
            {
                Thumbnail = thumbnail
            };
            return this;
        }

        public EmbedBuilder Author(Embed.EmbedAuthor? author)
        {
            _embed = _embed with
            {
                Author = author
            };
            return this;
        }

        public EmbedBuilder Timestamp(string? timestamp)
        {
            _embed = _embed with
            {
                Timestamp = timestamp
            };
            return this;
        }

        public EmbedBuilder Field(Embed.Field field)
        {
            _fields.Add(field);
            return this;
        }

        public Embed Build() =>
            _embed with { Fields = _fields.ToArray() };
    }
}