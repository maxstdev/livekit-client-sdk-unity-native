using Cysharp.Threading.Tasks;
using Dispatch;
using LiveKit.Proto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UniLiveKit.ErrorException;
using Unity.WebRTC;
using UnityEngine;
using Sid = System.String;

public partial class Room : MulticastDelegate<IRoomDelegate>
{
    internal SerialQueue queue = new("LiveKitSDK.room");

    // public
    public Sid Sid => _state.Value.sid;
    public string Name => _state.Value.name;
    public string Metadata => _state.Value.metadata;
    public string ServerVersion => _state.Value.serverVersion;
    public string ServerRegion => _state.Value.serverRegion;

    public LocalParticipant LocalParticipant => _state.Value.localParticipant;
    public Dictionary<Sid, RemoteParticipant> RemoteParticipants => _state.Value.remoteParicipants;
    public List<Participant> ActiveSpeakers => _state.Value.activeSpeakers;

    // expose engine's vers
    public string Url => engine._state.Value.url;
    public string Token => engine._state.Value.token;
    public ConnectionState ConnectionState => engine._state.Value.connectionState;
    public Support.Stopwatch ConnectStopwatch => engine._state.Value.connectStopwatch;

    // Internal
    internal Engine engine;

    internal struct State
    {
        internal RoomOptions options;

        internal string sid;
        internal string name;
        internal string metadata;
        internal string serverVersion;
        internal string serverRegion;

        internal LocalParticipant localParticipant;
        internal Dictionary<Sid, RemoteParticipant> remoteParicipants;
        internal List<Participant> activeSpeakers;

        internal State(RoomOptions options)
        {
            this.options = options;

            this.sid = null;
            this.name = null;
            this.metadata = null;
            this.serverVersion = null;
            this.serverRegion = null;

            this.localParticipant = null;
            this.remoteParicipants = new();
            this.activeSpeakers = new();
        }


        internal RemoteParticipant GetOrCreateRemoteParticipant(Sid sid, Room room, ParticipantInfo info = null)
        {
            if (remoteParicipants.ContainsKey(sid))
            {
                return remoteParicipants[sid];
            }

            var participant = new RemoteParticipant(sid, info, room);
            remoteParicipants[sid] = participant;
            return participant;
        }
    }

    internal StateSync<State> _state;

    public Room(
        IRoomDelegate iRoomDelegate = null,
        ConnectOptions? connectOptions = null,
        RoomOptions? roomOptions = null) : base()
    {
        var connectOptionsValue = connectOptions ?? new ConnectOptions(null);
        var roomOptionsValue = roomOptions ?? new RoomOptions(null);

        _state = new StateSync<State>(new State(roomOptionsValue));
        engine = new Engine(connectOptionsValue);

        Debug.Log("init Room");

        engine.room = new WeakReference<Room>(this);
        // listen to engine & signalClient
        engine.AddDelegate(this);
        engine.signalClient.AddDelegate(this);

        if (iRoomDelegate != null)
        {
            AddDelegate(iRoomDelegate);
        }

        // listen to app states
        AppStateListener.Shared.listener.AddDelegate(this);

        _state.OnMutate = (state, oldState) =>
        {
            var matadata = state.metadata;

            // metadata updated
            if (Metadata != oldState.metadata && ((oldState.metadata == null) ? !(Metadata.Length <= 0) : true))
            {
                engine.ExecuteIfConnected(() =>
                {
                    Notify((iRoomDelegate) =>
                    {
                        iRoomDelegate.DidUpdate(this, Metadata);
                    }, () =>
                    {
                        return $"room.didUpdate metadata: {Metadata}";
                    });
                });
            }
        };
    }

    ~Room()
    {
        Debug.Log("Room Destruct");
    }

    public async UniTask<Room> Connect(string url, string token, ConnectOptions? connectOptions = null, RoomOptions? roomOptions = null)
    {
        Debug.Log("connecting to room...");

        var state = _state.ReadCopy();

        if (state.localParticipant != null)
        {
            Debug.Log("localParticipant is not nil");

            throw new EnumException<EngineError>(EngineError.State, "localParticipant is not nil");
        }

        // update options if specified

        if (roomOptions is RoomOptions _roomOptions && !roomOptions.Equals(state.options))
        {
            _state.Mutate((state) => { state.options = _roomOptions; return state; });
        }

        // monitor.start(queue: monitorQueue)

        await queue.Sync(async () =>
        {
            await engine.Connect(url, token, connectOptions);
        });

        Debug.Log($"connected to {ToString()} {state.localParticipant}");

        return this;
    }

    public async UniTask Disconnect()
    {
        if (ConnectionState.State == ConnectionState.States.Disconnected) return;

        await queue.Sync(async () =>
        {
            try
            {
                await engine.signalClient.SendLeave();
            }
            catch (Exception error)
            {
                Debug.Log($"Failed to send leave, error {error}");
            }

            await CleanUp(DisconnectReason.user);
        });
    }
}

// Internal
public partial class Room
{
    // Resets state of Room
    internal async UniTask CleanUp(DisconnectReason? reason = null, bool isFullReconnect = false)
    {
        Debug.Log("Room CleanUp - reason : " + reason);

        engine._state.Mutate((state) =>
        {
            state.primaryTransportConnectedCompleter.Reset();
            state.publisherTransportConnectedCompleter.Reset();
            state.publisherReliableDCOpenCompleter.Reset();
            state.publisherLossyDCOpenCompleter.Reset();

            state = isFullReconnect ? new Engine.State(
                state.connectOptions,
                state.url,
                state.token,
                state.nextPreferredReconnectMode,
                state.reconnectMode,
                state.connectionState)
            : new Engine.State(
                state.connectOptions,
                null,
                null,
                null,
                null,
                new(ConnectionState.States.Disconnected, reason));

            return state;
        });

        engine.signalClient.CleanUp(reason);

        await queue.Sync(async () =>
        {
            try
            {
                await engine.CleanUpRTC();
                await CleanUpParticipants();
                _state.Mutate((state) => { state = new State(state.options); return state; });
            }
            catch (Exception error)
            {
                Debug.LogError("Room CleanUp failed with error: " + error);
            }
        });
    }
}

// Parivate

public partial class Room
{
    async UniTask CleanUpParticipants(bool notify = true)
    {
        Debug.Log("notify : " + notify);

        // Stop all local & remote tracks
        var allParticipants = new List<Participant>() { LocalParticipant };

        _state.Value.remoteParicipants
            .Values
            .Select((e) => { return e as Participant; })
            .Where(p => p != null)
            .ToList()
            .ForEach(participant => { allParticipants.Add(participant); });


        var cleanUpPromiss = allParticipants.Where(p => p != null).Select(p => p.CleanUp(notify));

        await queue.Sync(async () =>
        {
            await UniTask.WhenAll(cleanUpPromiss);

            _state.Mutate(state =>
            {
                state.localParticipant = null;
                state.remoteParicipants = new();
                return state;
            });
        });

        return;
    }

    UniTask OnParticipantDisconnect(Sid sid)
    {
        var participant = _state.Mutate<RemoteParticipant>(state =>
        {
            var rValue = state.remoteParicipants[sid];
            state.remoteParicipants.Remove(sid);
            return new(state, rValue);
        });

        if (participant == null)
        {
            throw new EnumException<EngineError>(EngineError.State, "Participant not founc for " + sid);
        }

        return participant.CleanUp(true);
    }
}


// Debugging
public partial class Room
{
    public UniTask SendSimulate(SimulateScenario scenario, int? secs)
    {
        engine.signalClient.SendSimulate(scenario, secs);
        return UniTask.CompletedTask;
    }
}

// Session Migration
public partial class Room
{
    internal void ResetTrackSettings()
    {
        Debug.Log("resetting track settings...");

        // create an array of RemoteTrackPublication
        var remoteTrackPublications = _state.Value.remoteParicipants.Values.Select(p =>
        {
            return p._state.Value.tracks.Values.Where(e => { return e != null; }).Select(e => e as RemoteTrackPublication);
        }).SelectMany(e => e);

        // reset track settings for all RemoteTrackPublication
        foreach (var publication in remoteTrackPublications)
        {
            publication.ResetTrackSettings();
        }
    }

    internal UniTask SendSyncState()
    {
        var subscriber = engine.Subscriber;

        if (subscriber == null) return UniTask.CompletedTask;

        if (subscriber.LocalDescription is RTCSessionDescription localDescription)
        {
            var sendUnSub = engine._state.Value.connectOptions.autoSubscribe;
            var participantTracks = _state.Value.remoteParicipants.Values.Select((participant) =>
            {
                var tracks = new ParticipantTracks() { ParticipantSid = participant.sid };
                var sids = participant._state.Value.tracks.Values.Where(e => { return e.Subscribed != sendUnSub; }).Select(e => e.sid).ToList();
                sids.ForEach(sid => tracks.TrackSids.Add(sid));
                return tracks;
            });

            // Backward compatibility
            var trackSids = participantTracks.Select(e => e.TrackSids).ToList();
            Debug.Log("trackSids : " + JsonConvert.SerializeObject(trackSids));

            var subscription = new LiveKit.Proto.UpdateSubscription() { Subscribe = !sendUnSub };
            trackSids.ForEach(sid => subscription.TrackSids.Add(sid));
            participantTracks.ToList().ForEach(track => subscription.ParticipantTracks.Add(track));

            return engine.signalClient.SendSyncState(localDescription.toPBType(), subscription, _state.Value.localParticipant.PublishedTracksInfo(), engine.DataChannelInfo());
        }

        return UniTask.CompletedTask;
    }
}

// ISignalClientDelegate

public partial class Room : ISignalClientDelegate
{
    bool ISignalClientDelegate.DidReceiveLeave(SignalClient signalClient, bool canReconnect)
    {
        Debug.Log("canReconnect : " + canReconnect);

        if (canReconnect)
        {
            // force .full for next reconnect
            engine._state.Mutate((state) =>
            {
                state.nextPreferredReconnectMode = ReconnectMode.Full;
                return state;
            });
        }
        else
        {
            // server indicates it's not recoverable
            var error = new EnumException<NetworkError>(NetworkError.Disconnected, "did receive leave");
            CleanUp(DisconnectReason.networkError.Reason(error)).Forget();
        }

        return true;
    }
    bool ISignalClientDelegate.DidUpdate(SignalClient signalClient, string trackSid, SubscribedQuality[] subscribedQualities)
    {
        Debug.Log("qualities : " + String.Join(", ", subscribedQualities.Select((e) => e.ToString()).ToList()));

        if (_state.Value.localParticipant != null)
        {
            LocalParticipant.OnSubscribedQualitiesUpdate(trackSid, subscribedQualities);
        }

        return true;
    }
    bool ISignalClientDelegate.DidReceive(SignalClient signalClient, JoinResponse joinResponse)
    {
        Debug.Log($"server version: {joinResponse.ServerVersion}, region: {joinResponse.ServerRegion}");

        _state.Mutate((state) =>
        {
            state.sid = joinResponse.Room.Sid;
            state.name = joinResponse.Room.Name;
            state.metadata = joinResponse.Room.Metadata;
            state.serverVersion = joinResponse.ServerVersion;
            state.serverRegion = String.IsNullOrEmpty(joinResponse.ServerRegion) ? null : joinResponse.ServerRegion;

            if (joinResponse.Participant != null)
            {
                state.localParticipant = new LocalParticipant(joinResponse.Participant, this);
            }

            if (joinResponse.OtherParticipants != null && joinResponse.OtherParticipants.Count() > 0)
            {
                foreach (var otherParticipant in joinResponse.OtherParticipants)
                {
                    state.GetOrCreateRemoteParticipant(otherParticipant.Sid, this, otherParticipant);
                }
            }
            return state;
        });

        return true;
    }
    bool ISignalClientDelegate.DidUpdate(SignalClient signalClient, LiveKit.Proto.Room room)
    {
        _state.Mutate((state) =>
        {
            state.metadata = room.Metadata;
            return state;
        });
        return true;
    }
    bool ISignalClientDelegate.DidUpdate(SignalClient signalClient, SpeakerInfo[] speakers)
    {
        Debug.Log("speakers : " + speakers);

        var activeSpeakers = _state.Mutate<List<Participant>>((state) =>
        {
            Dictionary<Sid, Participant> lastSpeakers = new();
            foreach (var speaker in state.activeSpeakers)
            {
                lastSpeakers.Add(speaker.sid, speaker);
            }

            foreach (var speaker in speakers)
            {
                Participant participant = null;
                if (speaker.Sid == state.localParticipant?.sid)
                {
                    participant = state.localParticipant;
                }
                else
                {
                    RemoteParticipant remoteParticipant = null;
                    state.remoteParicipants.TryGetValue(speaker.Sid, out remoteParticipant);
                    participant = remoteParticipant;
                }

                if (participant == null) continue;

                participant._state.Mutate((state) =>
                {
                    state.audioLevel = speaker.Level;
                    state.isSpeaking = speaker.Active;
                    return state;
                });

                if (speaker.Active)
                {
                    lastSpeakers[speaker.Sid] = participant;
                }
                else
                {
                    lastSpeakers.Remove(speaker.Sid);
                }
            }

            state.activeSpeakers = lastSpeakers.Values.OrderBy((participant) => { return participant; }).ToList();
            return new(state, state.activeSpeakers);
        });

        engine.ExecuteIfConnected(() =>
        {
            Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidUpdate(this, activeSpeakers.ToArray());
            }, () =>
            {
                return "room.didUpdate speakers : " + JsonConvert.SerializeObject(speakers);
            });
        });

        return true;
    }
    bool ISignalClientDelegate.DidUpdate(SignalClient signalClient, ConnectionQualityInfo[] connectionQuality)
    {
        Debug.Log("connectionQuality : " + JsonConvert.SerializeObject(connectionQuality));

        foreach (var entry in connectionQuality)
        {
            if (_state.Value.localParticipant != null && entry.ParticipantSid == LocalParticipant.sid)
            {
                // update for LocalParticipant
                LocalParticipant._state.Mutate((state) => { state.connectionQuality = entry.Quality.toLKType(); return state; });
            }
            else if (_state.Value.remoteParicipants.ContainsKey(entry.ParticipantSid))
            {
                var participant = _state.Value.remoteParicipants[entry.ParticipantSid];
                participant._state.Mutate((state) => { state.connectionQuality = entry.Quality.toLKType(); return state; });
            }
        }
        return true;
    }
    bool ISignalClientDelegate.DidUpdateRemoteMute(SignalClient signalClient, string trackSid, bool muted)
    {
        Debug.Log($"trackSid : {trackSid} muted: {muted}");

        // publication was not found but the delegate was handled
        if (_state.Value.localParticipant == null) return true;

        if (!_state.Value.localParticipant._state.Value.tracks.ContainsKey(trackSid)) return true;

        var track = _state.Value.localParticipant._state.Value.tracks[trackSid];

        if (track is not LocalTrackPublication publication) return true;

        if (muted)
        {
            publication.Mute().Forget();
        }
        else
        {
            publication.Unmute().Forget();
        }
        return true;
    }
    bool ISignalClientDelegate.DidUpdate(SignalClient signalClient, SubscriptionPermissionUpdate subscriptionPermission)
    {
        Debug.Log("did update subscriptionPermission : " + subscriptionPermission);

        if (_state.Value.remoteParicipants.ContainsKey(subscriptionPermission.ParticipantSid)) return true;
        var participant = _state.Value.remoteParicipants[subscriptionPermission.ParticipantSid];
        var publication = participant.GetRemoteTrackPublication(subscriptionPermission.TrackSid);
        if (publication == null) return true;

        publication.SetSubscriptionAllowed(subscriptionPermission.Allowed);

        return true;
    }
    bool ISignalClientDelegate.DidUpdate(SignalClient signalClient, StreamStateInfo[] trackStates)
    {
        Debug.Log("did update trackStates : " + string.Join(", ", trackStates.Select(state => { return $"{state.TrackSid} : {state.State}"; })));

        foreach (var update in trackStates)
        {
            // Try to find RemoteParticipant
            if (!_state.Value.remoteParicipants.TryGetValue(update.ParticipantSid, out var participant)) continue;
            // Try to find RemoteTrackPUblication
            if (!participant._state.Value.tracks.TryGetValue(update.TrackSid, out var track)) continue;
            // Update streamState (and notify)
            if (track is not RemoteTrackPublication trackPublication) continue;

            trackPublication._state.Mutate((state) =>
            {
                state.streamState = update.State.toLKType();
                return state;
            });
        }

        return true;
    }
    bool ISignalClientDelegate.DidUpdate(SignalClient signalClient, ParticipantInfo[] participants)
    {
        Debug.Log("participants : " + participants.ToString());

        List<Sid> disconnectedParticipants = new();
        List<RemoteParticipant> newParticipants = new();

        _state.Mutate((state) =>
        {
            foreach (var info in participants)
            {
                if (info.Sid == state.localParticipant.sid)
                {
                    state.localParticipant?.UpdateFromInfo(info);
                    continue;
                }

                var isNewParticipant = !state.remoteParicipants.TryGetValue(info.Sid, out RemoteParticipant _);
                var participant = state.GetOrCreateRemoteParticipant(info.Sid, this, info);

                if (info.State == ParticipantInfo.Types.State.Disconnected)
                {
                    disconnectedParticipants.Add(info.Sid);
                }
                else if (isNewParticipant)
                {
                    newParticipants.Add(participant);
                }
                else
                {
                    participant.UpdateFromInfo(info);
                }

                state.remoteParicipants[info.Sid] = participant;
            }
            return state;
        });

        foreach (var sid in disconnectedParticipants)
        {
            OnParticipantDisconnect(sid);
        }

        foreach (var participant in newParticipants)
        {
            engine.ExecuteIfConnected(() =>
            {
                Notify((iRoomDelegate) =>
                {
                    iRoomDelegate.DidJoin(this, participant);
                },
                () =>
                {
                    return $"room.participantDidJoin participant : {participant}";
                });
            });
        }

        return true;
    }
    bool ISignalClientDelegate.DidUnpublish(SignalClient signalClient, TrackUnpublishedResponse localTrack)
    {
        Debug.Log("");

        if (LocalParticipant == null)
        {
            Debug.LogWarning("track publication not found");
            return true;
        }

        if (!LocalParticipant._state.Value.tracks.TryGetValue(localTrack.TrackSid, out var track))
        {
            Debug.LogWarning("track publication not found");
            return true;
        }

        if (track is not LocalTrackPublication publication)
        {
            Debug.LogWarning("track publication not found");
            return true;
        }

        queue.Async(async () =>
        {
            try
            {
                await LocalParticipant.Unpublish(publication);
                Debug.Log("unpublished track " + localTrack.TrackSid);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"failed to unpublish track{localTrack.TrackSid}, error : {error}");
            }
        });

        return true;
    }
}

// IEngineDelegate
public partial class Room : IEngineDelegate
{
    void IEngineDelegate.DidUpdate(Engine engine, RTCDataChannel dataChannel, RTCDataChannelState state)
    {
        return;
    }

    void IEngineDelegate.DidMutate(Engine engine, Engine.State state, Engine.State oldState)
    {
        if ((state.connectionState.State != oldState.connectionState.State))
        {
            // connectionState did update

            // only if quick-reconnect
            if (state.connectionState.State == ConnectionState.States.Connected && state.reconnectMode == ReconnectMode.Quick)
            {
                queue.Async(async () =>
                {
                    try
                    {
                        await SendSyncState();
                    }
                    catch (Exception error)
                    {
                        Debug.LogError("Failed to sendSyncState, errror : " + error);
                    }
                });

                ResetTrackSettings();
            }

            // re-send track permissions
            if (state.connectionState.State == ConnectionState.States.Connected && LocalParticipant != null)
            {
                queue.Async(async () =>
                {
                    try
                    {
                        await LocalParticipant.SendTrackSubscriptionPermissions();
                    }
                    catch (Exception error)
                    {
                        Debug.LogError("Failed to send track subscription permissions, error: " + error);
                    }
                });
            }

            Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidUpdate(this, state.connectionState, oldState.connectionState);
            }, () =>
            {
                return $"room.didUpdate connectionState : {state.connectionState.State} oldValue: {oldState.connectionState.State}";
            });
        }

        if (state.connectionState.State.isReconnecting() && state.reconnectMode == ReconnectMode.Full && oldState.reconnectMode != ReconnectMode.Full)
        {
            // started full reconnect
            CleanUpParticipants(true).Forget();
        }
    }

    void IEngineDelegate.DidGenerate(Engine enigne, TrackStats[] trackStats, LiveKit.Proto.SignalTarget target)
    {
        var allParticipants = new List<Participant>() { LocalParticipant };

        _state.Value.remoteParicipants
            .Values
            .Select((e) => { return e as Participant; })
            .Where(p => p != null)
            .ToList()
            .ForEach(participant => { allParticipants.Add(participant); });

        var allTracks = allParticipants.SelectMany((e) => { return e._state.Value.tracks.Values.Select((v) => v.Track).ToList(); }).Where((e) => { return e != null; });

        // this relies on the last stat entry being the latest
        foreach (var track in allTracks)
        {
            var stats = trackStats.Where((e) => { return e.TrackId == track.MediaTrack.Id; }).Last();
            if (stats != null)
            {
                track.Set(stats);
            }
        }

    }

    void IEngineDelegate.DidUpdate(Engine engine, SpeakerInfo[] speakers)
    {
        var activeSpeakers = _state.Mutate<List<Participant>>((state) =>
        {
            List<Participant> activeSpeakers = new();
            Dictionary<string, bool> seenSids = new();

            foreach (var speaker in speakers)
            {
                var _localParticipant = state.localParticipant;
                if (_localParticipant != null && speaker.Sid == _localParticipant.sid)
                {
                    _localParticipant._state.Mutate((participantState) =>
                    {
                        participantState.audioLevel = speaker.Level;
                        participantState.isSpeaking = true;
                        return participantState;
                    });
                    activeSpeakers.Add(_localParticipant);
                }
                else
                {
                    if (state.remoteParicipants.TryGetValue(speaker.Sid, out var participant))
                    {
                        participant._state.Mutate((participantState) =>
                        {
                            participantState.audioLevel = speaker.Level;
                            participantState.isSpeaking = true;
                            return participantState;
                        });
                        activeSpeakers.Add(participant);
                    }
                }
            }

            var localParticipant = state.localParticipant;
            if (localParticipant != null && !seenSids.TryGetValue(localParticipant.sid, out bool _))
            {
                localParticipant._state.Mutate((state) =>
                {
                    state.audioLevel = 0.0F;
                    state.isSpeaking = false;
                    return state;
                });
            }

            foreach (var participant in state.remoteParicipants.Values)
            {
                if (!seenSids.TryGetValue(participant.sid, out var _))
                {
                    participant._state.Mutate((state) =>
                    {
                        state.audioLevel = 0.0F;
                        state.isSpeaking = false;
                        return state;
                    });
                }
            }

            return new(state, activeSpeakers);
        });

        engine.ExecuteIfConnected(() =>
        {
            Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidUpdate(this, activeSpeakers.ToArray());
            }, () =>
            {
                return "room.didUpdate speakers: " + JsonConvert.SerializeObject(activeSpeakers);
            });
        });
    }

    void IEngineDelegate.DidAdd(Engine engine, MediaStreamTrack track, MediaStream[] streams)
    {
        if (streams.Count() <= 0)
        {
            Debug.LogWarning("Received onTrack with no streams!");
            return;
        }

        var unpacked = streams[0].Id.Unpack();
        var participantSid = unpacked.sid;
        var trackSid = unpacked.trackId;
        if (trackSid == "")
        {
            trackSid = track.Id;
        }

        var participant = _state.Mutate<RemoteParticipant>((state) => { return new(state, state.GetOrCreateRemoteParticipant(participantSid, this)); });

        Debug.Log($"added media track from : {participantSid}, sid {trackSid}");

        _ = UniTaskExtension.Retry(null, 10, 0.2, async () =>
        {
            await participant.AddSubscribedMediaTrack(track, trackSid);
            return;
        }, (_, error) =>
        {
            if (error is not EnumException<TrackError> trackError) return false;
            if (trackError.type == TrackError.State) return true;
            return false;
        });

    }

    void IEngineDelegate.DidRemove(Engine engine, MediaStreamTrack track)
    {
        // find the publication

        var publication = _state.Value.remoteParicipants.Values.SelectMany((e) => { return e._state.Value.tracks.Values; }).Where((e) => { return e.sid == track.Id; }).First();

        if (publication == null) return;

        publication.SetTrack(null);
    }

    void IEngineDelegate.DidReceive(Engine engine, UserPacket userPacket)
    {
        // participant could be null if data brodcated form server

        var participant = _state.Value.remoteParicipants[userPacket.ParticipantSid];

        engine.ExecuteIfConnected(() =>
        {
            Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidReceive(this, participant, userPacket.Payload);
            }, () =>
            {
                return "room.didReceive data : " + userPacket.Payload;
            });
        });

        if (participant != null)
        {
            participant.Notify((iParticipantDelegate) =>
            {
                iParticipantDelegate.DidReceive(participant, userPacket.Payload);
            }, () =>
            {
                return "participant.didReceive data : " + userPacket.Payload;
            });
        }
    }
}
// IAppStateDelegate
public partial class Room : IAppStateDelegate
{
    public void AppDidEnterBackground()
    {
        if (!_state.Value.options.suspendLocalVideoTracksInBackground) return;

        if (LocalParticipant == null) return;
        var promises = LocalParticipant.LocalVideoTracks.Select((track) => { return track.Suspend(); });

        if (promises.Count() <= 0) return;

        queue.Async(async () =>
        {
            await UniTask.WhenAll(promises);
            Debug.Log("suspended all video tracks");
        });
    }

    public void AppWillEnterForeground()
    {
        if (LocalParticipant == null) return;
        var promises = LocalParticipant.LocalVideoTracks.Select((track) => { return track.Resume(); });

        if (promises.Count() <= 0) return;

        queue.Async(async () =>
        {
            await UniTask.WhenAll(promises);
            Debug.Log("resumed all video tracks");
        });

    }
    public void AppWillTerminate()
    {
        // attempt to disconnect if already connected.
        // this is not gurated since there is no reliable way to detect app termination.
        Disconnect().Forget();
    }
}

#region Maxst Custom: for Unity

public partial class Room
{
    static bool webRTCInitialized = false;

    public void PrepareConnection()
    {
        Debug.Log($"PrepareConnection():{webRTCInitialized}");

        if (webRTCInitialized) { return; }

        PrepareAudioSource();

        WebRTC.Initialize();
        webRTCInitialized = true;
    }

    public void CleanupDisconnection()
    {
        Debug.Log($"CleanupDisconnection():{webRTCInitialized}");

        if (!webRTCInitialized) { return; }

        WebRTC.Dispose();
        webRTCInitialized = false;

        CleanupAudioSource();
    }
}

// Audio
public partial class Room
{
    AudioClip micAudioClip;
    string micDeviceName;

    public AudioSource MicAudioSource;
    public readonly Dictionary<string, AudioSource> remoteAudioSources = new();

    void PrepareAudioSource()
    {
        Debug.Log("PrepareAudioSource()");

        // local audio
        if (Microphone.devices.Length > 0)
        {
            micDeviceName = Microphone.devices[0].ToString();
            Debug.Log("deviceName " + micDeviceName);
            micAudioClip = Microphone.Start(micDeviceName, true, 1, 48000);
            while (!(Microphone.GetPosition(micDeviceName) > 0)) { }
        }

        MicAudioSource = new GameObject().AddComponent<AudioSource>();
        MicAudioSource.loop = true;
        MicAudioSource.clip = micAudioClip;
        //MicAudioSource.Play();

        // remote audio
        foreach (KeyValuePair<String, AudioSource> pair in remoteAudioSources)
        {
            pair.Value.Stop();
        }
        remoteAudioSources.Clear();
    }

    void CleanupAudioSource()
    {
        Debug.Log($"CleanupAudioSource()");

        // local audio
        Microphone.End(micDeviceName);
        micAudioClip = null;

        if (MicAudioSource != null)
        {
            MicAudioSource.Stop();
            MicAudioSource.clip = null;
        }

        MicAudioSource = null;

        // remote audio
        foreach (KeyValuePair<String, AudioSource> pair in remoteAudioSources)
        {
            pair.Value.Stop();
        }
        remoteAudioSources.Clear();
    }
}
#endregion