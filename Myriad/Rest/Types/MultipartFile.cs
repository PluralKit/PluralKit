namespace Myriad.Rest.Types;

public record MultipartFile(string Filename, Stream Data, string? Description);