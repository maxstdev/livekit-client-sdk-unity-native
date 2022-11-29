using System.Collections.Generic;
using LiveKit.Proto;
using Unity.WebRTC;

internal interface ITransportDelegate
{
    void DidUpdate(Transport transport, RTCPeerConnectionState state);
    void DidGenerate(Transport transport, RTCIceCandidate iceCandidate);
    void DidOpen(Transport transport, RTCDataChannel dataChannel);
    void DidAdd(Transport transport, MediaStreamTrack track, MediaStream[] streams);
    //void DidRemove(Transport transport, MediaStreamTrack track); // NOTE:Thomas: Unexposing OnRemoveTrack in Unity WebRTC
    void TransportShouldNegotiate(Transport transport);
    void DidGenerate(Transport transport, List<TrackStats> stats, SignalTarget target);

    // NOTE:Thomas: It's in Unity.WebRTC, but it's not used in LiveKit.
    //void DidUpdate(Transport transport, RTCIceConnectionState state);
    //void DidUpdate(Transport transport, RTCIceGatheringState state);
    //void DidUpdate(Transport transport, RTCTrackEvent onTrack);
}