var builder = DistributedApplication.CreateBuilder(args);

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

var storage = builder.AddProject<Projects.Storage_Api>("storage")
    .WithHttpHealthCheck("/health");

var bridge = builder.AddProject<Projects.Bridge_Worker>("bridge")
    .WithReference(rabbitMq)
    .WithReference(storage)
    .WaitFor(storage)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Sender_Api>("sender")
    .WithReference(rabbitMq)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WaitFor(bridge);

builder.Build().Run();
