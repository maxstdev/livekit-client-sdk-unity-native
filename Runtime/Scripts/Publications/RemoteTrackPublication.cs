using Cysharp.Threading.Tasks;
using System;
using LiveKit.Proto;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using Unity.WebRTC;
using UniLiveKit.ErrorException;

public enum SubscriptionState
{
    Subscribed,
    NotAllowed,
    Unsubscribed
}

public partial class RemoteTrackPublication : TrackPublication
{
    public bool SubscriptionAllowed => _state.Value.subscriptionAllowed;
    public bool Enabled => _state.Value.trackSettings.enabled;
    public new bool Muted => Track?.muted ?? metadataMuted;

    // - Private

    // user's preference to subscribe or not
    private bool preferSubscribed;
    private bool metadataMuted = false;

    // adaptiveStream
    // this must be on .main queue
    private DispatchQueueTimer asTimer = new DispatchQueueTimer(intervalPerSecond: 0.3);

    internal RemoteTrackPublication(TrackInfo info,
                                    Participant participant,
                                    Track track = null)
        : base(info, participant, track)
    {
        asTimer.handler = () => { this.OnAdaptiveStreamTimer(); };
    }

    ~RemoteTrackPublication()
    {
        asTimer.Suspend();
    }

    internal override void UpdateFromInfo(TrackInfo info)
    {
        base.UpdateFromInfo(info: info);
        Track?.SetMuted(info.Muted);
        SetMetadataMuted(info.Muted);
    }

    public new bool Subscribed
    {
        get
        {
            if (!SubscriptionAllowed) { return false; }
            return preferSubscribed != false && base.Subscribed;
        }
    }

    public SubscriptionState SubscriptionState
    {
        get
        {
            if (!SubscriptionAllowed) { return SubscriptionState.NotAllowed; }
            return this.Subscribed ? SubscriptionState.Subscribed : SubscriptionState.Unsubscribed;
        }
    }

    /// Subscribe or unsubscribe from this track.
    async UniTask SetSubscribed(bool newValue)
    {
        if (this.preferSubscribed == newValue) { return; }

        Participant.TryGetTarget(out Participant participant);

        if (participant == null)
        {
            Debug.LogWarning("Participant is null");
            throw new EnumException<EngineError>(EngineError.State, "Participant is nil");
        }

        var signalClient = participant.Room.engine.signalClient;

        await signalClient.SendUpdateSubscription(participantSid: participant.sid,
                                                  trackSid: sid,
                                                  subscribed: newValue);
        this.preferSubscribed = newValue;

        return;
    }

    /// Enable or disable server from sending down data for this track.
    ///
    /// This is useful when the participant is off screen, you may disable streaming down their video to reduce bandwidth requirements.
    async UniTask setEnabled(bool newValue)
    {
        // no-op if already the desired value
        if (_state.Value.trackSettings.enabled == newValue) { return; }

        if (!UserCanModifyTrackSettings)
        {
            throw new EnumException<TrackError>(TrackError.State, "adaptiveStream must be disabled and track must be subscribed");
        }

        // keep old settings
        var oldSettings = _state.Value.trackSettings;
        // update state
        _state.Mutate(state =>
        {
            state.trackSettings = state.trackSettings.CopyWith(enabled: newValue);
            return state;
        });

        await queue.Async(async () =>
        {
            try
            {
                await Send(_state.Value.trackSettings);
            }
            catch (Exception error)
            {
                // revert track settings on failure
                _state.Mutate((state) =>
                {
                    state.trackSettings = oldSettings;
                    return state;
                });
                Debug.Log($"failed to update enabled: {newValue}, sid: {sid}, error: {error}");
            }
        });
    }

    internal new Track SetTrack(Track newValue)
    {
        Debug.Log($"RemoteTrackPublication set track: {this.Track}");

        var oldValue = base.SetTrack(newValue);
        if (newValue != oldValue)
        {
            // always suspend adaptiveStream timer first
            asTimer.Suspend();

            if (newValue != null)
            {
                // reset track settings, track is initially disabled only if adaptive stream and is a video track
                ResetTrackSettings();

                Debug.Log($"[adaptiveStream] did reset trackSettings: {_state.Value.trackSettings}, kind: {newValue.kind}");

                // start adaptiveStream timer only if it's a video track
                if (isAdaptiveStreamEnabled)
                {
                    asTimer.Restart();
                }

                // if new Track has been set to this RemoteTrackPublication,
                // update the Track's muted state from the latest info.
                newValue.SetMuted(newValue: metadataMuted,
                                  notify: false);
            }

            if (this.Participant.TryGetTarget(out Participant tmpParticipant))
            {
                var participant = tmpParticipant as RemoteParticipant;

                if ((oldValue != null) && (newValue == null) && (participant != null))
                {
                    participant.Notify((iParticipantDelegate) =>
                    {
                        iParticipantDelegate.DidUnsubscribe(participant, publication: this, track: oldValue);
                    },
                    () => // label:
                    {
                        return $"participant.didUnsubscribe {this}";
                    });

                    participant.Room.Notify((iRoomDelegate) =>
                    {
                        iRoomDelegate.DidUnsubscribe(participant.Room, participant: participant, publication: this, track: oldValue);
                    },
                    () => // label:
                    {
                        return $"room.didUnsubscribe {this}";
                    });
                }
            }
        }

        return oldValue;
    }
}

// - Private

public partial class RemoteTrackPublication
{
    bool isAdaptiveStreamEnabled
    {
        get
        {
            if (Participant.TryGetTarget(out Participant participant))
            {
                return (participant?.Room?._state.Value.options ?? new RoomOptions()).adaptiveStream && Track.Kind.Video == Kind;
            }

            return false;
        }
    }

    ConnectionState.States engineConnectionState
    {
        get
        {
            if (Participant.TryGetTarget(out Participant participant))
            {
                if (participant == null)
                {
                    Debug.LogWarning("Participant is null");
                    return ConnectionState.States.Disconnected;
                }
            }

            return participant.Room.engine._state.Value.connectionState.State;
        }
    }

    bool UserCanModifyTrackSettings
    {
        get
        {
            // adaptiveStream must be disabled and must be subscribed
            return !isAdaptiveStreamEnabled && Subscribed;
        }
    }
}

// - Internal

public partial class RemoteTrackPublication
{
    internal void SetMetadataMuted(bool newValue)
    {
        if (this.metadataMuted == newValue) { return; }

        if (this.Participant.TryGetTarget(out Participant participant))
        {
            if (participant == null)
            {
                Debug.LogWarning("Participant is null");
                return;
            }
        }

        this.metadataMuted = newValue;

        // if track exists, track will emit the following events
        if (Track == null)
        {
            participant.Notify((iParticipantDelegate) =>
            {
                iParticipantDelegate.DidUpdate(participant, publication: this, muted: newValue);
            }, () => // label:
            {
                return $"participant.didUpdate muted: {newValue}";
            });

            participant.Room.Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidUpdate(participant.Room, participant: participant, publication: this, muted: newValue);
            }, () => // label:
            {
                return $"room.didUpdate muted: {newValue}";
            });
        }
    }

    internal void SetSubscriptionAllowed(bool newValue)
    {
        if (_state.Value.subscriptionAllowed == newValue) { return; }
        _state.Mutate(t => { t.subscriptionAllowed = newValue; return t; });

        if (this.Participant.TryGetTarget(out Participant tmpParticipant))
        {
            var participant = tmpParticipant as RemoteParticipant;
            if (participant == null) { return; }

            participant.Notify((iParticipantDelegate) =>
            {
                iParticipantDelegate.DidUpdate(participant, publication: this, permissionAllowed: newValue);
            }, () => // label:
            {
                return $"participant.didUpdate permission: {newValue}";
            });

            participant.Room.Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidUpdate(participant.Room, participant: participant, publication: this, permissionAllowed: newValue);
            }, () => // label:
            {
                return $"room.didUpdate permission: {newValue}";
            });
        }
    }
}

// - TrackSettings

public partial class RemoteTrackPublication
{
    // reset track settings
    internal void ResetTrackSettings()
    {
        // track is initially disabled when adaptive stream is enabled
        _state.Mutate(state =>
        {
            state.trackSettings = new TrackSettings(enabled: !isAdaptiveStreamEnabled, dimensions: Dimensions.Zero);
            return state;
        });

    }

    // simply send track settings
    async UniTask Send(TrackSettings trackSettings)
    {
        
        if (Participant == null)
        {
            Debug.LogWarning("Participant is null");
            throw new EnumException<EngineError>(EngineError.State, "Participant is null");
        }

        Debug.Log($"[adaptiveStream] sending {trackSettings}, sid: {sid}");

        Participant.TryGetTarget(out Participant participant);
        await participant.Room.engine.signalClient.SendUpdateTrackSettings(sid: sid, settings: trackSettings);

        return;
    }
}

// - Adaptive Stream

// swift : internal extension Collection where Element == VideoRenderer {
internal static class VideoRendererListExtension
{
    internal static bool ContainsOneOrMoreAdaptiveStreamEnabledRenderers(this List<IVideoRenderer> videoRenderers)
    {
        // not visible if no entry
        if (videoRenderers.Count == 0) { return false; }
        // at least 1 entry should be visible
        return videoRenderers.Exists(t => t.IsVisible);
    }

    internal static SizeF LargestSize(this List<IVideoRenderer> videoRenderers)
    {
        SizeF maxSizeF(SizeF s1, SizeF s2)
        {
            return new SizeF(width: Math.Max(s1.Width, s2.Width),
                             height: Math.Max(s1.Width, s2.Width));
        }

        bool isNotZeroSize(SizeF sizef)
        {
            return (sizef.Width == 0 && sizef.Height == 0) ? false : true;
        }

        // use post-layout nativeRenderer's view size otherwise return nil
        // which results lower layer to be requested (enabled: true, dimensions: 0x0)

        var enabledRenderers = videoRenderers.Where(renderer => renderer.AdaptiveStreamIsEnabled);
        var adaptiveStreamSizes = enabledRenderers.Select(selected => selected.AdaptiveStreamSize);
        var nullRemoveds = adaptiveStreamSizes.Where(size => isNotZeroSize(size));

        var aggregated = nullRemoveds.Aggregate(new SizeF(0, 0), (previous, current) =>
        {
            return (previous == null) ? current : maxSizeF(previous, current);
        });

        return aggregated;

        // TODO:Thomas: 차후 번역이 됐는지 확인이 필요함
        // swift : 
        //return filter { $0.adaptiveStreamIsEnabled }
        //    .compactMap { $0.adaptiveStreamSize != .zero ? $0.adaptiveStreamSize : nil }
        //    .reduce(into: nil as CGSize?, { previous, current in
        //        guard let unwrappedPrevious = previous else {
        //            previous = current
        //            return
        //        }
        //        previous = maxCGSize(unwrappedPrevious, current)
        //    })
    }
}

public partial class RemoteTrackPublication
{
    // executed on .main
    private void OnAdaptiveStreamTimer()
    {
        // TODO:Thomas: 필요하면 구현
        //// this should never happen
        //assert(Thread.current.isMainThread, "this method must be called from main thread")

        // suspend timer first
        asTimer.Suspend();

        // don't continue if the engine is disconnected
        if (engineConnectionState.isDisconnected())
        {
            Debug.Log("engine is disconnected");
            return;
        }

        var videoRendererPairList = Track?.VideoRenderers.ToList();
        var videoRenderers = (List<IVideoRenderer>)videoRendererPairList.Select(pair => pair.Value);

        var enabled = videoRenderers.ContainsOneOrMoreAdaptiveStreamEnabledRenderers();
        var dimensions = Dimensions.Zero;

        // compute the largest video view size
        if (enabled)
        {
            var maxSize = videoRenderers.LargestSize();
            dimensions = new Dimensions(width: (Int32)(Math.Ceiling(maxSize.Width)),
                                        height: (Int32)(Math.Ceiling(maxSize.Height)));
        }

        var newSettings = _state.Value.trackSettings.CopyWith(enabled: enabled, dimensions: dimensions);

        if (_state.Value.trackSettings == newSettings)
        {
            // FIXME:Thomas: awit Resume() 관련 점검이 필요함
            // no settings updated
            asTimer.Resume();
            return;
        }

        // keep old settings
        var oldSettings = _state.Value.trackSettings;
        // update state
        _state.Mutate(state => { state.trackSettings = newSettings; return state; });

        // log when flipping from enabled -> disabled
        if (oldSettings.enabled && !newSettings.enabled)
        {
            var viewsStringArray = videoRenderers.Select((v, idx) => $"videoRenderer{idx}(adaptiveStreamIsEnabled: {v.AdaptiveStreamIsEnabled}, adaptiveStreamSize: {v.AdaptiveStreamSize}").ToArray();

            var viewsString = string.Join(", ", viewsStringArray);

            Debug.Log($"[adaptiveStream] disabling sid: {sid}, videoRenderersCount: {videoRenderers.Count}, {viewsString}");
        }

        VideoStreamTrack videoTrack = Track?.MediaTrack as VideoStreamTrack;
        if (videoTrack != null)
        {
            Debug.Log($"VideoTrack.shouldReceive: {enabled}");
            DispatchQueue.WebRTC.Sync(() => { videoTrack.Enabled = enabled; });
        }

        queue.Async(async () =>
        {
            try
            {
                await Send(newSettings);
            }
            catch(Exception error)
            {
                // revert to old settings on failure
                _state.Mutate((state) =>
                {
                    state.trackSettings = oldSettings;
                    return state;
                });
                Debug.LogError($"[adaptiveStream] failed to send trackSettings, sid: {sid}, error: {error}");
            }

            asTimer.Restart();
        });
    }
}