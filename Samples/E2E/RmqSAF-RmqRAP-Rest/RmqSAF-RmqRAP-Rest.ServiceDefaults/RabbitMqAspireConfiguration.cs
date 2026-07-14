using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

public static class RabbitMqAspireConfiguration
{
    public static void ApplyRabbitMqConnectionString(
        ConfigurationManager configuration,
        params (string Section, string Profile)[] profiles)
    {
        var connectionString = configuration.GetConnectionString("rabbitmq");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        if (!TryParseConnectionString(connectionString, out var settings))
        {
            return;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (section, profile) in profiles)
        {
            values[$"{section}:{profile}:HostName"] = settings.HostName;
            values[$"{section}:{profile}:Port"] = settings.Port.ToString();
            values[$"{section}:{profile}:UserName"] = settings.UserName;
            values[$"{section}:{profile}:Password"] = settings.Password;
            values[$"{section}:{profile}:VirtualHost"] = settings.VirtualHost;
        }

        configuration.AddInMemoryCollection(values);
    }

    private static bool TryParseConnectionString(string connectionString, out RabbitMqConnectionSettings settings)
    {
        settings = default!;
        try
        {
            var uri = connectionString.Contains("://", StringComparison.Ordinal)
                ? new Uri(connectionString)
                : new Uri($"amqp://{connectionString}");

            var userInfo = uri.UserInfo.Split(':', 2);
            settings = new RabbitMqConnectionSettings(
                uri.Host,
                uri.Port > 0 ? uri.Port : 5672,
                userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "guest",
                userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "guest",
                string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/"
                    ? "/"
                    : Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')));

            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private readonly record struct RabbitMqConnectionSettings(
        string HostName,
        int Port,
        string UserName,
        string Password,
        string VirtualHost);
}
