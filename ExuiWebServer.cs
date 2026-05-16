using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ExuiApi;

public static class ExuiWebServer
{
    public static void Initialize()
    {
        string serverUrl = "http://localhost:8080/";
        _ = Task.Run(() => StartWebServer(serverUrl));
        Console.WriteLine($"[exui] HTTP debug endpoint active: {serverUrl}exui");
        Console.WriteLine($"[exui] WebSocket live stream active: ws://localhost:8080/exui");
    }

    private static async Task StartWebServer(string prefixUrl)
    {
        using HttpListener server = new HttpListener();
        server.Prefixes.Add(prefixUrl);
        server.Start();

        while (true)
        {
            try
            {
                HttpListenerContext context = await server.GetContextAsync();
                await HandleClientRequest(context);
            }
            catch
            {
            }
        }
    }

    private static async Task HandleClientRequest(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        if (request.Url?.AbsolutePath.ToLower() != "/exui")
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.Close();
            return;
        }

        if (request.IsWebSocketRequest)
        {
            try
            {
                HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                _ = Task.Run(() => HandleWebSocketStream(wsContext.WebSocket));
            }
            catch
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Close();
            }
        }
        else
        {
            await SendJsonResponse(response);
        }
    }

    private static async Task SendJsonResponse(HttpListenerResponse response)
    {
        byte[] rawBytes = SerializeTelemetry();

        response.ContentType = "application/json";
        response.ContentLength64 = rawBytes.Length;
        response.Headers.Add("Access-Control-Allow-Origin", "*");

        using var outputStream = response.OutputStream;
        await outputStream.WriteAsync(rawBytes, 0, rawBytes.Length);
        response.Close();
    }

    private static async Task HandleWebSocketStream(WebSocket webSocket)
    {
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                byte[] rawBytes = SerializeTelemetry();
                var segment = new ArraySegment<byte>(rawBytes);
                
                await webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                await Task.Delay(16);
            }
        }
        catch
        {
        }
        finally
        {
            if (webSocket.State != WebSocketState.Aborted)
            {
                webSocket.Dispose();
            }
        }
    }

    private static byte[] SerializeTelemetry()
    {
        var payload = new Dictionary<string, object>
        {
            { "connected", GameState.IsGameRunning }
        };

        foreach (var kvp in GameState.Telemetry)
        {
            payload[kvp.Key] = kvp.Value;
        }

        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }
}