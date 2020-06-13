using System.Threading.Tasks;

namespace PluralKit.Core
{
    public interface IDatabase
    {
        Task<IPKConnection> Obtain();
    }
}