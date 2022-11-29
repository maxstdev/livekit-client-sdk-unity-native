using System;
using UnityEngine;
using Unity.WebRTC;
using Cysharp.Threading.Tasks;

public class ScreenCapturer : VideoCapturer
{
    public readonly Camera ScreenCamera;
    public readonly ScreenShareCaptureOptions Options;

    internal ScreenCapturer(Camera screenCamera, ScreenShareCaptureOptions? options = null)
    {
        ScreenCamera = screenCamera;
        Options = options ?? new ScreenShareCaptureOptions(null);
    }

    public override async UniTask<bool> StartCapture()
    {
        var didStart = await base.StartCapture();

        if (!didStart)
        {
            // already started
            return false;
        }

        ScreenCamera.enabled = true;
        dimensions = Options.Dimensions;

        return true;
    }

    public override async UniTask<bool> StopCapture()
    {
        var didStop = await base.StopCapture();   //Thoas didStart가 의미상 StartCapture()에 대한 결과 임 

        if (!didStop)
        {
            // already stopped
            return false;
        }

        ScreenCamera.enabled = false;
        dimensions = null;

        return true;
    }
}

public partial class LocalVideoTrack
{
    public static LocalVideoTrack CreateScreenCapturerTrack(Camera screenCamera,
                                                            string name = Track.ScreenShareVideoName,
                                                            ScreenShareCaptureOptions? options = null) 
    {
        var capturer = new ScreenCapturer(screenCamera, options ?? new ScreenShareCaptureOptions(null));

        return new LocalVideoTrack(name,
                                   Source.ScreenShareVideo,
                                   capturer);
    }
}