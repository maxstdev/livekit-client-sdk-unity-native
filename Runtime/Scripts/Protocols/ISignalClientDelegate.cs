using System;
using LiveKit.Proto;
using Unity.WebRTC;

internal interface ISignalClientDelegate
{
    bool DidMutate(SignalClient signalClient, SignalClient.State state, SignalClient.State oldState) { return false; }

    bool DidReceive(SignalClient signalClient, JoinResponse joinResponse) { return false; }
    bool DidReceiveAnswer(SignalClient signalClient, RTCSessionDescription answer) { return false; }
    bool DidReceiveOffer(SignalClient signalClient, RTCSessionDescription offer) { return false; }
    bool DidReceive(SignalClient signalClient, RTCIceCandidate iceCandidate, SignalTarget target) { return false; }
    bool DidPublish(SignalClient signalClient, TrackPublishedResponse localTrack) { return false; }
    bool DidUnpublish(SignalClient signalClient, TrackUnpublishedResponse localTrack) { return false; }
    bool DidUpdate(SignalClient signalClient, ParticipantInfo[] participants) { return false; }
    bool DidUpdate(SignalClient signalClient, LiveKit.Proto.Room room) { return false; }
    bool DidUpdate(SignalClient signalClient, SpeakerInfo[] speakers) { return false; }
    bool DidUpdate(SignalClient signalClient, ConnectionQualityInfo[] connectionQuality) { return false; }
    bool DidUpdateRemoteMute(SignalClient signalClient, string trackSid, bool muted) { return false; }
    bool DidUpdate(SignalClient signalClient, StreamStateInfo[] trackStates) { return false; }
    bool DidUpdate(SignalClient signalClient, string trackSid, SubscribedQuality[] subscribedQualities) { return false; }
    bool DidUpdate(SignalClient signalClient, SubscriptionPermissionUpdate subscriptionPermission) { return false; }
    bool DidUpdate(SignalClient signalClient, string token) { return false; }
    bool DidReceiveLeave(SignalClient signalClient, bool canReconnect) { return false; }
}