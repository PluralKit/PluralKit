using Xunit;

namespace PluralKit.Tests
{
    [CollectionDefinition(nameof(TestCollection))]
    public class TestCollection: ICollectionFixture<TestFixture>
    {
        
    }
}