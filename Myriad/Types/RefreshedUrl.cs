namespace Myriad.Types;

public record RefreshedUrlsResponse
{
    public record RefreshedUrl
    {
        public string Original;
        public string Refreshed;
    }
    public RefreshedUrl[] RefreshedUrls;
}