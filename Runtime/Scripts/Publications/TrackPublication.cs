using Cysharp.Threading.Tasks;
using Dispatch;
using LiveKit.Proto;
using System;
using UnityEngine;
using Sid = System.String;

public partial class TrackPublication
{
    internal SerialQueue queue = new("LiveKitSDK.publication");

    public Sid sid;
    public Track.Kind Kind;
    public Track.Source Source;

    public String Name => _state.Value.name;
    public Track Track => _state.Value.track;
    public bool Muted => Track?._state.Value.muted ?? false;
    public StreamState streamState => _state.Value.streamState;

    /// video-only
    public Dimensions? dimensions => _state.Value.dimensions;
    public bool Simulcasted => _state.Value.simulcasted;

    /// MIME type of the ``Track``.
    public string MimeType => _state.Value.mimeType;
    public bool Subscribed => _state.Value.track != null;

    // - Internal

    /// Reference to the ``Participant`` this publication belongs to.
    internal WeakReference<Participant> Participant;
    internal TrackInfo LatestInfo { get; private set; }

    internal struct State
    {
        internal Track track;
        internal string name;
        internal string mimeType;
        internal bool simulcasted; // = false;
        internal Dimensions? dimensions;
        // subscription permission
        internal bool subscriptionAllowed; // = true;
        //
        internal StreamState streamState; // = .paused
        internal TrackSettings trackSettings; // = new TrackSettings();

        internal State(string name = null, string mimeType = null)
        {
            this.name = name;
            this.mimeType = mimeType;

            this.track = null;
            //this.name = null;
            //this.mimeType = null;
            this.simulcasted = false; // = false;
            this.dimensions = null;

            this.subscriptionAllowed = true; // = true;

            this.streamState = StreamState.Paused; // = .paused
            this.trackSettings = new(); // TrackSettings()
        }
    }

    internal StateSync<State> _state;

    internal TrackPublication(TrackInfo info,
                              Participant participant,
                              Track track = null)
    {
        // initial state
        _state = new StateSync<State>(new State(
            name: info.Name,
            mimeType: info.MimeType
        ));

        this.sid = info.Sid;
        this.Kind = info.Type.ToLKType();
        this.Source = info.Source.ToLKType();
        this.Participant = new WeakReference<Participant>(participant);
        this.SetTrack(newValue: track);
        UpdateFromInfo(info: info);

        // listen for events from Track
        track?.AddDelegate(this);

        // trigger events when state mutates
        this._state.OnMutate = (state, oldState) =>
        {
            // TODO:thomas: C#에서 대응 로직 연구 필요, 필요하긴 할까?
            //guard let self = self else { return }

            if (state.streamState != oldState.streamState)
            {
                if (this.Participant.TryGetTarget(out Participant tmpParticipant))
                {
                    var participant = tmpParticipant as RemoteParticipant;
                    var trackPublication = this as RemoteTrackPublication;

                    if ((participant != null) && (trackPublication != null))
                    {
                        participant.Notify((iParticipantDelegate) =>
                        {
                            iParticipantDelegate.DidUpdate(participant, publication: trackPublication, streamState: state.streamState);
                        }, () => // label:
                        {
                            return $"participant.didUpdate {trackPublication} streamState: {state.streamState}";
                        });

                        participant.Room.Notify((iRoomDelegate) =>
                        {
                            iRoomDelegate.DidUpdate(participant.Room, participant: participant, publication: trackPublication, streamState: state.streamState);
                        }, () =>    // label:
                        {
                            return $"room.didUpdate {trackPublication} streamState: {state.streamState}";
                        });
                    }
                }
            }
        };
    }

    ~TrackPublication()
    {
        Debug.Log($"TrackPublication Destruct sid: {sid}");
    }

    internal virtual void UpdateFromInfo(TrackInfo info)
    {
        _state.Mutate((t) =>
        {
            // only muted and name can conceivably update
            t.name = info.Name;
            t.simulcasted = info.Simulcast;
            t.mimeType = info.MimeType;

            // only for video
            if (info.Type == TrackType.Video)
            {
                t.dimensions = new Dimensions(width: (Int32)info.Width,
                                              height: (Int32)info.Height);
            }

            return t;
        });

        this.LatestInfo = info;
    }

    internal Track SetTrack(Track newValue)
    {
        // keep ref to old value
        var oldValue = this.Track;
        // continue only if updated
        if (this.Track == newValue) { return oldValue; }
        Debug.Log($"{oldValue?.ToString()} -> {newValue?.ToString()}");

        // listen for visibility updates
        this.Track?.RemoveDelegate(this);
        newValue?.AddDelegate(this);

        _state.Mutate((t) => { t.track = newValue; return t; });
        return oldValue;
    }
}

public partial class TrackPublication : ITrackDelegate
{
    void ITrackDelegate.DidUpdate(Track track, bool muted, bool shouldSendSignal)
    {
        Debug.Log($"muted: {muted} shouldSendSignal: {shouldSendSignal}");

        var isTry = Participant.TryGetTarget(out var participant);

        if (!isTry)
        {
            Debug.LogWarning("Participant is null");
            return;
        }

        UniTask sendSignal()
        {
            if (shouldSendSignal == false)
            {
                return UniTask.CompletedTask;
            }

            return participant.Room.engine.signalClient.SendMuteTrack(sid, Muted);
        };

        queue.Async(async () =>
        {
            try
            {
                await sendSignal();
            }
            catch (Exception error)
            {
                Debug.Log("File to top al tacks, error: " + error);
            }

            participant.Notify((iParticipantDelegate) =>
            {
                iParticipantDelegate.DidUpdate(participant, this, Muted);
            });

            participant.Room.Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidUpdate(participant.Room, participant, this, Muted);
            });
        });
    }
}

public partial class TrackPublication : IEquatable<TrackPublication>
{
    // objects are considered equal if sids are the same

    public override int GetHashCode() => base.GetHashCode();

    public bool Equals(TrackPublication other)
    {
        if (other == null) return false;
        return (this.sid == other.sid) ? true : false;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        return (obj is TrackPublication casted) ? Equals(casted) : false;
    }

    public static bool operator ==(TrackPublication lhs, TrackPublication rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return object.Equals(lhs, rhs);
        return lhs.Equals(rhs);
    }

    public static bool operator !=(TrackPublication lhs, TrackPublication rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return !object.Equals(lhs, rhs);
        return !(lhs.Equals(rhs));
    }
}