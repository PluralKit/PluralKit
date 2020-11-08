using System.Threading.Tasks;

using Autofac;

using Dapper;

using PluralKit.Core;

using Xunit;

namespace PluralKit.Tests.Integration
{
    public class BasicTest: BaseTest
    {
        public BasicTest(TestFixture fixture): base(fixture) { }

        [Fact]
        public async Task DatabaseTest()
        {
            var db = Services.Resolve<IDatabase>();
            await using var conn = await db.Obtain();
            await conn.ExecuteAsync("select * from systems");
        } 
    }
}