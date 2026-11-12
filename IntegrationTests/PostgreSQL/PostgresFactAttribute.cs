namespace IntegrationTests.PostgreSQL;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DELIVERY_TEST_CONNECTION")))
            Skip = "Configure DELIVERY_TEST_CONNECTION para probar en una base temporal PostgreSQL/PostGIS.";
    }
}
