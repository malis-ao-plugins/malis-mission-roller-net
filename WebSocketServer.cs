using AOSharp.Core.UI;
using System.Collections.Concurrent;
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
    // Errors raised on the socket threads, held for the game thread to print.
    private readonly ConcurrentQueue<string> _notices = new();

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
                        lock (_clients)
                            _clients.Add(wsCtx.WebSocket);
                        OnClientConnected?.Invoke();
                        _ = HandleClient(wsCtx.WebSocket);
                    }
                }
            }
            catch (HttpListenerException) { }
        });
    }

    /// <summary>Hands over one queued notice. Call from the game thread, which is the one allowed to write to chat.</summary>
    public bool TryTakeNotice(out string notice) => _notices.TryDequeue(out notice);

    public void Broadcast(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        List<WebSocket> clients;
        lock (_clients)
            clients = _clients.ToList();

        foreach (var client in clients)
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

        List<WebSocket> clients;
        lock (_clients)
        {
            clients = _clients.ToList();
            _clients.Clear();
        }

        foreach (var c in clients)
        {
            try
            {
                if (c.State == WebSocketState.Open)
                    c.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", CancellationToken.None).Wait(500);
            }
            catch { }
            finally { c.Dispose(); }
        }

        try { _listenerTask?.Wait(1000); } catch { }
    }

    private async Task HandleClient(WebSocket ws)
    {
        var buffer = new byte[4096];
        var message = new MemoryStream();

        while (ws.State == WebSocketState.Open)
        {
            try
            {
                // A message larger than the buffer arrives in several reads; parse it only once it is whole.
                message.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, _cts.Token);
                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);

                if (result.MessageType == WebSocketMessageType.Close) break;
                if (message.Length == 0) continue;

                using var doc = JsonDocument.Parse(message.ToArray());
                var root = doc.RootElement;
                var type = root.GetProperty("type").GetString();

                if (_handlers.TryGetValue(type, out var handler))
                    handler(root);
                else
                    _notices.Enqueue($"Unhandled message type: {type}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _notices.Enqueue($"HandleClient error: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        lock (_clients)
            _clients.Remove(ws);
    }
}