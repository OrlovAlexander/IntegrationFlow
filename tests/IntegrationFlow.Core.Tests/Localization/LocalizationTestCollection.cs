using Xunit;

namespace IntegrationFlow.Tests.Localization;

[CollectionDefinition(Name)]
public sealed class LocalizationTestCollection : ICollectionFixture<LocalizationTestCollection>
{
    public const string Name = "Localization";
}
