using Cysharp.Threading.Tasks;
using Dispatch;
using Google.Protobuf;
using LiveKit.Proto;
using Newtonsoft.Json;
using Support;
using System;
using System.Collections.Generic;
using System.Linq;
using UniLiveKit.ErrorException;
using Unity.WebRTC;
using UnityEngine;

using ConditionEvalFunc = System.Func<Engine.State, Engine.State?, bool>;

internal partial class Engine : MulticastDelegate<IEngineDelegate>
{
    internal readonly SerialQueue queue = new("LiveKitSDK.engine");

    public struct State : ReconnectableState
    {
        ReconnectMode? ReconnectableState.reconnectMode => reconnectMode;
        ConnectionState ReconnectableState.connectionState => connectionState;

        public State(
             ConnectOptions connectOptions,
             string url = null,
             string token = null,
             ReconnectMode? nextPreferredReconnectMode = null,
             ReconnectMode? reconnectMode = null,
             ConnectionState? connectionState = null,
             Stopwatch connectStopwatch = null,
             bool hasPublished = false
            )
        {
            this.connectOptions = connectOptions;
            this.url = url;
            this.token = token;
            this.nextPreferredReconnectMode = nextPreferredReconnectMode;
            this.reconnectMode = reconnectMode;
            this.connectionState = connectionState ?? new(ConnectionState.States.Disconnected);
            this.connectStopwatch = connectStopwatch ?? new Stopwatch("connect");
            this.hasPublished = hasPublished;
            this.primaryTransportConnectedCompleter = new();
            this.publisherTransportConnectedCompleter = new();
            this.publisherReliableDCOpenCompleter = new();
            this.publisherLossyDCOpenCompleter = new();
        }
        public ConnectOptions connectOptions;
        public string url;
        public string token;
        // preferred reconnect mode which will be used only for next attempt
        public ReconnectMode? nextPreferredReconnectMode;
        public ReconnectMode? reconnectMode;
        public ConnectionState connectionState;
        public Stopwatch connectStopwatch;
        public bool hasPublished;
        public Completer<object> primaryTransportConnectedCompleter;
        public Completer<object> publisherTransportConnectedCompleter;
        public Completer<object> publisherReliableDCOpenCompleter;
        public Completer<object> publisherLossyDCOpenCompleter;
    }

    public StateSync<State> _state;
    public SignalClient signalClient = new();

    public Transport Publisher { get; private set; } = null;
    public Transport Subscriber { get; private set; } = null;

    public WeakReference<Room> room = null;

    internal struct ConditionalExecutionEntry
    {
        internal ConditionEvalFunc executeCondition;
        internal ConditionEvalFunc removeCondition;
        internal Action block;

        internal ConditionalExecutionEntry(ConditionEvalFunc execute, ConditionEvalFunc remove, Action block)
        {
            executeCondition = execute;
            removeCondition = remove;
            this.block = block;
        }
    }

    private bool subscriberPrimary = false;
    private Transport Primary => this.subscriberPrimary ? Subscriber : Publisher;

    private RTCDataChannel dcReliablePub;
    private RTCDataChannel dcLossyPub;
    private RTCDataChannel dcReliableSub;
    private RTCDataChannel dcLossySub;

    private readonly SerialQueue _blackProcessQueue = new("LiveKitSDK.engine.pendingBlocks");

    private List<ConditionalExecutionEntry> _queueBlocks = new();

    internal Engine(ConnectOptions connectOptions) : base()
    {
        this._state = new(new State(connectOptions));

        Debug.Log($"sdk: {UniLiveKit.LiveKit.version}, os: {Utils.CurrentOS()}({Utils.OSVersionString()})");

        signalClient.AddDelegate(this);
        ConnectivityListener.shared.AddDelegate(this);

        this._state.OnMutate = (state, oldState) =>
        {
            Debug.Assert(!((state.connectionState.State == ConnectionState.States.Reconnecting) && (state.reconnectMode == null)), "ReconnectMode should not be null");

            if (!state.connectionState.Equals(oldState.connectionState) || (state.reconnectMode != oldState.reconnectMode))
            {
                Debug.Log($"Engine connectionState: {oldState.connectionState.State} -> {state.connectionState.State}, reconnectMode : {state.reconnectMode}");
            }

            Notify((iEngineDelegate) => { iEngineDelegate.DidMutate(this, state, oldState); });

            _blackProcessQueue.Async(() =>
            {
                if (_queueBlocks.Count <= 0)
                {
                    return;
                }

                Debug.Log($"[excution control] processing pending entries ({_queueBlocks.Count})...");

                _queueBlocks.RemoveAll((entry) =>
                {
                    // return and remove this entry if matches remove condition
                    if (entry.removeCondition(state, oldState)) return true;
                    // return but don't remove this entry if doesn't match execute condition
                    if (!entry.executeCondition(state, oldState)) return false;

                    Debug.Log("[excution control] condition matching block...");
                    entry.block();
                    // remove this entry
                    return true;
                });
            });
        };
    }

    ~Engine()
    {
        Debug.Log("Engine Destory");
    }

    // Connect sequence, resets existing state
    internal async UniTask Connect(
        string url,
        string token,
        ConnectOptions? connectOptions = null)
    {
        Debug.Log("Engine Connect");

        // update options if specified
        if ((connectOptions is ConnectOptions _connectOptions) && (_connectOptions == this._state.Value.connectOptions))
        {
            _state.Mutate((e) => { e.connectOptions = _connectOptions; return e; });
        }

        try
        {
            await queue.Sync<UniTask>(async () =>
            {
                await CleanUp();

                await _state.MutateAwait((state) =>
                {
                    state.connectionState.Update(ConnectionState.States.Connecting);
                    return UniTask.FromResult(state);
                });

                await FullConnectSequence(url, token);

                // connect sequence successful
                Debug.Log("Connect sequence completed");

                // update internal vars (only if connect succeeded)
                this._state.Mutate((state) =>
                {
                    state.url = url;
                    state.token = token;
                    state.connectionState.Update(ConnectionState.States.Connected);
                    return state;
                });

                return;
            });
        }
        catch (Exception e)
        {
            CleanUp(DisconnectReason.networkError.Reason(e)).Forget();
        }
    }

    // cleanUp (reset) both Room & Engine's state
    UniTask CleanUp(DisconnectReason? reson = null, bool isFullReconnect = false)
    {
        if (this.room.TryGetTarget(out Room room))
        {
            // call Room's cleanUp
            return room.CleanUp(reson, isFullReconnect);
        }
        else
        {
            // this should never happen since Engine is owned by Room
            throw new EnumException<EngineError>(EngineError.State, "Room iu nil");
        }
    }

    internal async UniTask CleanUpRTC()
    {
        await queue.Sync<UniTask>(async () =>
        {
            // TODO : - Confirm deletion.
            // guard let self = self else { return Promise(()) }

            List<RTCDataChannel> dataChannels = new List<RTCDataChannel>{
                dcReliablePub,
                dcLossyPub,
                dcReliableSub,
                dcLossySub
            }
            .Where((e) => e != null)
            .ToList();

            // can be async
            DispatchQueue.WebRTC.Async(() =>
            {
                foreach (var dataChannel in dataChannels)
                {
                    dataChannel.Close();
                }
            });

            dcReliablePub = null;
            dcLossyPub = null;
            dcReliableSub = null;
            dcLossySub = null;

            if (Publisher != null)
            {
                await Publisher.Close();
            }

            if (Subscriber != null)
            {
                await Subscriber.Close();
            }

            Publisher = null;
            Subscriber = null;
            _state.Mutate((e) => { e.hasPublished = false; return e; });
        });
    }

    internal UniTask PublisherShouldNegotiate()
    {
        return queue.Async<UniTask>(() =>
        {
            if (Publisher == null)
            {
                throw new EnumException<EngineError>(EngineError.State, "self or publisher is nil");
            }

            _state.Mutate((e) => { e.hasPublished = true; return e; });
            Publisher.negotiate();

            return UniTask.CompletedTask;
        }).Unwrap();
    }

    internal async UniTask Send(UserPacket userPacket, Reliability reliability = Reliability.Reliable)
    {
        UniTask EnsurePublisherConnected()
        {
            return UniTask.Create(async () =>
            {
                if (subscriberPrimary)
                {
                    return;
                }

                if (Publisher == null)
                {
                    throw new EnumException<EngineError>(EngineError.State, "publisher is null");
                }

                if (!Publisher.IsConnected() && Publisher.ConnectionState != RTCPeerConnectionState.Connected)
                {
                    PublisherShouldNegotiate().Forget();
                }

                var p1 = _state.MutateAwait(async (e) =>
                {
                    await e.publisherTransportConnectedCompleter.Wait(
                        this.queue,
                        TimeInterval.DefaultTransportState,
                        new EnumException<TransportError>(TransportError.TimeOut, "publisher didn't connect"));
                    return e;
                });


                var p2 = _state.MutateAwait(async (state) =>
                {
                    var completer = reliability == Reliability.Reliable ? state.publisherReliableDCOpenCompleter : state.publisherLossyDCOpenCompleter;
                    await completer.Wait(this.queue, TimeInterval.DefaultPublisherDataChannelOpen, new EnumException<TransportError>(TransportError.TimeOut, "Publisher dc didn't open"));
                    return state;
                });

                await UniTask.WhenAll(p1, p2);

                return;
            });
        }

        await EnsurePublisherConnected();

        await queue.Sync<UniTask>(async () =>
        {
            DataPacket packet = new()
            {
                Kind = reliability.toPBType(),
                User = userPacket
            };

            // TODO - Not use static func createDataBuffer(data: Data) -> RTCDataBuffer
            var rtcData = packet.ToByteArray();

            var channel = PublisherDataChannel(reliability);

            if (channel == null)
            {
                throw new EnumException<InternalError>(InternalError.State, "Data Channel is null");
            }

            try
            {
                channel.Send(rtcData);
            }
            catch (Exception e)
            {
                Debug.Log(e);
                throw new EnumException<EngineError>(EngineError.WebRTC, "DataChannel.sendData returned false");
            }

            await UniTask.CompletedTask;
        });

    }
}

// Excution control (Internal)
internal partial class Engine
{
    // TODO : func executeIfConnected(_ block: @escaping @convention(block) () -> Void)
    internal void ExecuteIfConnected(Action block)
    {
        if (_state.Value.connectionState.State == ConnectionState.States.Connected)
        {
            // execute immediately
            block();
        }
    }

    internal void Excute(ConditionEvalFunc condition, ConditionEvalFunc removeCodition, Action block)
    {
        // already matches condition, execute immediately
        if (_state.Read((e) => { return condition(e, null); }))
        {
            Debug.Log("[execution control] executing immediately...");
            block();
        }
        else
        {
            _blackProcessQueue?.Async(() =>
            {
                Debug.Log("[execution control] enqueuing entry...");

                var entry = new ConditionalExecutionEntry(condition, removeCodition, block);
                _queueBlocks.Add(entry);
            });
        }
    }
}

// private
internal partial class Engine
{
    private void AddRTCDataChannelDelegate(RTCDataChannel dataChannel)
    {
        if (dataChannel == null) return;

        dataChannel.OnClose = () =>
        {
            DelegateOnClose(dataChannel);
        };
        dataChannel.OnOpen = () =>
        {
            DelegateOnOpen(dataChannel);
        };

        dataChannel.OnMessage = (bytes) =>
        {
            DelegateOnMessage(bytes);
        };
    }

    private RTCDataChannel PublisherDataChannel(Reliability forReliability)
    {
        return forReliability == Reliability.Reliable ? dcReliablePub : dcLossyPub;
    }

    private void OnReveiced(RTCDataChannel dataChannel)
    {
        Debug.Log($"Server opened data channel {dataChannel.Label}");

        if (dataChannel.Label.Equals(RTCDataChannelLabels.reliable))
        {
            dcReliableSub = dataChannel;
            AddRTCDataChannelDelegate(dcReliableSub);
        }
        else if (dataChannel.Label.Equals(RTCDataChannelLabels.lossy))
        {
            dcLossySub = dataChannel;
            AddRTCDataChannelDelegate(dcLossySub);
        }
        else
        {
            Debug.LogWarning($"Unknown data channel label {dataChannel.Label}");
        }
    }

    private async UniTask FullConnectSequence(string url, string token)
    {
        if (this.room.TryGetTarget(out Room room))
        {
            await queue.Sync<UniTask>(async () =>
            {
                await this.signalClient.Connect(
                    url,
                    token,
                    room._state.Value.options.adaptiveStream,
                    _state.Value.connectOptions,
                    _state.Value.reconnectMode
                    );

                LiveKit.Proto.JoinResponse jr = null;

                await this.signalClient._state.MutateAwait(async (state) =>
                {
                    jr = await state.joinResponseCompleter.Wait(
                        this.queue,
                        TimeInterval.DefaultJoinResponse,
                        new EnumException<SignalClientError>(SignalClientError.TimeOut, "failed to receive join response")
                        );
                    return state;
                });

                await UniTask.WaitUntil(() => jr != null);

                this._state.Mutate((e) => { e.connectStopwatch.Split("signal"); return e; });

                await ConfigureTransports(jr);

                await signalClient.ResumeResponseQueue();

                await _state.MutateAwait(async (state) =>
                {
                    await state.primaryTransportConnectedCompleter.Wait(
                        this.queue,
                        TimeInterval.DefaultTransportState,
                        new EnumException<TransportError>(TransportError.TimeOut, "primary transport didn't connect"));
                    return state;
                });

                _state.Mutate((state) =>
                {
                    state.connectStopwatch.Split("engine");
                    Debug.Log(this._state.Value.connectStopwatch);
                    return state;
                });
            });
        }
        else
        {
            throw new EnumException<EngineError>(EngineError.State, "Room is nil");
        }
    }

    async UniTask StartReconnect()
    {
        if (_state.Value.connectionState.State != ConnectionState.States.Connected)
        {
            throw new EnumException<EngineError>(EngineError.State, "Must be called with connected state");
        }

        if (string.IsNullOrEmpty(_state.Value.url) || string.IsNullOrEmpty(_state.Value.token))
        {
            throw new EnumException<EngineError>(EngineError.State, "url or token is null");
        }

        var url = _state.Value.url;
        var token = _state.Value.token;

        if (Subscriber == null || Publisher == null)
        {
            throw new EnumException<EngineError>(EngineError.State, "Publisher or Subscriber is null");
        }

        //this should never happen since Engine is owned by Room
        async UniTask QuickReconnectSequence()
        {
            Debug.Log("[Reconnect] starting QUICK reconnect sequece...");

            bool isGetSuccess = room.TryGetTarget(out Room _room);

            if (!isGetSuccess)
            {
                throw new EnumException<EngineError>(EngineError.State, "Room is null");
            }

            await queue.Sync<UniTask>(async () =>
            {
                await signalClient.Connect(url, token, _room._state.Value.options.adaptiveStream, _state.Value.connectOptions, _state.Value.reconnectMode);
                Debug.Log("[reconnect] waiting for socket to connect...");

                // Wait for primary transport to connect(if not already)
                await _state.MutateAwait(async (e) =>
                {
                    await e.primaryTransportConnectedCompleter.Wait(this.queue, TimeInterval.DefaultTransportState, new EnumException<TransportError>(TransportError.TimeOut, "primary transport didn't connect"));
                    return e;
                });

                Subscriber.restartingIce = false;

                // only if published, contiue...
                if (Publisher == null || !_state.Value.hasPublished)
                {
                    return;
                }

                Debug.Log("[reconnect] waiting for publisher to connect...");

                await Publisher.CreateAndSendOffer(true);

                await _state.MutateAwait(async (e) =>
                {
                    await e.publisherTransportConnectedCompleter.Wait(this.queue, TimeInterval.DefaultTransportState, new EnumException<TransportError>(TransportError.TimeOut, "publisher transport didn't connect"));
                    return e;
                });

                Debug.Log("[reconnect] send queued requests");
                // always check if there are queue requests
                await signalClient.SendQueuedRequests();
            });
        }

        // "full" re-connection sequence
        // as a last resort, try to do a clean re-connection and re-publish exisiting tracks

        async UniTask FullReconnectSequece()
        {
            Debug.Log("[reconnect] starting FULL reconnect sequence...");

            await queue.Sync<UniTask>(async () =>
            {
                await CleanUp(null, true);

                if (string.IsNullOrEmpty(_state.Value.url) || string.IsNullOrEmpty(_state.Value.token))
                {
                    throw new EnumException<EngineError>(EngineError.State, "url or token is null");
                }

                var url = _state.Value.url;
                var token = _state.Value.token;

                await FullConnectSequence(url, token);
            });
        }

        //async UniTask<bool> TryReconnected(int triesLeft)
        //{
        //    // not reconnecting state anymore
        //    if (_state.Value.connectionState.State != ConnectionState.States.Reconnecting)
        //    {
        //        return false;
        //    }

        //    // full reconnect failed, give up
        //    if (_state.Value.reconnectMode == ReconnectMode.Full)
        //    {
        //        return false;
        //    }

        //    Debug.Log($"[reconnect] retry in {TimeInterval.defaultQuickReconnectRetry} second, {triesLeft} tries left...");

        //    // try full reconnect for the final attempt
        //    if (triesLeft == 1 && _state.Value.nextPreferredReconnectMode == null)
        //    {
        //        _state.Mutate((e) => { e.nextPreferredReconnectMode = ReconnectMode.Full; return e; });
        //    }

        //    return true;
        //}

        try
        {
            await UniTaskExtension.Retry(
                queue,
                3,
                TimeInterval.DefaultQuickReconnectRetry,
                async () =>
                {
                    ReconnectMode mode = _state.Mutate((e) =>
                    {
                        ReconnectMode mode = (e.nextPreferredReconnectMode == ReconnectMode.Full || e.reconnectMode == ReconnectMode.Full) ? ReconnectMode.Full : ReconnectMode.Quick;
                        e.connectionState.Update(ConnectionState.States.Reconnecting);
                        e.reconnectMode = mode;
                        e.nextPreferredReconnectMode = null;
                        return new Tuple<State, ReconnectMode>(e, mode);
                    });

                    var sequence = (mode == ReconnectMode.Full) ? FullReconnectSequece() : QuickReconnectSequence();
                    await sequence;
                    return;
                },
                (triesLeft, error) =>
                {
                    if (_state.Value.connectionState.State != ConnectionState.States.Reconnecting)
                    {
                        return false;
                    }

                    if (_state.Value.reconnectMode == ReconnectMode.Full)
                    {
                        return false;
                    }

                    Debug.Log($"[reconnect] retry in {TimeInterval.DefaultQuickReconnectRetry} seconds, {triesLeft} triesleft...");

                    if (triesLeft == 1 && _state.Value.nextPreferredReconnectMode == null)
                    {
                        _state.Mutate((e) => { e.nextPreferredReconnectMode = ReconnectMode.Full; return e; });
                    }

                    return true;
                });

            Debug.Log("[reconnect] sequence completed");
            _state.Mutate((e) => { e.connectionState.Update(ConnectionState.States.Connected); return e; });
        }
        catch (Exception error)
        {
            Debug.Log("[reconnect] sequence failed with error " + error);
        }
    }
}

// - Session Migration
internal partial class Engine
{
    internal List<DataChannelInfo> DataChannelInfo()
    {
        List<RTCDataChannel> infos = new() {
            PublisherDataChannel(Reliability.Lossy),
            PublisherDataChannel(Reliability.Reliable)
        };
        return infos.Where((e) => e != null).Select((e) => e.toLKInfoType()).ToList();
    }
}

// - SignalClientDelegate
internal partial class Engine : ISignalClientDelegate
{
    bool ISignalClientDelegate.DidMutate(SignalClient signalClient, SignalClient.State state, SignalClient.State oldState)
    {
        // connectionState did update
        if (!state.connectionState.State.Equals(oldState.connectionState.State)
            // did disconnect
            && state.connectionState.State == ConnectionState.States.Disconnected
            // only attempt re-connect if disconnected(reason: network)
            && state.connectionState.Reason == DisconnectReason.networkError
            // engine is currently connected state
            && _state.Value.connectionState.State == ConnectionState.States.Connected)
        {
            Debug.Log($"[reconnect] starting, reason: socket network error. connectionState: {_state.Value.connectionState})");
            StartReconnect().Forget();
        }

        return true;
    }

    bool ISignalClientDelegate.DidReceive(SignalClient signalClient, RTCIceCandidate iceCandidate, SignalTarget target)
    {
        var transport = target == SignalTarget.Subscriber ? Subscriber : Publisher;

        if (transport == null)
        {
            Debug.LogError($"failed to add ice candidate, transport is nil for target: ${target}");
            return true;
        }

        queue.Async(async () =>
        {
            try
            {
                await transport.AddIceCandidate(iceCandidate);
                return;
            }
            catch (Exception error)
            {
                Debug.LogError($"failed to add ice candidate for transport: {transport}, error: {error}");
            }
        });

        return true;
    }

    bool ISignalClientDelegate.DidReceiveAnswer(SignalClient signalClient, RTCSessionDescription answer)
    {
        if (Publisher == null)
        {
            Debug.LogError("publisher is null");
            return true;
        }

        queue.DispatchAsync(async () =>
        {
            try
            {
                await Publisher.SetRemoteDescription(answer);
            }
            catch (Exception error)
            {
                Debug.LogError($"failed to set remote description, error: {error}");
            }
        });

        return true;
    }

    bool ISignalClientDelegate.DidReceiveOffer(SignalClient signalClient, RTCSessionDescription offer)
    {
        Debug.Log("received offer, creating & sending answer...");

        if (Subscriber == null)
        {
            Debug.LogError("failed to send answer, subscriber is null");
            return true;
        }

        queue.DispatchAsync(async () =>
        {
            try
            {
                await Subscriber.SetRemoteDescription(offer);
                var answer = await Subscriber.CreateAnswer();
                await Subscriber.SetLocalDescription(answer);
                await this.signalClient.SendAnswer(answer);
                Debug.Log("answer sent to signal");
            }
            catch (Exception error)
            {
                Debug.LogError($"failed to send answer, error: {error}");
            }
        });

        return true;
    }

    bool ISignalClientDelegate.DidUpdate(SignalClient signalClient, string token)
    {
        _state.Mutate((state) => { state.token = token; return state; });
        return true;
    }
}

internal partial class Engine
{
    private void DataChannelDideChangeState(RTCDataChannel dataChannel)
    {
        Notify((iEngineDelegate) => { iEngineDelegate.DidUpdate(this, dataChannel, dataChannel.ReadyState); });

        Debug.Log($"DataChannelDideChangeState dataChannel.{dataChannel?.Label} : {dataChannel?.Id}");
        if (dataChannel == dcReliablePub)
        {
            _state.Value.publisherReliableDCOpenCompleter.Set(dataChannel.ReadyState == RTCDataChannelState.Open ? new() : null);
        }
        else if (dataChannel == dcLossyPub)
        {
            _state.Value.publisherLossyDCOpenCompleter.Set(dataChannel.ReadyState == RTCDataChannelState.Open ? new() : null);
        }

        Debug.Log($"dataChannel.{dataChannel.Label}, didChangeState : {dataChannel.Id}");
    }

    void DelegateOnClose(RTCDataChannel rtcDataChannel)
    {
        DataChannelDideChangeState(rtcDataChannel);
    }

    void DelegateOnOpen(RTCDataChannel rtcDataChannel)
    {
        DataChannelDideChangeState(rtcDataChannel);
    }

    void DelegateOnMessage(byte[] bytes)
    {
        MessageParser<DataPacket> parser = new(() => new DataPacket());
        DataPacket packet = parser.ParseFrom(bytes);

        switch (packet.ValueCase)
        {
            case DataPacket.ValueOneofCase.User:
                Notify((iEngineDelegate) => { iEngineDelegate.DidReceive(this, packet.User); });
                break;

            case DataPacket.ValueOneofCase.Speaker:
                Notify((iEngineDelegate) => { iEngineDelegate.DidUpdate(this, packet.Speaker.Speakers.ToArray()); });
                break;

            default:
                return;
        }
    }
}

internal partial class Engine : ITransportDelegate
{
    void ITransportDelegate.DidGenerate(Transport transport, List<TrackStats> stats, SignalTarget target)
    {
        // relay to Room
        Notify((iEngineDelegate) => { iEngineDelegate.DidGenerate(this, stats.ToArray(), target); });
    }

    void ITransportDelegate.DidUpdate(Transport transport, RTCPeerConnectionState pcState)
    {
        Debug.Log($"target: {transport.target}, state: {pcState}");

        // primary connected
        if (transport.primary)
        {
            var primary = pcState == RTCPeerConnectionState.Connected ? new object() : null;
            _state.Value.primaryTransportConnectedCompleter.Set(primary);
        }

        // publisher connected
        if (transport.target == SignalTarget.Publisher)
        {
            var primary = pcState == RTCPeerConnectionState.Connected ? new object() : null;
            _state.Value.publisherTransportConnectedCompleter.Set(primary);
        }

        if (_state.Value.connectionState.State.isConnected())
        {
            List<RTCPeerConnectionState> states = new() { RTCPeerConnectionState.Disconnected, RTCPeerConnectionState.Failed };

            // Attempt re-connect if primary or publisher transport failed
            if ((transport.primary || (_state.Value.hasPublished && transport.target == SignalTarget.Publisher)) && states.Contains(pcState))
            {
                Debug.Log("[reconnect] starting, reason: transport disconnected or failed");
                StartReconnect().Forget();
            }
        }
    }

    private async UniTask ConfigureTransports(JoinResponse joinResponse)
    {
        await queue.Sync<UniTask>(async () =>
        {
            Debug.Log("configurating transports...");

            // this should never happen since Engine is owned by Room
            if (this.room.TryGetTarget(out Room room))
            {
                if (this.Subscriber != null && this.Publisher != null)
                {
                    Debug.Log("Transports already Configured");
                    return;
                }

                // protocol v3
                this.subscriberPrimary = joinResponse.SubscriberPrimary;
                Debug.Log("subscriberPrimary :" + joinResponse.SubscriberPrimary);

                // update iceServers from joinResponse
                _state.Mutate((state) =>
                {
                    state.connectOptions.rtcConfiguration.Set(joinResponse.IceServers.ToArray());

                    var clientConfiguration = joinResponse.ClientConfiguration ?? new();

                    if (clientConfiguration.Equals(ClientConfigSetting.Enabled))
                    {
                        state.connectOptions.rtcConfiguration.iceTransportPolicy = RTCIceTransportPolicy.Relay;
                    }
                    else
                    {
                        state.connectOptions.rtcConfiguration.iceTransportPolicy = RTCIceTransportPolicy.All;
                    }
                    return state;
                });

                this.Subscriber = new Transport(
                    _state.Value.connectOptions.rtcConfiguration,
                    SignalTarget.Subscriber,
                    subscriberPrimary,
                    this,
                    room._state.Value.options.reportStats);

                this.Publisher = new Transport(
                    _state.Value.connectOptions.rtcConfiguration,
                    SignalTarget.Publisher,
                    !subscriberPrimary,
                    this,
                    room._state.Value.options.reportStats)
                {
                    onOffer = (async (offer) =>
                    {
                        Debug.Log($"publisher onOffer {offer.sdp}");
                        await signalClient.SendOffer(offer);
                        return;
                    })
                };

                // data over pub channel for backwards compatibility
                this.dcReliablePub = this.Publisher?.DataChannel(RTCDataChannelLabels.reliable, Engine.CreateDataChannelConfiguration());

                if (dcReliablePub != null)
                {
                    AddRTCDataChannelDelegate(dcReliablePub);
                }

                this.dcLossyPub = this.Publisher?.DataChannel(RTCDataChannelLabels.lossy, Engine.CreateDataChannelConfiguration(true, 0));

                if (dcLossyPub != null)
                {
                    AddRTCDataChannelDelegate(dcLossyPub);
                }

                Debug.Log($"dataChannel.{dcReliablePub?.Label} : {dcLossyPub?.Id}");
                Debug.Log($"dataChannel.{dcLossyPub?.Label} : {dcReliablePub?.Id}");

                if (!subscriberPrimary)
                {
                    // lazy negotiation for porocol v3+
                    await PublisherShouldNegotiate();
                }
            }
            else
            {
                throw (new EnumException<EngineError>(EngineError.State, "Room is nil"));
            }
        });
    }

    public void DidGenerate(Transport transport, RTCIceCandidate iceCandidate)
    {
        Debug.Log("didGenerate iceCadidate");

        queue.DispatchAsync(() =>
        {
            try
            {
                signalClient.SendCandidate(iceCandidate, transport.target).Forget();
            }
            catch (Exception error)
            {
                Debug.LogError($"Failed to send candidate, error : {error}");
            }
        });
    }

    void ITransportDelegate.DidAdd(Transport transport, MediaStreamTrack track, MediaStream[] streams)
    {
        Debug.Log("did add track");
        if (transport.target == SignalTarget.Subscriber)
        {
            // execute block when connected
            Excute((state, _) =>
            {
                // always remove this block when disconnected
                return state.connectionState.State == ConnectionState.States.Connected;
            }, (state, _) =>
            {
                return state.connectionState.State == ConnectionState.States.Disconnected;
            }, () =>
            {
                Notify((iEngineDelegate) => { iEngineDelegate.DidAdd(this, track, streams); });
            });
        }
    }

    //void ITransportDelegate.Transport(Transport transport, MediaStreamTrack didRemoveTrack)
    //{
    //    if (transport.target == SignalTarget.Subscriber)
    //    {
    //        Notify((e) => { e.Engine(this, didRemoveTrack); });
    //    }
    //}

    void ITransportDelegate.DidOpen(Transport transport, RTCDataChannel dataChannel)
    {
        Debug.Log($"Did open dataChannel label: {dataChannel.Label}");

        if (subscriberPrimary && transport.target == SignalTarget.Subscriber)
        {
            OnReveiced(dataChannel);
        }

        Debug.Log($"dataChannel..{dataChannel.Label} : {dataChannel.Id}");
    }

    void ITransportDelegate.TransportShouldNegotiate(Transport transport) { }
}

// IConnectivityListenerDelegate
internal partial class Engine : IConnectivityListenerDelegate
{
    // TODO - 인터넷 환경이 변경되었을때 동작 추가

    // func connectivityListener(_: ConnectivityListener, didSwitch path: NWPath) {
    //if (_state.Value.connectionState.State == ConnectionState.States.Connected)
    //{
    //    Debug.Log("[reconnect] setting, reason: network path changed");
    //    StartReconnect().Forget();
    //}
    //}
    void IConnectivityListenerDelegate.ConnectivityListener(
        ConnectivityListener listerner,
        bool didUpdateHasConnectivity)
    {


    }
}

// - Engine - Factory methods

// Note Unimplemented function.
/*
 * #if os(macOS)
private let h264BaselineLevel5: RTCVideoCodecInfo = {

    // this should never happen
    guard let profileLevelId = RTCH264ProfileLevelId(profile: .constrainedBaseline, level: .level5) else {
        logger.log("failed to generate profileLevelId", .error, type: Engine.self)
        fatalError("failed to generate profileLevelId")
    }

    // create a new H264 codec with new profileLevelId
    return RTCVideoCodecInfo(name: kRTCH264CodecName,
                             parameters: ["profile-level-id": profileLevelId.hexString,
                                          "level-asymmetry-allowed": "1",
                                          "packetization-mode": "1"])
}()
#endif

private extension Array where Element: RTCVideoCodecInfo {

    func rewriteCodecsIfNeeded() -> [RTCVideoCodecInfo] {
        #if os(macOS)
        // rewrite H264's profileLevelId to 42e032 only for macOS
        let codecs = map { $0.name == kRTCVideoCodecH264Name ? h264BaselineLevel5 : $0 }
        logger.log("supportedCodecs: \(codecs.map({ "\($0.name) - \($0.parameters)" }).joined(separator: ", "))", type: Engine.self)
        return codecs
        #else
        // no-op
        return self
        #endif
    }
}

private class VideoEncoderFactory: RTCDefaultVideoEncoderFactory {

    override func supportedCodecs() -> [RTCVideoCodecInfo] {
        super.supportedCodecs().rewriteCodecsIfNeeded()
    }
}

private class VideoDecoderFactory: RTCDefaultVideoDecoderFactory {

    override func supportedCodecs() -> [RTCVideoCodecInfo] {
        super.supportedCodecs().rewriteCodecsIfNeeded()
    }
}

private class VideoEncoderFactorySimulcast: RTCVideoEncoderFactorySimulcast {

    override func supportedCodecs() -> [RTCVideoCodecInfo] {
        super.supportedCodecs().rewriteCodecsIfNeeded()
    }
}
 */

// for Unity WebRTC

internal partial class Engine
{
    //static var audioDeviceModule: RTCAudioDeviceModule {
    //    factory.audioDeviceModule
    //}

    // NOTE:Thomas: Unity.WebRTC does not expose RTCMediaConstraints.
    internal static RTCPeerConnection CreatePeerConnection(RTCConfiguration configuration)
    {
        return DispatchQueue.WebRTC.Sync(() => { return new RTCPeerConnection(ref configuration); });
    }

    internal static VideoStreamTrack CreateVideoTrack(VideoCapturer videoCapturer)
    {
        return DispatchQueue.WebRTC.Sync(() =>
        {
            VideoStreamTrack videoTrack = null;

            switch (videoCapturer)
            {
                case WebCamCapturer webCamCapturer:
                    var webCamTexture = webCamCapturer.MWebCamTexture;
                    videoTrack = new VideoStreamTrack(webCamTexture);
                    break;

                case ScreenCapturer screenCapturer:
                    var screenCamera = screenCapturer.ScreenCamera;
                    var dimensions = screenCapturer.Options.Dimensions;

                    videoTrack = screenCamera.CaptureStreamTrack(dimensions.Width, dimensions.Height);
                    break;

                default:
                    Debug.LogWarning("<unknown VideoCapturer type>");
                    break;

                case null:
                    throw new ArgumentNullException(nameof(videoCapturer));
            }

            return videoTrack;
        });
    }

    internal static AudioStreamTrack CreateAudioTrack(AudioSource source)
    {
        return DispatchQueue.WebRTC.Sync(() =>
        {
            return new AudioStreamTrack(source);
        });
    }

    static RTCDataChannelInit CreateDataChannelConfiguration(bool ordered = true, int maxRetransmits = -1)
    {
        var result = DispatchQueue.WebRTC.Sync(() => { return new RTCDataChannelInit(); });
        result.ordered = ordered;
        result.maxRetransmits = maxRetransmits;
        return result;
    }

    internal static RTCIceCandidate CreateIceCandidate(string jsonString)
    {
        return DispatchQueue.WebRTC.Sync(() =>
        {
            return RTCIceCandidateExtension.FromJsonString(jsonString);
        });
    }

    internal static RTCSessionDescription CreateSessionDescription(RTCSdpType type, string sdp)
    {
        return DispatchQueue.WebRTC.Sync(() => { return new RTCSessionDescription() { type = type, sdp = sdp }; });
    }

    internal static RTCRtpEncodingParameters CreateRtpEncodingParameters(string rid = null,
                                                                       VideoEncoding? encoding = null,
                                                                       double scaleDown = 1.0,
                                                                       bool active = true)
    {
        var result = DispatchQueue.WebRTC.Sync(() => { return new RTCRtpEncodingParameters(); });

        result.active = active;
        result.rid = rid;
        result.scaleResolutionDownBy = scaleDown;

        if (encoding != null)
        {
            result.maxFramerate = (uint)encoding.Value.MaxFps;
            result.maxBitrate = (ulong)encoding.Value.MaxBitrate;
        }

        return result;
    }
}
