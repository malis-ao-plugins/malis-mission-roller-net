using AOSharp.Core.UI;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

public class WebSocketServer
{
    private readonly HttpListener _listener;
    private readonly List<WebSocket> _clients = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<string, Action<JsonElement>> _handlers = new();
    private Task _listenerTask;
    private readonly int _port;

    public event Action OnClientConnected;

    public WebSocketServer(int port)
    {
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Register(string messageType, Action<JsonElement> handler)
    {
        _handlers[messageType] = handler;
    }

    public void Start()
    {
        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            Chat.WriteLine($"WS start failed: {ex.Message}");
            return;
        }

        _listenerTask = Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested && _listener.IsListening)
                {
                    var ctx = await _listener.GetContextAsync();
                    if (ctx.Request.IsWebSocketRequest)
                    {
                        var wsCtx = await ctx.AcceptWebSocketAsync(null);
                        _clients.Add(wsCtx.WebSocket);
                        OnClientConnected?.Invoke();
                        _ = HandleClient(wsCtx.WebSocket);
                    }
                }
            }
            catch (HttpListenerException) { }
        });
    }

    public void Broadcast(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        foreach (var client in _clients.ToList())
        {
            if (client.State == WebSocketState.Open)
                client.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
        }
    }

    public void Stop()
    {
        _handlers.Clear();
        OnClientConnected = null;

        _cts.Cancel();
        try { _listener.Stop(); } catch { }

        foreach (var c in _clients)
        {
            try
            {
                if (c.State == WebSocketState.Open)
                    c.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", CancellationToken.None).Wait(500);
            }
            catch { }
            finally { c.Dispose(); }
        }

        _clients.Clear();
        try { _listenerTask?.Wait(1000); } catch { }
    }

    private async Task HandleClient(WebSocket ws)
    {
        var buffer = new byte[4096];

        while (ws.State == WebSocketState.Open)
        {
            try
            {
                var result = await ws.ReceiveAsync(buffer, _cts.Token);
                if (result.Count == 0 || result.MessageType == WebSocketMessageType.Close) break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.GetProperty("type").GetString();

                if (_handlers.TryGetValue(type, out var handler))
                    handler(root);
                else
                    Chat.WriteLine($"Unhandled message type: {type}");
            }
            catch (Exception ex)
            {
                Chat.WriteLine($"HandleClient error: {ex.Message}");
            }
        }

        _clients.Remove(ws);
    }
}