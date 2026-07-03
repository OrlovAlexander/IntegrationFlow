using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;

namespace IntegrationFlow.Testing;

public static class TempRabbitMqConfigWriter
{
    public static string WritePublishProfile(
        string profileName,
        string queueName,
        string hostName,
        int port)
    {
        var path = Path.Combine(AppContext.BaseDirectory, RabbitMqPublishConfigurationLoader.DefaultFileName);
        var json = $$"""
            {
              "RabbitMqPublish": {
                "{{profileName}}": {
                  "HostName": "{{hostName}}",
                  "Port": {{port}},
                  "UserName": "guest",
                  "Password": "guest",
                  "QueueName": "{{queueName}}",
                  "PublishTarget": "Queue",
                  "PublisherConfirmsEnabled": true,
                  "ValidateTopology": false
                }
              }
            }
            """;
        File.WriteAllText(path, json);
        return path;
    }
}
