using Cysharp.Threading.Tasks;
using LiveKit.Proto;
using System;
using System.Linq;
using System.Threading;
using UniLiveKit.ErrorException;
using UnityEngine;

public partial class LocalTrackPublication : TrackPublication
{
    internal LocalTrackPublication(TrackInfo info,
                                   Participant participant,
                                   Track track = null)
        : base(info, participant, track)
    {
        shouldRecomputeSenderParameters = Utils.CreateDebounceFunc(
            queue,
            100,
            (cancelSource) =>
            {
                debounceWorkItem = new(cancelSource);
            }, 
            () =>
            {
                RecomputeSenderParameters();
            });
    }

    // indicates whether the track was suspended(muted) by the SDK
    internal bool Suspended = false;

    private WeakReference<CancellationTokenSource> debounceWorkItem;

    // stream state is always active for local tracks
    public new StreamState streamState => StreamState.Active;

    public async UniTask Mute()
    {
        var localTrack = Track as LocalTrack;
        if (localTrack == null)
        {
            throw new EnumException<InternalError>(InternalError.State, "track is null or not a LocalTrack");
        }

        await localTrack.Mute();

        return;
    }

    public async UniTask Unmute()
    {
        var localTrack = Track as LocalTrack;
        if (localTrack == null)
        {
            throw new EnumException<InternalError>(InternalError.State, "track is null or not a LocalTrack");
        }

        await localTrack.Unmute();

        return;
    }

    internal new Track SetTrack(Track newValue)
    {
        var oldValue = base.SetTrack(newValue: newValue);

        // listen for VideoCapturerDelegate
        if (oldValue is LocalVideoTrack oldLocalVideoTrack)
        {
            oldLocalVideoTrack.Capturer.RemoveDelegate(this);
        }

        if (newValue is LocalVideoTrack newLocalVideoTrack)
        {
            newLocalVideoTrack.Capturer.AddDelegate(this);
        }

        return oldValue;
    }

    ~LocalTrackPublication()
    {
        Debug.Log("LocalTrackPublication deinit");
        debounceWorkItem.TryGetTarget(out var workItem);
        workItem.Cancel();
    }

    Action shouldRecomputeSenderParameters;
}

public partial class LocalTrackPublication
{
    internal async UniTask Suspend()
    {
        if (Muted) return;

        await Mute();
        this.Suspended = true;

        return;
    }

    internal async UniTask Resume()
    {
        if (!Suspended) return;

        await Unmute();
        this.Suspended = false;

        return;
    }
}

partial class LocalTrackPublication : VideoCapturerDelegate
{
    void VideoCapturerDelegate.Capturer(VideoCapturer capturer, Dimensions? didUpdateDimensions)
    {
        shouldRecomputeSenderParameters();
    }

}

public partial class LocalTrackPublication
{
    internal void RecomputeSenderParameters()
    {
        var track = Track as LocalVideoTrack;
        var sender = Track.Transceiver?.Sender;

        if (track == null || sender == null) { return; }

        var dimensions = track.Capturer.dimensions;
        if (dimensions.HasValue == false)
        {
            Debug.LogWarning("Cannot re-compute sender parameters without dimensions");
            return;
        }

        if (Participant == null)
        {
            Debug.LogWarning("Participant is null");
            return;
        }

        Debug.Log($"Re-computing sender parameters, dimensions: {track.Capturer.dimensions.ToString()}");

        // get current parameters
        var parameters = sender.GetParameters();

        Participant.TryGetTarget(out Participant participant);
        var publishOptions = (track.publishOptions as VideoPublishOptions?) ?? participant.Room._state.Value.options.defaultVideoPublishOptions;

        // re-compute encodings
        var encodings = Utils.ComputeEncodings(dimensions: dimensions.Value,
                                               publishOptions: publishOptions,
                                               isScreenShare: track.source == Track.Source.ScreenShareVideo);

        Debug.Log($"Computed encodings: {encodings}");

        foreach (var current in parameters.encodings)
        {
            var updated = encodings.FirstOrDefault(encoding => encoding.rid == current.rid);

            if (updated != null)
            {
                // update parameters for matching rid
                current.active = updated.active;
                current.scaleResolutionDownBy = updated.scaleResolutionDownBy;
                current.maxBitrate = updated.maxBitrate;
                current.maxFramerate = updated.maxFramerate;
            }
            else
            {
                current.active = false;
                current.scaleResolutionDownBy = null;
                current.maxBitrate = null;
                current.maxFramerate = null;
            }
        }

        // set the updated parameters
        sender.SetParameters(parameters);

        Debug.Log($"Using encodings: {sender.GetParameters().encodings}");

        // Report updated encodings to server

        var layers = dimensions.Value.VideoLayers(encodings);

        Debug.Log($"Using encodings layers: {layers.Select(layer => layer.ToString()).ToString().Split(", ")}");

        try
        {
            participant.Room.engine.signalClient.SendUpdateVideoLayers(track.sid, layers);
        }
        catch(Exception error)
        {
            Debug.Log("Failed to send update video layers" + error);
        }
    }
}