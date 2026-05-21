using Amazon;
using Aspire.Hosting.LocalStack.Container;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("warehouse-item-cache")
    .WithRedisInsight(containerName: "warehouse-item-insight");

var gateway = builder.AddProject<Projects.WarehouseItem_Gateway>("gateway")
    .WithHttpEndpoint(name: "http", port: 5300)
    .WithExternalHttpEndpoints();

var awsConfig = builder.AddAWSSDKConfig()
    .WithProfile("default")
    .WithRegion(RegionEndpoint.EUCentral1);

var localstack = builder
    .AddLocalStack("warehouse-item-localstack", awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
        container.Port = 4566;
        container.AdditionalEnvironmentVariables
            .Add("DEBUG", "1");
        container.AdditionalEnvironmentVariables
            .Add("SNS_CERT_URL_HOST", "sns.eu-central-1.amazonaws.com");
    });

var awsResources = builder
    .AddAWSCloudFormationTemplate("files", "CloudFormation/warehouse-item-template-sns-s3.yaml", "warehouse-item")
    .WithReference(awsConfig);

const int generatorPortBase = 5200;
for (var i = 1; i <= 5; ++i)
{
    var generator = builder.AddProject<Projects.WarehouseItem_Generator>($"generator-{i}")
        .WithReference(cache, "warehouse-item-cache")
        .WithHttpEndpoint(name: "http", port: generatorPortBase + i)
        .WithReference(awsResources)
        .WithEnvironment("Settings__MessageBroker", "SNS")
        .WaitFor(awsResources);

    gateway.WaitFor(generator);
}

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(gateway)
    .WaitFor(gateway);

var fileService = builder.AddProject<Projects.WarehouseItem_FileService>("warehouse-item-file-service")
    .WithHttpEndpoint(name: "http", port: 5280)
    .WithReference(awsResources)
    .WithEnvironment("Settings__MessageBroker", "SNS")
    .WithEnvironment("Settings__S3Hosting", "Localstack")
    .WaitFor(awsResources);
fileService.WithEnvironment("AWS__Resources__SNSUrl", "http://host.docker.internal:5280/api/sns");
fileService.WithEnvironment("AWS__Resources__SNSTopicArn", "arn:aws:sns:eu-central-1:000000000000:warehouse-item-topic");

builder.UseLocalStack(localstack);

builder.Build().Run();
