using Amazon.S3;
using Amazon.SimpleNotificationService;
using LocalStack.Client.Extensions;
using WarehouseItem.FileService.Service.Messaging;
using WarehouseItem.FileService.Service.Storage;
using WarehouseItem.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();

builder.Services.AddLocalStack(builder.Configuration);

builder.Services.AddSingleton<ISubscriptionService, SnsSubscriptionService>();
builder.Services.AddSingleton<IFileService, AwsFileService>();

builder.Services.AddAwsService<IAmazonSimpleNotificationService>();
builder.Services.AddAwsService<IAmazonS3>();

var app = builder.Build();

var scope = app.Services.CreateScope();
var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
await subscriptionService.SubscribeEndpoint();

scope = app.Services.CreateScope();
var s3Service = scope.ServiceProvider.GetRequiredService<IFileService>();
await s3Service.EnsureBucketExists();

app.MapControllers();
app.Run();
