using System;
using UnityEngine;
using Unity.WebRTC;
using Cysharp.Threading.Tasks;
using System.Linq;

public class WebCamCapturer : VideoCapturer
{
    public WebCamDevice? MWebCamDevice;
    public WebCamTexture MWebCamTexture;
    public readonly CameraCaptureOptions Options;

    internal WebCamCapturer(CameraCaptureOptions? options = null)
    {
        Debug.Log("WebCamCapturer()");

        Options = options ?? new CameraCaptureOptions(null);

        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.LogWarning("No devices cameras found");
            return;
        }

        var isPositionFront = (Options.Position == CameraPosition.Front) ? true : false;

        foreach (var device in devices)
        {
            if (device.isFrontFacing == isPositionFront)
            {
                MWebCamDevice = device;
                break;
            }
        }

        MWebCamDevice = MWebCamDevice ?? devices.First();

        MWebCamTexture = new WebCamTexture(MWebCamDevice?.name, Options.Dimensions.Width, Options.Dimensions.Height, Options.Fps);
        MWebCamTexture.Play();
    }

    ~WebCamCapturer()
    {
        Debug.Log($"~WebCamCapturer()");

        MWebCamDevice = null;

        if (MWebCamTexture is not null)
        {
            MWebCamTexture.Stop();
            WebCamTexture.Destroy(MWebCamTexture);
            MWebCamTexture = null;
        }
    }

    public override async UniTask<bool> StartCapture()
    {
        var didStart = await base.StartCapture();

        if (!didStart)
        {
            // already started
            return false;
        }

        if (MWebCamTexture.isPlaying is false)
        {
            MWebCamTexture.Play();
        }

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

        MWebCamTexture.Stop();
        dimensions = null;

        return true;
    }
}

public partial class LocalVideoTrack
{
    public static LocalVideoTrack CreateWebCamCapturerTrack(WebCamDevice webCamDevice,
                                                            string name = Track.CameraName,
                                                            CameraCaptureOptions? options = null)
    {
        var capturer = new WebCamCapturer(options ?? new CameraCaptureOptions(null));

        return new LocalVideoTrack(name,
                                   Source.Camera,
                                   capturer);
    }
}