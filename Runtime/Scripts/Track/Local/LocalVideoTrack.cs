using Cysharp.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

public partial class LocalVideoTrack : LocalTrack
{
    public VideoCapturer Capturer { get; internal set; }

    internal LocalVideoTrack(string name,
                             Track.Source source,
                             VideoCapturer videoCapturer)
        : base(name: name,
               kind: Kind.Video,
               source: source,
               track: null)
    {
        this.Capturer = videoCapturer;

        var rtcTrack = Engine.CreateVideoTrack(videoCapturer);
        rtcTrack.Enabled = true;
        this.MediaTrack = rtcTrack; // set base.track
    }

    public override async UniTask<bool> Start()
    {
        var didStart = await base.Start();

        return await queue.Sync(async () =>
        {
            await Capturer.StartCapture();
            return didStart;
        });
    }

    public override async UniTask<bool> Stop()
    {
        var didStop = await base.Stop();
        return await queue.Sync(async () =>
        {
            await Capturer.StopCapture();
            return didStop;
        });
    }
}

public static class RTCRtpEncodingParametersExtension
{
    public static string ToString(this RTCRtpEncodingParameters parameters)
    {
        var maxBitrateBps = parameters.maxBitrate == null ? "null" : parameters.maxBitrate.ToString();
        var maxFramerate = parameters.maxFramerate == null ? "nil" : parameters.maxFramerate.ToString();

        return $"RTCRtpEncodingParameters(rid: {parameters.rid ?? "null"}, "
            + $"active: {parameters.active}, "
            + $"scaleResolutionDownBy: {parameters.scaleResolutionDownBy}, "
            + $"maxBitrateBps: {maxBitrateBps}, "
            + $"maxFramerate: {maxFramerate}";
    }
}