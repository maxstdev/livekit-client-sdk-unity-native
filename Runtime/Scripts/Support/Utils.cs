using Cysharp.Threading.Tasks;
using Dispatch;
using System;
using System.Linq;
using System.Threading;
using UniLiveKit.ErrorException;
using Unity.WebRTC;
using UnityEngine;

public class Utils
{
    internal enum OS
    {
        macOS, iOS, android, unityEditor, window, unknown
    }

    internal static OS CurrentOS()
    {
#if UNITY_IOS
        return OS.iOS;
#elif UNITY_EDITOR
        return OS.unityEditor;
#elif UNITY_ANDROID
        return OS.android;
#elif UNITY_STANDALONE_WIN
        return OS.window;
#elif UNITY_STANDALONE_OSX
        return OS.macOS;
#else
        return OS.unknown;
#endif
    }

    // TODO : - 동작에 대한 디바이스 별 테스트가 필요함 ex) "iOSSimulator,arm64"
    // TODO : get_internetReachability can only be called from the main thread. - MonoBehaviour 와의 연결 재정리
    internal static string ModelIdentifier()
    {
        return SystemInfo.deviceModel;
    }

    internal static string OSVersionString()
    {
        return SystemInfo.operatingSystem;
    }

    // TODO : get_internetReachability can only be called from the main thread. - MonoBehaviour 와의 연결 재정리
    internal static string NetworkTypeToString()
    {
        return ConnectivityListener.shared.ActiveInterfaceType switch
        {
            NetworkReachability.NotReachable => null,
            NetworkReachability.ReachableViaCarrierDataNetwork => "callular",
            NetworkReachability.ReachableViaLocalAreaNetwork => "wifi_or_wired",
            _ => null
        };
    }

    internal static Uri BuildUrl(
        string url,
        string token,
        bool adaptiveStream,
        ConnectOptions? connectOptions = null,
        ReconnectMode? reconnectMode = null,
        bool validate = false,
        bool forceSecure = false)
    {
        ConnectOptions _connectOptions = connectOptions ?? new();

        if (!URL(url, out Uri uriResult))
        {
            throw new EnumException<InternalError>(InternalError.Parse, "Failed to parse url");
        }

        // for check rfc3986
        if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
        {
            throw new EnumException<InternalError>(InternalError.Parse, "Failed to parse UriBuilder");
        }

        UriBuilder builder = new(uriResult);

        var useSecure = builder.Uri.isSecure() || forceSecure;
        string httpScheme = useSecure ? "https" : "http";
        string wsScheme = useSecure ? "wss" : "ws";
        string lastPathSegment = validate ? "validate" : "rtc";

        var pathSegments = builder.Uri.AbsolutePath.Split("/").Where((e) => !string.IsNullOrEmpty(e));

        string[] filePathSegments = new string[2] { "rtc", "validate" };

        if (builder.Uri.IsFile
            && pathSegments.Count() > 0
            && filePathSegments.Contains(pathSegments.Last()))
        {
            pathSegments = pathSegments.SkipLast(1);
        }

        pathSegments = pathSegments.Append(lastPathSegment);

        builder.Scheme = validate ? httpScheme : wsScheme;
        builder.Path = "/" + string.Join("/", pathSegments);

        builder.Query += $"access_token={token}";
        builder.Query += $"&protocol={_connectOptions.protocolVersion.ToIntString()}";
        builder.Query += $"&sdk=UniLiveKit";
        builder.Query += $"&version={UniLiveKit.LiveKit.version}";
        builder.Query += $"&os={CurrentOS()}";
        builder.Query += $"&os_version={OSVersionString()}";

        var modelIdentifier = ModelIdentifier();
        if (!string.IsNullOrEmpty(modelIdentifier))
        {
            builder.Query += $"&device_model={modelIdentifier}";
        }

        var networkType = NetworkTypeToString();
        if (!string.IsNullOrEmpty(networkType))
        {
            builder.Query += $"&network={networkType}";
        }
        builder.Query += $"&reconnect={(reconnectMode == ReconnectMode.Quick ? "1" : "0")}";
        builder.Query += $"&auto_subscribe={(_connectOptions.autoSubscribe ? "1" : "0")}";
        builder.Query += $"&adaptive_stream={(adaptiveStream ? "1" : "0")}";

        var publish = _connectOptions.publishOnlyMode;
        if (!string.IsNullOrEmpty(publish))
        {
            builder.Query += $"&publish={publish}";
        }

        builder.Query = Uri.EscapeUriString(builder.Query);

        if (!builder.Uri.IsWellFormedOriginalString())
        {
            throw new EnumException<InternalError>(InternalError.Convert, $"Failed to convert components to url {builder}");
        }
        return builder.Uri;
    }

    internal static Action CreateDebounceFunc(SerialQueue queue, int wait, Action<CancellationTokenSource> onCreateWorkItem, Action fnc)
    {
        CancellationTokenSource source = null;
        return () =>
        {
            queue.Async(async () =>
            {

                source?.Cancel();
                source?.Dispose();

                source = new();
                onCreateWorkItem?.Invoke(source);

                await UniTask.Delay(wait, false, PlayerLoopTiming.Update, source.Token);

                fnc();
            });
        };
    }

    internal static RTCRtpEncodingParameters[] ComputeEncodings(
        Dimensions dimensions,
        VideoPublishOptions? publishOptions,
        bool isScreenShare = false)
    {
        var _publishOptions = publishOptions ?? new VideoPublishOptions();
        var preferredEncoding = isScreenShare ? _publishOptions.screenShareEncoding : _publishOptions.encoding;
        var encoding = preferredEncoding ?? dimensions.ComputeSuggestedPreset(dimensions.ComputeSuggestedPresets(isScreenShare));

        // TODO: Unity WebRTC needs to add Simulcast support
        //if (_publishOptions.simulcast) { }
        //else
        //{
        return new[] { Engine.CreateRtpEncodingParameters(null, encoding) };
        //}


        // NOTE: About Simulcast Support Settings
        var baseparameters = new VideoParameters(dimensions, encoding);

        var preferredPresets = (isScreenShare ? publishOptions?.screenShareSimulcastLayers : publishOptions?.simulcastLayers);
        var presets = (!(preferredPresets.Count() <= 0) ? preferredPresets : baseparameters.DefaultSimulcastLayers(isScreenShare));
        Array.Sort(presets, (e1, e2) => e1 < e2 ? -1 : 1);

        Debug.Log($"Using presets: {presets}, count: {presets.Count()}, isScreenShare: {isScreenShare}");

        var lowPreset = presets[0];
        VideoParameters? midPreset = presets.Count() > 0 ? presets[1] : null;

        var resultPresets = new[] { baseparameters };

        if (dimensions.Max >= 960 && midPreset is VideoParameters _midPreset)
        {
            resultPresets = new[] { lowPreset, _midPreset, baseparameters };
        }
        else if (dimensions.Max >= 480)
        {
            resultPresets = new[] { lowPreset, baseparameters };
        }

        return dimensions.Encodings(resultPresets);
    }

    private static bool URL(string url, out Uri uriResult)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out uriResult);
    }
}