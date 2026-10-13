using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;
using Avocado;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var directory = Path.Combine(Path.GetTempPath(), $"avocado-mcp-tests-{Guid.NewGuid():N}");
var store = new AppStateStore(Path.Combine(directory, "state.json"));
var ready = new TaskCompletionSource<MainWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
var uiThread = new Thread(() =>
{
    try
    {
        var window = new MainWindow(store);
        window.Left = 0;
        window.Top = 0;
        ready.SetResult(window);
        Dispatcher.Run();
    }
    catch (Exception exception) { ready.TrySetException(exception); }
});
uiThread.SetApartmentState(ApartmentState.STA);
uiThread.IsBackground = true;
uiThread.Start();
var window = await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;
listener.Stop();
var endpoint = new Uri($"http://127.0.0.1:{port}/mcp");
await using var server = new AvocadoMcpServer(window, port);
try
{
    Assert(!server.IsRunning && !store.Load().McpServerEnabled, "Server defaults to disabled.");
    await server.StartAsync();
    await server.StartAsync();
    await window.Dispatcher.InvokeAsync(() => window.SetMcpServerEnabled(true));
    Assert(store.Load().McpServerEnabled, "The enabled preference is saved.");
    await using var client = await Connect();
    var tools = await client.ListToolsAsync();
    Assert(tools.Select(tool => tool.Name).Order().SequenceEqual(
        new[] { "create_task", "list_tasks", "update_task" }), "All tools are discoverable over HTTP.");
    Assert((await Call(client, "list_tasks")).GetArrayLength() == 0, "An empty list is returned.");
    var created = await Call(client, "create_task", new() { ["input"] = "daily 09:00 !! Review report #work" });
    var id = created.GetProperty("id").GetString()!;
    Assert(created.GetProperty("text").GetString() == "Review report #work", "Creation uses the existing parser.");
    Assert(created.GetProperty("priority").GetString() == "Medium" &&
        created.GetProperty("recurrence").GetString() == "Daily", "Priority and reminder are parsed.");
    Assert(store.Load().Tasks.Single().Id.ToString() == id, "Creation persists the returned ID.");

    var updated = await Call(client, "update_task", new()
    {
        ["id"] = id, ["text"] = "Updated #home", ["priority"] = "high", ["isPinned"] = true
    });
    Assert(updated.GetProperty("text").GetString() == "Updated #home" &&
        updated.GetProperty("isPinned").GetBoolean(), "Updates change text and pinning.");
    Assert(updated.GetProperty("reminderTime").GetString() == "09:00", "Omitted reminder fields are preserved.");
    await Call(client, "update_task", new() { ["id"] = id, ["schedule"] = "tomorrow 10am" });
    Assert(store.Load().Tasks.Single().DueAt is not null, "Rescheduling persists the new due date.");

    await ExpectError(client, "update_task", new() { ["id"] = id, ["text"] = "Must not apply", ["priority"] = "invalid" });
    Assert(store.Load().Tasks.Single().Text == "Updated #home", "Invalid updates cannot partially change tasks.");
    await ExpectError(client, "update_task", new() { ["id"] = id, ["schedule"] = "nonsense" });
    await ExpectError(client, "update_task", new() { ["id"] = Guid.NewGuid().ToString(), ["text"] = "Unknown" });
    await ExpectError(client, "create_task", new() { ["input"] = "  " });
    await ExpectError(client, "update_task", new() { ["id"] = id, ["text"] = " " });
    await ExpectError(client, "update_task", new() { ["id"] = id, ["priority"] = "3" });

    var completed = await Call(client, "update_task", new() { ["id"] = id, ["isCompleted"] = true });
    Assert(completed.GetProperty("isCompleted").GetBoolean() && store.Load().ArchivedTasks.Count == 1,
        "Completion uses the archive and persists it.");
    Assert((await Call(client, "list_tasks")).GetArrayLength() == 0, "Completed tasks are excluded by default.");
    Assert((await Call(client, "list_tasks", new() { ["includeCompleted"] = true })).GetArrayLength() == 1,
        "Completed tasks can be listed.");
    await Call(client, "update_task", new() { ["id"] = id, ["isCompleted"] = false, ["schedule"] = "" });
    Assert(store.Load().Tasks.Count == 1 && store.Load().ArchivedTasks.Count == 0 &&
        store.Load().Tasks.Single().DueAt is null, "Restore and reminder clearing persist.");
    var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
        Call(client, "create_task", new() { ["input"] = $"Concurrent {index}" })));
    Assert(concurrent.Select(task => task.GetProperty("id").GetString()).Distinct().Count() == 8 &&
        store.Load().Tasks.Count == 9, "Concurrent calls are serialized on the UI thread without losing tasks.");

    using var http = new HttpClient();
    using var badHost = new HttpRequestMessage(HttpMethod.Post, endpoint);
    badHost.Headers.Host = "attacker.example";
    Assert((await http.SendAsync(badHost)).StatusCode == HttpStatusCode.Forbidden, "Untrusted hosts are rejected.");
    using var badOrigin = new HttpRequestMessage(HttpMethod.Post, endpoint);
    badOrigin.Headers.Add("Origin", "https://attacker.example");
    Assert((await http.SendAsync(badOrigin)).StatusCode == HttpStatusCode.Forbidden, "Untrusted origins are rejected.");

    // Check clients that still use the initialize handshake as well as the latest SDK client.
    await using (var legacy = await Connect("2025-11-25"))
        Assert((await legacy.ListToolsAsync()).Count == 3, "Legacy protocol clients can discover tools.");

    await server.StopAsync();
    Assert(!server.IsRunning, "Stopping clears the running state.");
    try
    {
        await http.GetAsync(endpoint);
        throw new Exception("Stopped server still accepts connections.");
    }
    catch (HttpRequestException) { }
    await server.StartAsync();
    await using (var restarted = await Connect())
        Assert((await Call(restarted, "list_tasks")).GetArrayLength() == 9, "Restart retains tasks.");

    await using var conflicting = new AvocadoMcpServer(window, port);
    try
    {
        await conflicting.StartAsync();
        throw new Exception("Port conflict unexpectedly succeeded.");
    }
    catch (System.IO.IOException) { Assert(!conflicting.IsRunning, "Failed startup stays disabled."); }
    await server.StopAsync();
    await window.Dispatcher.InvokeAsync(() => window.SetMcpServerEnabled(false));
    Assert(!store.Load().McpServerEnabled, "The disabled preference is saved.");
    Console.WriteLine("MCP integration checks passed.");
}
finally
{
    await server.StopAsync();
    await window.Dispatcher.InvokeAsync(() =>
    {
        window.AllowClose();
        window.Close();
        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
    });
    uiThread.Join(TimeSpan.FromSeconds(5));
    if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
}

Task<McpClient> Connect(string? protocolVersion = null) => McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = endpoint, TransportMode = HttpTransportMode.StreamableHttp,
        ConnectionTimeout = TimeSpan.FromSeconds(10)
    }), new McpClientOptions { ProtocolVersion = protocolVersion });

static async Task<JsonElement> Call(McpClient client, string tool, Dictionary<string, object?>? arguments = null)
{
    var result = await client.CallToolAsync(tool, arguments);
    Assert(result.IsError != true, $"{tool} failed: {string.Join(" ", result.Content.OfType<TextContentBlock>().Select(block => block.Text))}");
    using var document = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return document.RootElement.Clone();
}

static async Task ExpectError(McpClient client, string tool, Dictionary<string, object?> arguments)
{
    var result = await client.CallToolAsync(tool, arguments);
    Assert(result.IsError == true, $"{tool} must return a tool error for invalid input.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
