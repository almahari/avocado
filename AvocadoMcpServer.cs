using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;

namespace Avocado;

public sealed class AvocadoMcpServer(IMcpTaskStore store, int port = AvocadoMcpServer.DefaultPort)
    : IAsyncDisposable
{
    public const int DefaultPort = 17842;
    public const string Endpoint = "http://127.0.0.1:17842/mcp";
    private WebApplication? _host;

    public bool IsRunning => _host is not null;

    public async Task StartAsync()
    {
        if (_host is not null) return;
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders();
        // Bind explicitly; environment variables cannot expose the task store to the network.
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, port);
            options.Limits.MaxRequestBodySize = 64 * 1024;
        });
        builder.Services.AddSingleton(store);
        builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new() { Name = "Avocado", Version = "1.0.0" };
        }).WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
          .WithTools<McpTaskTools>();

        var host = builder.Build();
        host.Use(async (context, next) =>
        {
            // Reject DNS rebinding and browser requests from other origins.
            var hostname = context.Request.Host.Host;
            var origin = context.Request.Headers.Origin;
            if ((hostname != "127.0.0.1" && hostname != "localhost") ||
                (origin.Count > 0 && (origin.Count != 1 ||
                    (origin[0] != $"http://127.0.0.1:{port}" && origin[0] != $"http://localhost:{port}"))))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next(context);
        });
        host.MapMcp("/mcp");
        try
        {
            await host.StartAsync().ConfigureAwait(false);
            _host = host;
        }
        catch
        {
            await host.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync()
    {
        var host = _host;
        _host = null;
        if (host is null) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await host.StopAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            // Disposal finishes shutdown when an in-flight request exceeded the grace period.
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
