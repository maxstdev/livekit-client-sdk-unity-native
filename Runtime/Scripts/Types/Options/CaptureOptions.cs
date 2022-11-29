using System;

public interface ICaptureOptions { }

public interface IVideoCaptureOptions : ICaptureOptions
{
    public Dimensions Dimensions { get; }
    public int Fps { get; }
}

public enum CameraPosition
{
    Unspecified,
    Back,
    Front
}

public struct CameraCaptureOptions : IVideoCaptureOptions
{
    public CameraPosition Position { get; }
    // public let preferredFormat: AVCaptureDevice.Format?

    /// preferred dimensions for capturing, the SDK may override with a recommended value.
    public Dimensions Dimensions { get; }

    /// preferred fps to use for capturing, the SDK may override with a recommended value.
    public int Fps { get; }

    public CameraCaptureOptions(CameraPosition? position = null,    // CameraPosition.Front
                                Dimensions ? dimensions = null,     // Dimensions.H720_169
                                int fps = 30)
    {
        this.Position = position ?? CameraPosition.Front;
        this.Dimensions = dimensions ?? Dimensions.H720_169;
        this.Fps = fps;
    }

    public CameraCaptureOptions CopyWith(CameraPosition? position = null,
                                         Dimensions ? dimensions = null,
                                         int? fps = null)
    {
        return new CameraCaptureOptions(position: position ?? this.Position,
                                        dimensions: dimensions ?? this.Dimensions,
                                        fps: fps ?? this.Fps);
    }
}

public struct ScreenShareCaptureOptions : IVideoCaptureOptions
{
    public Dimensions Dimensions { get; }
    public int Fps { get; }

    public ScreenShareCaptureOptions(Dimensions? dimensions = null,
                                     int fps = 30)
    {
        this.Dimensions = dimensions ?? Dimensions.H1080_169;
        this.Fps = fps;
    }
}

public struct AudioCaptureOptions : ICaptureOptions
{
    public bool echoCancellation;
    public bool noiseSuppression;
    public bool autoGainControl;
    public bool typingNoiseDetection;
    public bool highpassFilter;
    public bool experimentalNoiseSuppression;
    public bool experimentalAutoGainControl;

    #region Maxst Custom: for Unity
    public int lengthSec;
    public int frequency;
    #endregion

    public AudioCaptureOptions(bool? echoCancellation = null,   //true,
                               bool noiseSuppression = false,
                               bool autoGainControl = true,
                               bool typingNoiseDetection = true,
                               bool highpassFilter = true)
    {
        this.echoCancellation = echoCancellation ?? true;
        this.noiseSuppression = noiseSuppression;
        this.autoGainControl = autoGainControl;
        this.typingNoiseDetection = typingNoiseDetection;
        this.highpassFilter = highpassFilter;

        this.experimentalNoiseSuppression = false;
        this.experimentalAutoGainControl = false;

        #region Maxst Custom: for Unity
        this.lengthSec = 1;
        this.frequency = 44_100;
        #endregion
    }
}