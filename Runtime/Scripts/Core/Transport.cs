using Cysharp.Threading.Tasks;
using Dispatch;
using LiveKit.Proto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UniLiveKit.ErrorException;
using Unity.WebRTC;
using UnityEditor;
using UnityEngine;
using DebouncFunc = System.Action;

internal partial class Transport : MulticastDelegate<ITransportDelegate>
{
    private readonly SerialQueue queue = new("LiveKitSDK.transport");

    // Public

    public SignalTarget target;
    public bool primary;

    public bool restartingIce = false;
    public Func<RTCSessionDescription, UniTask> onOffer;

    public RTCPeerConnectionState ConnectionState => DispatchQueue.WebRTC.Sync(() => { return pc.ConnectionState; });
    public RTCSessionDescription LocalDescription => DispatchQueue.WebRTC.Sync(() => { return pc.LocalDescription; });
    public RTCSessionDescription? RemoteDescription => DispatchQueue.WebRTC.Sync(() => { return pc?.RemoteDescription; });
    public RTCSignalingState SignalingState => DispatchQueue.WebRTC.Sync(() => { return pc.SignalingState; });

    public bool IsConnected()
    {
        return ConnectionState == RTCPeerConnectionState.Connected;
    }

    // create debounce func
    public DebouncFunc negotiate;

    // private
    private bool renegoriate = false;

    // forbid direct access to PeerConnectin
    private readonly RTCPeerConnection pc;
    private List<RTCIceCandidate> pendingCandidates = new();

    // used for stats timer
    private readonly DispatchQueueTimer statsTimer = new(1, DispatchQueue.WebRTC);
    private readonly Dictionary<string, TrackStats> stats = new();

    // keep reference to cancel later
    private CancellationTokenSource debounceWorkItem;

    internal Transport(RTCConfiguration config, SignalTarget target, bool primary, ITransportDelegate iTransportDelegate, bool reportStats = false)
    {
        negotiate = Utils.CreateDebounceFunc(queue, 100, (workItem) =>
        {
            this.debounceWorkItem = workItem;
        }, async () =>
        {
            await this.CreateAndSendOffer();
        });

        // try create peerConnection
        var pc = Engine.CreatePeerConnection(config);
        if (pc == null)
        {
            throw new EnumException<EngineError>(EngineError.WebRTC, "failed to create peerConnection");
        }

        this.target = target;
        this.primary = primary;
        this.pc = pc;

        AddDelegate(iTransportDelegate);

        DispatchQueue.WebRTC.Async(() =>
        {
            AddDelegate(pc);
        });

        statsTimer.handler = () =>
        {
            OnStatsTimer();
        };

        Set(reportStats);
    }

    ~Transport()
    {
        statsTimer.Suspend();
        Debug.Log("Transport Destruct");
    }

    internal void Set(bool reportStats)
    {
        Debug.Log($"reportStats: {reportStats}");
        if (reportStats)
        {
            statsTimer.Resume();
        }
        else
        {
            statsTimer.Suspend();
        }
    }

    internal UniTask AddIceCandidate(RTCIceCandidate candidate)
    {
        if (RemoteDescription != null && !restartingIce)
        {
            return AddIceCandidatePromise(candidate);
        }

        this.pendingCandidates.Add(candidate);
        return UniTask.CompletedTask;
    }

    internal async UniTask SetRemoteDescription(RTCSessionDescription sd)
    {
        await queue.Sync(async () =>
        {
            await SetRemoteDescriptionPromise(sd);
            var tasks = pendingCandidates.Select(candidate => AddIceCandidatePromise(candidate));

            await UniTask.WhenAll(tasks);

            pendingCandidates = new();
            restartingIce = false;

            if (renegoriate)
            {
                renegoriate = false;
                await CreateAndSendOffer();
                return;
            }

            return;
        });
    }

    internal async UniTask CreateAndSendOffer(bool iceRestart = false)
    {
        if (onOffer == null)
        {
            Debug.LogWarning("onOffer is null");
            return;
        }

        Dictionary<string, string> constrainsts = new();
        if (iceRestart)
        {
            Debug.Log("Restarting ICE...");
            //constrainsts[kRTCMediaConstraintsIceRestart] = kRTCMediaConstraintsValuetrue
            restartingIce = true;
        }

        if (SignalingState == RTCSignalingState.HaveLocalOffer && !(iceRestart && RemoteDescription != null))
        {
            renegoriate = true;
            return;
        }

        if (SignalingState == RTCSignalingState.HaveLocalOffer && iceRestart && RemoteDescription is RTCSessionDescription sd)
        {
            await queue.Sync(async () =>
            {
                await SetRemoteDescriptionPromise(sd);
                await NegotiateSequence();
                return;
            });
        }

        async UniTask NegotiateSequence()
        {
            await queue.Sync(async () =>
            {
                var offer = await CreateOffer(constrainsts);
                var localOffer = await SetLocalDescription(offer);
                await onOffer(localOffer);
            });
        }

        await NegotiateSequence();
        return;
    }

    internal UniTask Close()
    {
        return queue.Sync(() =>
        {
            // prevent debounced negoriate firing
            debounceWorkItem?.Cancel();
            statsTimer.Suspend();

            // can be async
            return DispatchQueue.WebRTC.Async(() =>
            {
                // Stop listening to delegate
                RemoveAllDelegate(pc);
                // Remove all senderss(if any)
                foreach (var sender in pc.GetSenders())
                {
                    pc.RemoveTrack(sender);
                }
                pc.Close();

                return UniTask.CompletedTask;
            }).Unwrap();
        });
    }
}

// Stats
internal partial class Transport
{
    async void OnStatsTimer()
    {
        statsTimer.Suspend();
        statsTimer.Resume();

        var report = pc.GetStats();

        await UniTask.WaitUntil(() => report.Value != null);

        var entry = report.Value.Stats.Values.Where((k) => k.Dict.ContainsKey(TrackStats.KeyTypeSSRC)).ToDictionary((item) => item.Id);

        List<TrackStats> tracks = new();

        if (entry.Count > 0)
        {
            foreach (var e in entry.Values)
            {
                TrackStats? findPrevious = null;

                if (e.Dict.TryGetValue(TrackStats.KeyTypeSSRC, out var value))
                {
                    var ssrc = value.ToString();
                    if (stats.TryGetValue(ssrc, out var previous))
                    {
                        findPrevious = previous;
                    }
                }

                if (TrackStats.From(e.Dict, findPrevious) is TrackStats newTrackStats)
                {
                    tracks.Add(newTrackStats);
                }
            }
        }

        foreach (var track in tracks)
        {
            stats[track.Ssrc] = track;
        }

        if (tracks.Count() > 0)
        {
            Notify((iTransportDelegate) =>
            {
                iTransportDelegate.DidGenerate(this, tracks, this.target);
            });
        }

        //    clean up
        //     for key in self.stats.keys {
        //        if !tracks.contains(where: { $0.ssrc == key }) {
        //        self.stats.removeValue(forKey: key)
        //        }
        //}
    }
}

// RTCPeerConnectionDelegate
internal partial class Transport
{
    private void AddDelegate(RTCPeerConnection peerConnection)
    {
        peerConnection.OnConnectionStateChange = (state) =>
        {
            Debug.Log($"Transport OnConnectionStateChange : did update state {state} for {target}");
            Notify((iTransportDelegate) =>
            {
                iTransportDelegate.DidUpdate(this, state);
            });
        };

        peerConnection.OnDataChannel = (dataChannel) =>
        {
            Debug.Log($"Transport OnDataChannel : dataChannel {JsonConvert.SerializeObject(dataChannel)} for {target}");

            Notify((iTransportDelegate) =>
            {
                iTransportDelegate.DidOpen(this, dataChannel);
            });
        };

        peerConnection.OnIceCandidate = (iceCandidate) =>
        {
            Debug.Log($"Transport OnIceCandidate : iceCandidate {JsonConvert.SerializeObject(iceCandidate)} for {target}");
            Notify((iTransportDelegate) =>
            {
                iTransportDelegate.DidGenerate(this, iceCandidate);
            });
        };

        //peerConnection.OnIceConnectionChange = (iceConnectionState) =>
        //{
        //    Debug.Log($"Transport OnIceConnectionChange : iceConnectionState {iceConnectionState} for {target}");
        //    Notify((iTransportDelegate) =>
        //    {
        //        iTransportDelegate.DidUpdate(this, iceConnectionState);
        //    });
        //};

        //peerConnection.OnIceGatheringStateChange = (iceGatheringState) =>
        //{
        //    Debug.Log($"Transport OnIceGatheringStateChange : iceGatheringState {iceGatheringState} for {target}");

        //    Notify((iTransportDelegate) =>
        //    {
        //        iTransportDelegate.DidUpdate(this, iceGatheringState);
        //    });
        //};

        peerConnection.OnNegotiationNeeded = () =>
        {
            Debug.Log($"Transport OnNegotiationNeeded : target {target}");

            Notify((iTransportDelegate) =>
            {
                iTransportDelegate.TransportShouldNegotiate(this);
            });
        };

        peerConnection.OnTrack = (trackEvent) =>
        {
            Debug.Log($"Transport OnTrack : target {target}");

            Notify((iTransportDelegate) =>
            {
                iTransportDelegate.DidAdd(this, track: trackEvent.Track, streams: trackEvent.Streams.ToArray());
            });
        };
    }
}

// Private
internal partial class Transport
{

    private UniTask<RTCSessionDescription> CreateOffer(Dictionary<string, string> constraints)
    {
        return DispatchQueue.WebRTC.Sync<UniTask<RTCSessionDescription>>(async () =>
        {
            try
            {
                var sd = pc.CreateOffer();
                await UniTask.WaitUntil(() => { return !string.IsNullOrEmpty(sd.Desc.sdp); });

                Debug.Log($"Transport CreateOffer Session SDP - {sd.Desc.sdp}");

                return sd.Desc;
            }
            catch (Exception error)
            {
                throw new EnumException<EngineError>(EngineError.WebRTC, "Failed to create offer", error);
            }
        });
    }

    private UniTask SetRemoteDescriptionPromise(RTCSessionDescription sd)
    {
        return DispatchQueue.WebRTC.Sync<UniTask<RTCSessionDescription>>(async () =>
        {
            try
            {
                await pc.SetRemoteDescription(ref sd);
                return sd;
            }
            catch (Exception error)
            {
                throw new EnumException<EngineError>(EngineError.WebRTC, "failed to set remote description", error);
            }
        });
    }

    private UniTask AddIceCandidatePromise(RTCIceCandidate candidate)
    {
        return DispatchQueue.WebRTC.Sync<UniTask>(async () =>
        {
            try
            {
                pc.AddIceCandidate(candidate);
                await UniTask.CompletedTask;
                return;
            }
            catch (Exception error)
            {
                throw new EnumException<EngineError>(EngineError.WebRTC, "failed to add ice candidate", error);
            }
        });
    }

    private void RemoveAllDelegate(RTCPeerConnection peerConnection)
    {
        peerConnection.OnConnectionStateChange = null;
        peerConnection.OnDataChannel = null;
        peerConnection.OnIceCandidate = null;
        //peerConnection.OnIceConnectionChange = null;
        //peerConnection.OnIceGatheringStateChange = null;
        peerConnection.OnNegotiationNeeded = null;
        peerConnection.OnTrack = null;
    }
}

// Internal
internal partial class Transport
{
    internal UniTask<RTCSessionDescription> CreateAnswer()
    {
        return DispatchQueue.WebRTC.Sync<UniTask<RTCSessionDescription>>(async () =>
        {
            try
            {
                var sd = pc.CreateAnswer();
                await UniTask.WaitUntil(() => { return !string.IsNullOrEmpty(sd.Desc.sdp); });

                Debug.Log($"Transport CreateAnswer Session SDP - {sd.Desc.sdp}");

                return sd.Desc;
            }
            catch (Exception error)
            {
                throw new EnumException<EngineError>(EngineError.WebRTC, "failed to create answer", error);
            }
        });
    }
    internal UniTask<RTCSessionDescription> SetLocalDescription(RTCSessionDescription sd)
    {
        return DispatchQueue.WebRTC.Sync<UniTask<RTCSessionDescription>>(async () =>
        {
            try
            {
                Debug.Log($"Transport SetLocalDescription Session SDP - {sd.sdp}");
                await pc.SetLocalDescription(ref sd);
                return sd;
            }
            catch (Exception error)
            {
                throw new EnumException<EngineError>(EngineError.WebRTC, "failed to set local description", error);
            }
        });
    }

    internal UniTask<RTCRtpTransceiver> AddTransceiver(MediaStreamTrack track, RTCRtpTransceiverInit transceiverInit)
    {
        return DispatchQueue.WebRTC.Sync<UniTask<RTCRtpTransceiver>>(async () =>
        {
            try
            {
                var transceiver = pc.AddTransceiver(track, transceiverInit);
                await UniTask.CompletedTask;
                return transceiver;
            }
            catch
            {
                throw new EnumException<EngineError>(EngineError.WebRTC, "failed to add transceiver");
            }
        });
    }

    internal UniTask RemoveTrack(RTCRtpSender sender)
    {
        return DispatchQueue.WebRTC.Sync(async () =>
        {
            var errorType = pc.RemoveTrack(sender);

            if (errorType == RTCErrorType.None)
            {
                return UniTask.CompletedTask;
            }

            throw new EnumException<EngineError>(EngineError.WebRTC, "failed to remove track");

        }).Unwrap();
    }

    // TODO RCTDataChannelDelegate?
    internal RTCDataChannel DataChannel(string forLabel, RTCDataChannelInit configuration)

    {
        return DispatchQueue.WebRTC.Sync<RTCDataChannel>(() =>
        {
            return pc.CreateDataChannel(forLabel, configuration);
        });
    }
}