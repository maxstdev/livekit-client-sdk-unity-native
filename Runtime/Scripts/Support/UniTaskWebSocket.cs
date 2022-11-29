using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniLiveKit.ErrorException;

using OnMessage = System.EventHandler<WebSocketSharp.MessageEventArgs>;
using OnDisconnect = System.Action<DisconnectReason?>;
using OnError = System.EventHandler<WebSocketSharp.ErrorEventArgs>;

using WebSocketSharp;
using WebSocket = WebSocketSharp.WebSocket;
using System.Threading.Tasks;
using System.Security.Authentication;
using Dispatch;
using System.Net.WebSockets;

internal class UniTaskWebSocket
{
    private SerialQueue queue = new("LiveKitSDK.webSocket");
    
    private OnMessage onMessage;
    private OnDisconnect onDisconnect;

    private WebSocket webSocket;

    private static bool isOnOpen = false;
    private bool isDisconnected = false;
    private bool isOnDisconnected = false;

    internal static async UniTask<UniTaskWebSocket> Connect(
        Uri uri,
        OnMessage onMessage = null,
        OnDisconnect onDisconnect = null
        )
    {
        var uniTask = new UniTaskWebSocket(uri, onMessage, onDisconnect);
        uniTask.webSocket.ConnectAsync();
        await UniTask.WaitUntil(() => isOnOpen);
        return uniTask;
    }

    private UniTaskWebSocket(
        Uri uri,
        OnMessage onMessage = null,
        OnDisconnect onDisconnect = null
        )
    {
        this.onMessage = onMessage;
        this.onDisconnect = onDisconnect;

        webSocket = WSCreate(uri);
    }

    ~UniTaskWebSocket()
    {
        Debug.Log("UniTaskWebSocket Destructor");
    }

    internal async void CleanUp(DisconnectReason? reason)
    {
        if (isDisconnected)
        {
            throw new Exception("WebSocket disconnected");
        }
 
        isDisconnected = true;

        await WSDestory();
        
        onDisconnect?.Invoke(reason);
    }

    public async UniTask Send(byte[] bytes)
    {
        await queue.Sync(async () =>
        {
            UniTaskCompletionSource source = new();

            try
            {
                webSocket.SendAsync(bytes, (isSuccess) => {
                    source.TrySetResult();
                });
            }
            catch(Exception error)
            {
                throw error;
            }

            await source.Task;
        });
    }

    private WebSocket WSCreate(Uri uri)
    {
        var url = uri.AbsoluteUri;
        var _webSocket = new WebSocket(url);

        if (url.StartsWith("wss"))
        {
            _webSocket.SslConfiguration.EnabledSslProtocols =
                SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
        }

        _webSocket.Log.Level = LogLevel.Trace;
        _webSocket.WaitTime = TimeSpan.FromSeconds(60);
        _webSocket.OnOpen += WSOpen;
        _webSocket.OnMessage += OnWSMessage;
        _webSocket.OnClose += OnWSDisConnect;
        _webSocket.OnError += OnWSError;
        return _webSocket;
    }

    private async Task WSDestory()
    {
        if (webSocket == null) return;

        webSocket.CloseAsync();

        await UniTask.WaitUntil(() => isOnDisconnected);

        webSocket.OnOpen -= WSOpen;
        webSocket.OnMessage -= OnWSMessage;
        webSocket.OnClose -= OnWSDisConnect;
        webSocket.OnError -= OnWSError;
        webSocket = null;
    }

    private void WSOpen(object sender, EventArgs e)
    {
        if (isDisconnected) return;

        isOnOpen = true;
    }

    private void OnWSMessage(object sender, MessageEventArgs e)
    {
        queue.Async(() =>
        {
            if (isDisconnected) return;
            onMessage?.Invoke(sender, e);
        });
    }

    private void OnWSDisConnect(object sender, CloseEventArgs e)
    {
        isOnDisconnected = true;

        if (isDisconnected) return;

        var sdkError = new EnumException<NetworkError>(NetworkError.Disconnected, $"WebSocket did close with code: {e.Code}, reason : {e.Reason}");

        CleanUp(DisconnectReason.networkError.Reason(sdkError));
    }

    private void OnWSError(object sender, ErrorEventArgs e)
    {
        if (isDisconnected) return;
        var sdkError = new EnumException<NetworkError>(NetworkError.Disconnected, $"WebSocket disconnected", e.Exception);

        CleanUp(DisconnectReason.networkError.Reason(sdkError));
    }

}
