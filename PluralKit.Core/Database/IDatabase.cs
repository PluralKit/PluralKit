using System.Threading.Tasks;

namespace PluralKit.Core
{
    public interface IDatabase
    {
        Task ApplyMigrations();
        Task<IPKConnection> Obtain();
    }
}