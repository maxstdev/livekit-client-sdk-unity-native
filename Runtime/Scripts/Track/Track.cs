using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;
using PB = LiveKit.Proto;
using Sid = System.String;
using System.Runtime.CompilerServices;
using Dispatch;

public partial class Track : MulticastDelegate<ITrackDelegate>
{
    internal SerialQueue queue = new("LiveKitSDK.track");

    public const string CameraName = "camera";
    public const string MicrophoneName = "microphone";
    public const string ScreenShareVideoName = "screen_share";
    public const string ScreenShareAudioName = "screen_share_audio";

    public enum Kind
    {
        Audio,
        Video,
        Data, // from PB Data
        None
    }

    public enum TrackState
    {
        Stopped,
        Started
    }

    public enum Source
    {
        Unknown,
        Camera,
        Microphone,
        ScreenShareVideo,
        ScreenShareAudio,
    }

    public Track.Kind kind;
    public Track.Source source;
    public string name;

    public Sid sid => _state.Value.sid;
    public bool muted => _state.Value.muted;
    public TrackStats? stats => _state.Value.stats;

    /// Dimensions of the video (only if video track)
    public Dimensions? dimensions => _state.Value.dimensions;

    /// The last video frame received for this track
    // NOTE:Thomas: RTCVideoFrame은 Unity WebRTC에 없다.
    //public RTCVideoFrame videoFrame => _state.Value.videoFrame;
    public TrackState trackState => _state.Value.trackState;

    // - Internal

    internal MediaStreamTrack MediaTrack;
    internal RTCRtpTransceiver Transceiver;
    internal RTCRtpSender Sender => Transceiver?.Sender;

    // Weak reference to all VideoViews attached to this track. Must be accessed from main thread.
    // swift: internal var videoRenderers = NSHashTable<VideoRenderer>.weakObjects()
    internal ConditionalWeakTable<IVideoRenderer, IVideoRenderer> VideoRenderers = new();

    internal struct State
    {
        internal string sid;
        internal Dimensions? dimensions;
        //internal RTCVideoFrame videoFrame;
        internal TrackState trackState;     // = TrackState.Stopped;
        internal bool muted;                // = false;
        internal TrackStats? stats;
    }

    internal StateSync<State> _state = new(new State());

    internal Track(string name, Kind kind, Source source, MediaStreamTrack track)
    {
        this.name = name;
        this.kind = kind;
        this.source = source;
        this.MediaTrack = track;
    }

    ~Track()
    {
        Debug.Log($"sid: {sid}");
    }

    // returns true if updated state
    public virtual UniTask<bool> Start()
    {
        return queue.Sync<UniTask<bool>>(async () =>
        {
            if (trackState == TrackState.Started)
            {
                // already started
                return await UniTask.FromResult(false);
            }

            _state.Mutate(t => { t.trackState = TrackState.Started; return t; });

            return await UniTask.FromResult(true);
        });
    }

    // returns true if updated state
    public virtual UniTask<bool> Stop()
    {
        return queue.Sync<UniTask<bool>>(async () =>
        {
            if (trackState == TrackState.Stopped)
            {
                // already started
                return await UniTask.FromResult(false);
            }

            _state.Mutate(t => { t.trackState = TrackState.Stopped; return t; });

            return await UniTask.FromResult(true);
        });
    }

    internal UniTask<bool> Enable()
    {
        return queue.Sync<UniTask<bool>>(async () =>
        {
            if (MediaTrack.Enabled)
            {
                // already started
                return await UniTask.FromResult(false);
            }

            MediaTrack.Enabled = true;

            return await UniTask.FromResult(true);
        });
    }

    internal UniTask<bool> Disable()
    {
        return queue.Sync<UniTask<bool>>(async () =>
        {
            if (MediaTrack.Enabled == false)
            {
                // already started
                return await UniTask.FromResult(false);
            }

            MediaTrack.Enabled = false;

            return await UniTask.FromResult(true);
        });
    }

    internal void SetMuted(bool newValue,
                           bool notify = true,
                           bool shouldSendSignal = false)
    {
        if (_state.Value.muted == newValue) { return; }

        _state.Mutate(t => { t.muted = newValue; return t; });

        //if (newValue)
        //{
        //    // clear video frame cache if muted
        //    Set(newVideoFrame: null);
        //}

        if (notify)
        {
            Notify((iTrackDelegate) => {
                iTrackDelegate.DidUpdate(this, muted: newValue, shouldSendSignal: shouldSendSignal);
            }, () => {
                return $"track.didUpdate muted: {muted}";
            });
        }
    }
}

// - Internal

public partial class Track
{
    internal void Set(TrackStats newStats)
    {
        if (_state.Value.stats == newStats) { return; }

        _state.Mutate(state => { state.stats = newStats; return state; });

        Notify((iTrackDelegate) => { iTrackDelegate.DidUpdate(this, newStats); });
    }
}

// - Internal

public partial class Track
{
    // returns true when value is updated
    internal bool Set(Dimensions? newDimensions)
    {
        if (_state.Value.dimensions == newDimensions) { return false; }

        _state.Mutate(t => { t.dimensions = newDimensions; return t; });

        if ((this is not VideoTrack videoTrack)) { return true; }

        Notify((iTrackDelegate) => {
            iTrackDelegate.DidUpdate(videoTrack, newDimensions);
        }, () => {
            return $"track.didUpdate dimensions: {newDimensions.ToString()}"; 
        });

        return true;
    }

    //internal void Set(RTCVideoFrame newVideoFrame)
    //{
    //    if (_state.Value.videoFrame == newVideoFrame) { return; }

    //    _state.Mutate(t => { t.videoFrame = newVideoFrame; return t; });
    //}
}

#region Maxst Custom: for Unity

public partial class Track
{
    public MediaStreamTrack GetMediaTrack => MediaTrack;
}

#endregion