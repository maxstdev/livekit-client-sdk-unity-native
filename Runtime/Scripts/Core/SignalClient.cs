using Cysharp.Threading.Tasks;
using Dispatch;
using Google.Protobuf;
using LiveKit.Proto;
using System;
using System.Collections.Generic;
using System.Linq;
using UniLiveKit.ErrorException;
using Unity.WebRTC;
using UnityEditor;
using UnityEngine;
using WebSocketSharp;

using Sid = System.String;

internal partial class SignalClient : MulticastDelegate<ISignalClientDelegate>
{
    private readonly SerialQueue queue = new("LiveKitSDK.signalClient");

    // Public
    public ConnectionState ConnectionState => _state.Value.connectionState;

    // Internal
    internal struct State : ReconnectableState
    {
        ReconnectMode? ReconnectableState.reconnectMode => reconnectMode;
        ConnectionState ReconnectableState.connectionState => connectionState;

        internal ReconnectMode? reconnectMode;
        internal ConnectionState connectionState;
        internal Completer<JoinResponse> joinResponseCompleter;
        internal Dictionary<string, Completer<TrackInfo>> completrsForAddTrack;

        internal State(ReconnectMode? mode)
        {
            reconnectMode = mode;
            connectionState = new(ConnectionState.States.Disconnected);
            joinResponseCompleter = new();
            completrsForAddTrack = new();
        }
    }

    internal readonly StateSync<State> _state = new StateSync<State>(new State(null));

    // Private
    private enum QueueState
    {
        Resumed,
        Suspended
    }

    // queue to store request while reconnecting
    private List<SignalRequest> requestQueue = new();
    private List<SignalResponse> responseQueue = new();

    private readonly SerialQueue requestDispatchQueue = new("LiveKitSDK.signalClient.requestQueue");
    private readonly SerialQueue responseDispatchQueue = new("LiveKitSDK.signalClient.responseQueue");

    private QueueState responseQueueState = QueueState.Resumed;

    private UniTaskWebSocket webSocket;
    private JoinResponse latestJoinResponse;

    internal SignalClient() : base()
    {
        // trigger events when state mutates
        _state.OnMutate = (state, oldState) =>
        {
            if (oldState.connectionState.State != state.connectionState.State)
            {
                Debug.Log($"SignalClient State : {oldState.connectionState.State} -> {state.connectionState.State}");
            }

            Notify((iSignalClientDelegate) => { iSignalClientDelegate.DidMutate(this, state, oldState); });
        };
    }

    ~SignalClient()
    {
        Debug.Log("deinit");
    }

    internal async UniTask Connect(
        string url,
        string token,
        bool adptiveStream,
        ConnectOptions? connectOptions = null,
        ReconnectMode? reconnectMode = null
        )
    {
        CleanUp();

        if (reconnectMode != null)
        {
            Debug.Log("reconnectMode : " + reconnectMode.ToString());
        }

        await queue.Sync(async () =>
        {
            Uri uri;
            try
            {
                uri = Utils.BuildUrl(url, token, adptiveStream, connectOptions, reconnectMode);
            }
            catch (Exception error)
            {
                Debug.LogError("Failed to parse rtc url " + error);
                return;
            };

            //Debug.Log("Connecting with url : " + url);
            Debug.Log("Connecting with uri : " + uri);

            try
            {
                _state.Mutate((state) =>
                {
                    state.reconnectMode = reconnectMode;
                    state.connectionState.Update(ConnectionState.States.Connecting);
                    return state;
                });

                webSocket = await UniTaskWebSocket.Connect(
                    uri,
                    OnWebSocketMessage,
                    (reason) =>
                    {
                        webSocket = null;
                        CleanUp(reason);
                    });

                _state.Mutate((state) => { state.connectionState.Update(ConnectionState.States.Connected); return state; });
            }
            catch (Exception error)
            {
                try
                {
                    // skip validation if reconnect mode
                    if (reconnectMode != null) throw error;

                    // Catch first, then throw again after getting validation response
                    // Re-build url with validate mode

                    var newUri = Utils.BuildUrl(url, token, adptiveStream, connectOptions, null, true);
                    Debug.Log("Validating with url : " + newUri);
                    var str = await new HTTP().Get(queue, newUri);

                    Debug.Log("validate response : " + str);
                    // re-throw with validation response
                    throw new EnumException<SignalClientError>(SignalClientError.Connect, str);
                }
                catch (Exception innerError)
                {
                    CleanUp(DisconnectReason.networkError.Reason(innerError));
                }
            }
        });
    }

    internal void CleanUp(DisconnectReason? reason = null)
    {
        Debug.Log("SignalClient CleanUp - reason: " + reason?.Reason());

        _state.Mutate((state) => { state.connectionState.Update(ConnectionState.States.Disconnected, reason); return state; });

        if (webSocket != null)
        {
            webSocket.CleanUp(reason);
            webSocket = null;
        }

        latestJoinResponse = null;

        _state.Mutate((state) =>
        {
            foreach (var completer in state.completrsForAddTrack.Values)
            {
                completer.Reset();
            }

            state.joinResponseCompleter.Reset();

            // reset state
            state = new(null);
            return state;
        });

        requestDispatchQueue.Async(() =>
        {
            requestQueue = new();
        });

        responseDispatchQueue.Async(() =>
        {
            responseQueue = new();
            responseQueueState = QueueState.Resumed;
        });
    }

    internal void CompleteCompleter(string AddTrackRequestTrackCid, TrackInfo trackInfo)
    {
        var trackCid = AddTrackRequestTrackCid;
        _state.Mutate((state) =>
        {
            if (!state.completrsForAddTrack.TryGetValue(trackCid, out var completer)) return state;
            completer.Set(trackInfo);
            return state;
        });
    }

    internal async UniTask<TrackInfo> PrepareCompleter(string AddTrackRequestTrackCid)
    {
        var trackCid = AddTrackRequestTrackCid;

        return await _state.MutateAwait<TrackInfo>(async (state) =>
        {
            if (state.completrsForAddTrack.Keys.Contains(trackCid))
            {
                // reset if already existst
                state.completrsForAddTrack[trackCid].Reset();
            }
            else
            {
                state.completrsForAddTrack[trackCid] = new();
            }

            var trackInfo = await state.completrsForAddTrack[trackCid].Wait(
                this.queue,
                TimeInterval.DefaultPublish,
                new EnumException<EngineError>(EngineError.TimeOut, "server didn't respond to addTrack request")
                );

            return new(state, trackInfo);
        });
    }
}


// Private
internal partial class SignalClient
{
    // send request or enqueue while reconnecting
    private UniTask SendRequest(SignalRequest request, bool enqueueIfReconnecting = true)
    {
        //Debug.Log("SendRequest() : " + JsonConvert.SerializeObject(request));

        return requestDispatchQueue.Sync(async () =>
        {
            //Debug.Log("requestDispatchQueue.Async() : " + JsonConvert.SerializeObject(request));

            if (!(_state.Value.connectionState.State.isReconnecting() && request.CanEnqueue() && enqueueIfReconnecting)) { }
            else
            {
                Debug.Log("queuing request while reconnecting, request: " + request);
                requestQueue.Add(request);
                return UniTask.CompletedTask;
            }

            if (ConnectionState.State == ConnectionState.States.Connected) { }
            else
            {
                Debug.LogError("not connected");
                throw new EnumException<SignalClientError>(SignalClientError.State, "Not connected");
            }

            // this shouldn't happen
            if (webSocket is not null) { }
            else
            {
                Debug.LogError("WebSocket is null");
                throw new EnumException<SignalClientError>(SignalClientError.State, "WebSocket is null");
            }

            var data = request.ToByteArray();
            if (data is not null && data.Count() > 0) { }
            else
            {
                Debug.LogError("could not serialize data");
                throw new EnumException<InternalError>(InternalError.Convert, "Could not serialize data");
            }

            Debug.Log("WebSocket Send: " + request);
            await UniTask.CompletedTask;

            return webSocket.Send(data);

        }).Unwrap();
    }

    private void OnWebSocketMessage(object sender, MessageEventArgs e)
    {
        SignalResponse response = null;

        if (e.IsBinary)
        {
            response = SignalResponse.Parser.ParseFrom(e.RawData);
        }
        else if (e.IsText)
        {
            response = SignalResponse.Parser.ParseJson(e.Data);
        }
        Debug.Log("WebSocket Received: " + response);

        if (response == null)
        {
            Debug.Log("Failed to decode SignalResponse");
            return;
        }

        responseDispatchQueue.Async(() =>
        {
            if (responseQueueState == QueueState.Suspended)
            {
                //Debug.Log("Enqueueing response: " + JsonConvert.SerializeObject(response));
                Debug.Log("Enqueueing response: " + response.MessageCase);
                responseQueue.Add(response);
            }
            else
            {
                OnSignalResponse(response);
            }
        });
    }

    private void OnSignalResponse(SignalResponse response)
    {
        if (ConnectionState.State != ConnectionState.States.Connected)
        {
            Debug.LogWarning("Not connected, currentState : " + ConnectionState.State);
            return;
        }

        switch (response.MessageCase)
        {
            case SignalResponse.MessageOneofCase.Join:

                var joinResponse = response.Join;
                responseQueueState = QueueState.Suspended;
                latestJoinResponse = response.Join;

                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidReceive(this, joinResponse); ;
                });

                _state.Value.joinResponseCompleter.Set(joinResponse);
                break;

            case SignalResponse.MessageOneofCase.Answer:
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidReceiveAnswer(this, response.Answer.toRTCType());
                });
                break;

            case SignalResponse.MessageOneofCase.Offer:
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidReceiveOffer(this, response.Offer.toRTCType());
                });
                break;

            case SignalResponse.MessageOneofCase.Trickle:
                var rtcCandidate = Engine.CreateIceCandidate(response.Trickle.CandidateInit);
                if (rtcCandidate == null) break;
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidReceive(this, rtcCandidate, response.Trickle.Target);
                });
                break;

            case SignalResponse.MessageOneofCase.Update:
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidUpdate(this, response.Update.Participants.ToArray());
                });
                break;

            case SignalResponse.MessageOneofCase.RoomUpdate:
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidUpdate(this, response.RoomUpdate.Room);
                });
                break;

            case SignalResponse.MessageOneofCase.TrackPublished:
                var trackPublished = response.TrackPublished;
                // not required to be handled because we use completer pattern for this case
                Notify((iSignalClientDelegate) =>
                {
                    return iSignalClientDelegate.DidPublish(this, trackPublished);
                }, 
                false);

                Debug.Log("[publish] resolving completer for cid: " + trackPublished.Cid);
                // complete
                CompleteCompleter(trackPublished.Cid, trackPublished.Track);
                break;

            case SignalResponse.MessageOneofCase.TrackUnpublished:
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidUnpublish(this, response.TrackUnpublished);
                });
                break;

            case SignalResponse.MessageOneofCase.SpeakersChanged:
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidUpdate(this, response.SpeakersChanged.Speakers.ToArray());
                });
                break;

            case SignalResponse.MessageOneofCase.ConnectionQuality:
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidUpdate(this, response.ConnectionQuality.Updates.ToArray());
                });
                break;

            case SignalResponse.MessageOneofCase.Mute:
                var mute = response.Mute;
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidUpdateRemoteMute(this, mute.Sid, mute.Muted);
                });
                break;

            case SignalResponse.MessageOneofCase.Leave:
                var leave = response.Leave;
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidReceiveLeave(this, leave.CanReconnect);
                });
                break;

            case SignalResponse.MessageOneofCase.StreamStateUpdate:
                var state = response.StreamStateUpdate;
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidUpdate(this, state.StreamStates.ToArray());
                });
                break;

            case SignalResponse.MessageOneofCase.SubscribedQualityUpdate:
                var update = response.SubscribedQualityUpdate;
                // ignore 0.15.1
                if (latestJoinResponse?.ServerVersion == "0.15.1")
                {
                    break;
                }

                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidUpdate(this, update.TrackSid, update.SubscribedQualities.ToArray());
                });
                break;

            case SignalResponse.MessageOneofCase.SubscriptionPermissionUpdate:
                var permissionUpdate = response.SubscriptionPermissionUpdate;
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidUpdate(this, permissionUpdate);
                });
                break;

            case SignalResponse.MessageOneofCase.RefreshToken:
                var token = response.RefreshToken;
                Notify((iSignalClientDelegate) =>
                {
                    iSignalClientDelegate.DidUpdate(this, token);
                });
                break;

            case SignalResponse.MessageOneofCase.Pong:
                Debug.Log("pong : " + response.Pong);
                break;
        }
    }
}

// Internal
internal partial class SignalClient
{
    internal UniTask ResumeResponseQueue()
    {
        Debug.Log("ResumeResponseQueue");

        return responseDispatchQueue.Sync<UniTask>(async () =>
        {
            try
            {
                // quickly return if no queued requests
                if (responseQueue.Count <= 0)
                {
                    Debug.Log("No queued response");
                    return;
                }

                var maxCount = responseQueue.Count();
                foreach (var response in responseQueue.ToList())
                {
                    queue.Sync(() =>
                    {
                        OnSignalResponse(response);
                        Debug.Log($"ResumeResponseQueue response: {response.MessageCase}");
                        maxCount -= 1;
                    });
                }

                await UniTask.WaitUntil(() => maxCount == 0);

                // clear the queue
                responseQueue.Clear();

                return;
            }
            finally
            {
                responseQueueState = QueueState.Resumed;
            }
        });
    }
}

// Send method
internal partial class SignalClient
{
    internal UniTask SendQueuedRequests()
    {
        // create a promise that never throws so the send sequence can continue
        async UniTask safeSend(SignalRequest request)
        {
            try
            {
                await SendRequest(request, false);
            }
            catch (Exception error)
            {
                Debug.Log($"Failed to send queued request, request {request} {error}");
            }
        }

        return requestDispatchQueue.Sync<UniTask>(async () =>
        {
            // quickly return if no queued requests
            if (requestQueue.Count() <= 0)
            {
                Debug.Log("No queued requests");
                return;
            }

            // send requests in sequential order

            await queue.Sync(async () =>
            {
                foreach (var request in requestQueue)
                {
                    await safeSend(request);
                }
            });

            // clear the queue
            requestQueue = new();

            return;
        });
    }

    internal UniTask SendOffer(RTCSessionDescription offer)
    {
        Debug.Log("SendOffer()");

        var request = new SignalRequest
        {
            Offer = offer.toPBType()
        };

        return SendRequest(request);
    }

    internal UniTask SendAnswer(RTCSessionDescription answer)
    {
        Debug.Log("SignalClient SendAnswer");

        var request = new SignalRequest
        {
            Answer = answer.toPBType()
        };

        return SendRequest(request);
    }

    internal async UniTask SendCandidate(RTCIceCandidate candidate, SignalTarget target)
    {
        Debug.Log("target : " + target);

        TrickleRequest trickle = new()
        {
            Target = target,
            CandidateInit = candidate.toLKType().toJsonString()
        };

        SignalRequest request = new()
        {
            Trickle = trickle
        };

        await SendRequest(request);
    }

    internal UniTask SendMuteTrack(string trackSid, bool muted)
    {
        Debug.Log($"trackSid : {trackSid}, muted : {muted}");

        MuteTrackRequest mute = new()
        {
            Sid = trackSid,
            Muted = muted
        };

        SignalRequest request = new()
        {
            Mute = mute
        };

        return SendRequest(request);
    }

    internal async UniTask<Tuple<R, TrackInfo>> SendAddTrack<R>(
        string cid,
        string name,
        TrackType type,
        TrackSource source,
        Func<AddTrackRequest, Tuple<AddTrackRequest, R>> populator)
    {
        Debug.Log("SendAddTrack");

        try
        {
            AddTrackRequest addTrackRequest = new()
            {
                Cid = cid,
                Name = name,
                Type = type,
                Source = source
            };

            var populateResult = populator(addTrackRequest);

            SignalRequest request = new()
            {
                AddTrack = populateResult.Item1
            };

            var completer = PrepareCompleter(cid);

            await SendRequest(request);

            return await queue.Sync<UniTask<Tuple<R, TrackInfo>>>(async () =>
            {
                var trackInfo = await completer;
                return new(populateResult.Item2, trackInfo);
            });
        }
        catch (Exception error)
        {
            // the populator block throwed
            throw error;
        }
    }


    internal UniTask SendUpdateTrackSettings(Sid sid, TrackSettings settings)
    {
        Debug.Log($"sending track settings... sid: {sid}, settings: {settings}");

        UpdateTrackSettings trackSetting = new()
        {
            Disabled = !settings.enabled,
            Width = (uint)settings.dimensions.Width,
            Height = (uint)settings.dimensions.Height,
            Quality = settings.videoQuality.toPBType()
        };
        trackSetting.TrackSids.Add(sid);

        SignalRequest request = new()
        {
            TrackSetting = trackSetting
        };

        return SendRequest(request);
    }

    internal UniTask SendUpdateVideoLayers(Sid trackSid, VideoLayer[] layers)
    {
        UpdateVideoLayers updateLayers = new()
        {
            TrackSid = trackSid,
        };

        updateLayers.Layers.Concat(layers);

        SignalRequest request = new()
        {
            UpdateLayers = updateLayers
        };

        return SendRequest(request);
    }

    internal UniTask SendUpdateSubscription(Sid participantSid,
                                                  string trackSid,
                                                  bool subscribed)
    {
        Debug.Log("SendUpdateSubscription");

        ParticipantTracks p = new()
        {
            ParticipantSid = participantSid,
        };
        p.TrackSids.Add(trackSid);

        UpdateSubscription subscription = new()
        {
            Subscribe = subscribed
        };
        subscription.TrackSids.Add(trackSid);
        subscription.ParticipantTracks.Add(p);

        SignalRequest request = new()
        {
            Subscription = subscription
        };

        return SendRequest(request);
    }

    internal UniTask SendUpdateSubscriptionPermission(bool allParticipants, ParticipantTrackPermission[] trackPermissions)
    {
        Debug.Log("SendUpdateSubscriptionPermission");

        SubscriptionPermission subscriptionPermission = new()
        {
            AllParticipants = allParticipants
        };

        subscriptionPermission.TrackPermissions.Add(trackPermissions.Select(track => track.ToPBType()));

        SignalRequest request = new()
        {
            SubscriptionPermission = subscriptionPermission
        };

        return SendRequest(request);
    }

    internal UniTask SendSyncState(SessionDescription answer,
                       UpdateSubscription subscription,
                       List<TrackPublishedResponse> publishTracks = null,
                       List<DataChannelInfo> dataChannels = null)
    {
        Debug.Log("SendSyncState");

        SyncState syncState = new()
        {
            Answer = answer,
            Subscription = subscription,
        };

        if (publishTracks != null)
        {
            syncState.PublishTracks.Add(publishTracks);
        }

        if (dataChannels != null)
        {
            syncState.DataChannels.Add(dataChannels);
        }

        SignalRequest request = new()
        {
            SyncState = syncState
        };

        return SendRequest(request);
    }

    internal UniTask SendLeave()
    {
        Debug.Log("SendLeave");


        SignalRequest request = new()
        {
            Leave = new()
        };

        return SendRequest(request);
    }

    internal void SendSimulate(SimulateScenario secenario, int? secs = null)
    {
        var simulate = new LiveKit.Proto.SimulateScenario();
        switch (secenario)
        {
            case SimulateScenario.NodeFailure:
                simulate.NodeFailure = true;
                break;
            case SimulateScenario.Migration:
                simulate.Migration = true;
                break;
            case SimulateScenario.ServerLeave:
                simulate.ServerLeave = true;
                break;
            case SimulateScenario.SpeakerUpdate:
                simulate.SpeakerUpdate = secs ?? 0;
                break;
        }
    }
}

internal static class SignalRequestExtension
{
    internal static bool CanEnqueue(this SignalRequest request)
    {
        return request.MessageCase switch
        {
            SignalRequest.MessageOneofCase.SyncState => false,
            SignalRequest.MessageOneofCase.Trickle => false,
            SignalRequest.MessageOneofCase.Offer => false,
            SignalRequest.MessageOneofCase.Answer => false,
            SignalRequest.MessageOneofCase.Simulate => false,
            SignalRequest.MessageOneofCase.Leave => false,
            _ => true,
        };
    }
}

