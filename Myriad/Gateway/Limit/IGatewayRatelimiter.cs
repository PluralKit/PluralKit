using System.Threading.Tasks;

namespace Myriad.Gateway.Limit
{
    public interface IGatewayRatelimiter
    {
        public Task Identify(int shard);
    }
}