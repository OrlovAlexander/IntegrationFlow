using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;

namespace IntegrationFlow.Testing;

public static class TempRabbitMqConfigWriter
{
    public static string WriteConsumeProfile(
        string profileName,
        string queueName,
        string hostName,
        int port,
        ushort prefetchCount = 1,
        bool requeueOnFailure = false,
        int maxRetryCount = 0)
    {
        return WriteConsumeProfiles(
            new[] { (profileName, queueName) },
            hostName,
            port,
            prefetchCount,
            requeueOnFailure,
            maxRetryCount);
    }

    public static string WriteConsumeProfiles(
        IReadOnlyList<(string ProfileName, string QueueName)> profiles,
        string hostName,
        int port,
        ushort prefetchCount = 1,
        bool requeueOnFailure = false,
        int maxRetryCount = 0)
    {
        if (profiles.Count == 0)
        {
            throw new ArgumentException("At least one profile is required.", nameof(profiles));
        }

        var path = Path.Combine(AppContext.BaseDirectory, RabbitMqConfigurationLoader.DefaultFileName);
        var profilesJson = string.Join(
            "," + Environment.NewLine,
            profiles.Select(profile => $$"""
                "{{profile.ProfileName}}": {
                  "HostName": "{{hostName}}",
                  "Port": {{port}},
                  "UserName": "{{RabbitMqTestCredentials.Username}}",
                  "Password": "{{RabbitMqTestCredentials.Password}}",
                  "VirtualHost": "/",
                  "QueueName": "{{profile.QueueName}}",
                  "PrefetchCount": {{prefetchCount}},
                  "Asynchronously": true,
                  "AutomaticRecoveryEnabled": true,
                  "RequeueOnFailure": {{requeueOnFailure.ToString().ToLowerInvariant()}},
                  "MaxRetryCount": {{maxRetryCount}}
                }
                """));

        var json = $$"""
            {
              "RabbitMq": {
            {{profilesJson}}
              }
            }
            """;
        File.WriteAllText(path, json);
        return path;
    }

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
                  "UserName": "{{RabbitMqTestCredentials.Username}}",
                  "Password": "{{RabbitMqTestCredentials.Password}}",
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
