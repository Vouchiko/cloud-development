using Aspire.Hosting;

namespace WarehouseItem.Tests;

/// <summary>
/// Фикстура для интеграционных тестов - поднимает полный стек Aspire один раз для всех тестов класса.
/// </summary>
public class AppFixture : IAsyncLifetime
{
    public DistributedApplication App { get; private set; } = null!;

    private IDistributedApplicationTestingBuilder? _builder;

    public async Task InitializeAsync()
    {
        _builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.WarehouseItem_AppHost>();
        _builder.Configuration["DcpPublisher:RandomizePorts"] = "false";

        App = await _builder.BuildAsync();
        await App.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
        await _builder!.DisposeAsync();
    }
}
