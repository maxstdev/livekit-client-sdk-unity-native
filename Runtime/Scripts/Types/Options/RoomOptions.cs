using System;

public struct RoomOptions
{
    // default options for capturing
    public CameraCaptureOptions defaultCameraCaptureOptions;
    public ScreenShareCaptureOptions defaultScreenShareCaptureOptions;
    public AudioCaptureOptions defaultAudioCaptureOptions;

    // default options for publishing
    public VideoPublishOptions defaultVideoPublishOptions;
    public AudioPublishOptions defaultAudioPublishOptions;

    /// AdaptiveStream lets LiveKit automatically manage quality of subscribed
    /// video tracks to optimize for bandwidth and CPU.
    /// When attached video elements are visible, it'll choose an appropriate
    /// resolution based on the size of largest video element it's attached to.
    ///
    /// When none of the video elements are visible, it'll temporarily pause
    /// the data flow until they are visible again.
    ///
    public bool adaptiveStream;

    /// Dynamically pauses video layers that are not being consumed by any subscribers,
    /// significantly reducing publishing CPU and bandwidth usage.
    ///
    public bool dynacast;

    public bool stopLocalTrackOnUnpublish;

    /// Automatically suspend(mute) video tracks when the app enters background and
    /// resume(unmute) when the app enters foreground again.
    public bool suspendLocalVideoTracksInBackground;

    /// **Experimental**
    /// Report ``TrackStats`` every second to ``TrackDelegate`` for each local and remote tracks.
    /// This may consume slightly more CPU resources.
    public bool reportStats;

    public RoomOptions(CameraCaptureOptions? defaultCameraCaptureOptions = null,
                       ScreenShareCaptureOptions? defaultScreenShareCaptureOptions = null,
                       AudioCaptureOptions? defaultAudioCaptureOptions = null,
                       VideoPublishOptions? defaultVideoPublishOptions = null,
                       AudioPublishOptions? defaultAudioPublishOptions = null,
                       bool adaptiveStream = false,
                       bool dynacast = false,
                       bool stopLocalTrackOnUnpublish = true,
                       bool suspendLocalVideoTracksInBackground = true,
                       bool reportStats = false)
    {
        this.defaultCameraCaptureOptions = defaultCameraCaptureOptions ?? new CameraCaptureOptions(null);
        this.defaultScreenShareCaptureOptions = defaultScreenShareCaptureOptions ?? new ScreenShareCaptureOptions(null);
        this.defaultAudioCaptureOptions = defaultAudioCaptureOptions ?? new AudioCaptureOptions(null);
        this.defaultVideoPublishOptions = defaultVideoPublishOptions ?? new VideoPublishOptions(null);
        this.defaultAudioPublishOptions = defaultAudioPublishOptions ?? new AudioPublishOptions(null);

        this.adaptiveStream = adaptiveStream;
        this.dynacast = dynacast;
        this.stopLocalTrackOnUnpublish = stopLocalTrackOnUnpublish;
        this.suspendLocalVideoTracksInBackground = suspendLocalVideoTracksInBackground;
        this.reportStats = reportStats;
    }
}