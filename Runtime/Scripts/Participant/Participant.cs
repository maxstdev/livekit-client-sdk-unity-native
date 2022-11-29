using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LiveKit.Proto;

using Sid = System.String;
using Cysharp.Threading.Tasks;
using static Track;
using Dispatch;

public partial class Participant : MulticastDelegate<IParticipantDelegate>
{
    internal readonly SerialQueue queue = new("LiveKitSDK.participant");

    public readonly Sid sid;
    public string identity => _state.Value.identity;
    public string name => _state.Value.name;
    public float audioLevel => _state.Value.audioLevel;
    public bool isSpeaking => _state.Value.isSpeaking;
    public string metadata => _state.Value.metadata;
    public ConnectionQuality connectionQuality => _state.Value.connectionQuality;
    public ParticipantPermissions permissions => _state.Value.permissions;
    public DateTime? joinedAt => _state.Value.joinedAt;
    public Dictionary<string, TrackPublication> tracks => _state.Value.tracks;

    public List<TrackPublication> audioTracks
    {
        get => _state.Value.tracks.Values.ToList().FindAll(t => t.Kind == Track.Kind.Audio);
    }

    public List<TrackPublication> videoTracks
    {
        get => _state.Value.tracks.Values.ToList().FindAll(t => t.Kind == Track.Kind.Video);
    }

    internal ParticipantInfo info;

    // Reference to the Room this Participant belongs to
    public readonly Room Room;

    // - Internal

    internal struct State
    {
        internal string identity;
        internal string name;
        internal float audioLevel;   // = 0.0;
        internal bool isSpeaking;    // = false;
        internal string metadata;
        internal DateTime? joinedAt;
        internal ConnectionQuality connectionQuality;    // = .unknown;
        internal ParticipantPermissions permissions;     // = ParticipantPermissions();
        internal Dictionary<string, TrackPublication> tracks;   // = [String: TrackPublication]();

        internal State(string identity = null, string name = null)
        {
            this.identity = identity;
            this.name = name;

            this.audioLevel = 0;        // = 0.0;
            this.isSpeaking = false;    // = false;
            this.metadata = default;
            this.joinedAt = default;
            this.connectionQuality = ConnectionQuality.Unknown;     // = .unknown;
            this.permissions = new ParticipantPermissions();        // = ParticipantPermissions();
            this.tracks = new Dictionary<string, TrackPublication>();   // = [String: TrackPublication]();
        }
    }

    internal StateSync<State> _state;

    internal Participant(Sid sid,
                         string identity,
                         string name,
                         Room room)
        : base()
    {
        this.sid = sid;
        this.Room = room;

        // initial state
        _state = new StateSync<State>(new State(
            identity: identity,
            name: name
        ));

        // trigger events when state mutates
        _state.OnMutate = (state, oldState) =>
        {
            if (state.isSpeaking != oldState.isSpeaking)
            {
                this.Notify((iParticipantDelegate) =>
                {
                    iParticipantDelegate.DidUpdate(this, speaking: this.isSpeaking);
                },
                () => // label:
                {
                    return $"participant.didUpdate isSpeaking: {this.isSpeaking}";
                });
            }

            // metadata updated
            string metadata = state.metadata;

            if ((metadata != null) && (metadata != oldState.metadata)
                // don't notify if empty string (first time only)
                && (oldState.metadata == null) ? !String.IsNullOrEmpty(metadata) : true)
            {
                this.Notify((iParticipantDelegate) =>
                {
                    iParticipantDelegate.DidUpdate(this, metadata: metadata);
                },
                () => // label:
                {
                    return $"participant.didUpdate metadata: {metadata}";
                });

                this.Room.Notify((iRoomDelegate) =>
                {
                    iRoomDelegate.DidUpdate(this.Room, participant: this, metadata: metadata);
                },
                () => // label:
                {
                    return $"room.didUpdate metadata: {metadata}";
                });
            }

            if (state.connectionQuality != oldState.connectionQuality)
            {
                this.Notify((iParticipantDelegate) =>
                {
                    iParticipantDelegate.DidUpdate(this, connectionQuality: this.connectionQuality);
                },
                () => // label:
                {
                    return $"participant.didUpdate connectionQuality: {this.connectionQuality}";
                });

                this.Room.Notify((iRoomDelegate) =>
                {
                    iRoomDelegate.DidUpdate(this.Room, participant: this, connectionQuality: this.connectionQuality);
                },
                () => // label:
                {
                    return $"room.didUpdate connectionQuality: {this.connectionQuality}";
                });
            }
        };
    }


    internal virtual async UniTask CleanUp(bool notify = true)
    {
        await UnpublishAll(notify: notify);

        await queue.Sync(async () =>
        {
            this._state.Mutate(state =>
            {
                state = new State(identity: state.identity, name: state.name);
                return state;
            });
            await UniTask.CompletedTask;
            return;
        });
    }

    public virtual UniTask UnpublishAll(bool notify = true)
    {
        // swift: fatalError("Unimplemented")
        Debug.LogWarning("UnpublishAll !!");
        throw new NotImplementedException();
    }

    internal void AddTrack(TrackPublication publication)
    {
        _state.Mutate(state =>
        {
            state.tracks[publication.sid] = publication;
            return state;
        });

        publication.Track?._state.Mutate(state =>
        {
            state.sid = publication.sid;
            return state;
        });
    }

    internal virtual void UpdateFromInfo(ParticipantInfo info)
    {
        _state.Mutate(state =>
        {
            state.identity = info.Identity;
            state.name = info.Name;
            state.metadata = info.Metadata;
            state.joinedAt = DateTimeOffset.FromUnixTimeSeconds(info.JoinedAt).DateTime;

            return state;
        });

        this.info = info;
        SetPermissions(info.Permission.toLKType());
    }

    internal virtual bool SetPermissions(ParticipantPermissions newValue)
    {
        if (this.permissions == newValue)
        {
            // no change
            return false;
        }

        _state.Mutate(state => { state.permissions = newValue; return state; });

        return true;
    }
}

// - Simplified API

public partial class Participant
{
    public bool IsCameraEnabled() => !(GetTrackPublication(source: Source.Camera)?.Muted ?? true);
    public bool IsMicrophoneEnabled() => !(GetTrackPublication(source: Source.Microphone)?.Muted ?? true);
    public bool IsScreenShareEnabled() => !(GetTrackPublication(source: Source.ScreenShareVideo)?.Muted ?? true);

    public TrackPublication GetTrackPublication(string name)
    {
        return _state.Value.tracks.Values.First(pub => pub.Name == name);
    }

    /// find the first publication matching `source` or any compatible.
    public TrackPublication GetTrackPublication(Track.Source source)
    {
        // if source is unknown return nil
        if (source == Track.Source.Unknown) { return null; }

        // try to find a Publication with matching source
        var result = _state.Value.tracks.Values.ToList().FirstOrDefault(publication => publication.Source == source);
        if (result != null) { return result; }

        // try to find a compatible Publication
        var unknowns = _state.Value.tracks.Values.ToList().Where(publication => publication.Source == Source.Unknown);

        var unknownFirst = unknowns.FirstOrDefault(pub =>
            (source == Source.Microphone && pub.Kind == Kind.Audio) ||
            (source == Source.Camera && pub.Kind == Kind.Video && pub.Name != Track.ScreenShareVideoName) ||
            (source == Source.ScreenShareVideo && pub.Kind == Kind.Video && pub.Name == Track.ScreenShareVideoName) ||
            (source == Source.ScreenShareAudio && pub.Kind == Kind.Audio && pub.Name == Track.ScreenShareAudioName)
        );

        if (unknownFirst != null)
        {
            return unknownFirst;
        }

        return null;
    }
}

// - Equality

public partial class Participant : IEquatable<Participant>
{
    public override int GetHashCode() => sid.GetHashCode();

    public bool Equals(Participant other)
    {
        if (other == null) return false;
        return (this.sid == other.sid) ? true : false;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        return (obj is Participant casted) ? Equals(casted) : false;
    }

    public static bool operator ==(Participant lhs, Participant rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return object.Equals(lhs, rhs);
        return lhs.Equals(rhs);
    }

    public static bool operator !=(Participant lhs, Participant rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return !object.Equals(lhs, rhs);
        return !(lhs.Equals(rhs));
    }
}
