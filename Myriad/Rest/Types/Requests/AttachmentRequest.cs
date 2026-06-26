using Myriad.Types;

namespace Myriad.Rest.Types.Requests;

public record AttachmentRequest
{
    public ulong Id { get; init; }
    public string Filename { get; init; }
    public string? Description { get; init; }
    public string? Waveform { get; init; }
    public float? DurationSecs { get; init; }
    public bool? IsSpoiler { get; init; }
}