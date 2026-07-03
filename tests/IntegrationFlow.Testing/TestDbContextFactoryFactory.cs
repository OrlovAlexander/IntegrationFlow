namespace IntegrationFlow.Testing;

public static class TestDbContextFactoryFactory
{
    public static TestDbContextFactory Create(string databaseName)
        => TestDbContextFactory.Create(databaseName);
}
