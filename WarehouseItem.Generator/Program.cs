using Amazon.SimpleNotificationService;
using LocalStack.Client.Extensions;
using WarehouseItem.Generator.Generator;
using WarehouseItem.Generator.Service;
using WarehouseItem.Generator.Service.Messaging;
using WarehouseItem.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("warehouse-item-cache", configureOptions: options =>
{
    options.AbortOnConnectFail = false;
    options.SslClientAuthenticationOptions = host => new System.Net.Security.SslClientAuthenticationOptions
    {
        TargetHost = host,
        RemoteCertificateValidationCallback = (_, _, _, _) => true // dev: доверяем self-signed сертификату Aspire
    };
});

builder.Services.AddLocalStack(builder.Configuration);
builder.Services.AddAwsService<IAmazonSimpleNotificationService>();

builder.Services.AddSingleton<WarehouseItemGenerator>();
builder.Services.AddSingleton<IWarehouseItemCache, WarehouseItemCache>();
builder.Services.AddSingleton<IPublisherService, SnsPublisherService>();
builder.Services.AddSingleton<IWarehouseItemService, WarehouseItemService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
