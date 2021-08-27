using System.IO;

namespace Myriad.Rest.Types
{
    public record MultipartFile(string Filename, Stream Data);
}