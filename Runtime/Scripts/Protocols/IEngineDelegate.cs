using LiveKit.Proto;
using Unity.WebRTC;

internal interface IEngineDelegate
{
    internal void DidMutate(Engine engine, Engine.State state, Engine.State oldState);
    internal void DidUpdate(Engine engine, SpeakerInfo[] speakers);
    internal void DidAdd(Engine engine, MediaStreamTrack track, MediaStream[] streams);
    internal void DidRemove(Engine engine, MediaStreamTrack track);
    internal void DidReceive(Engine engine, UserPacket userPacket);
    internal void DidUpdate(Engine engine, RTCDataChannel dataChannel, RTCDataChannelState state);
    internal void DidGenerate(Engine enigne, TrackStats[] stats, SignalTarget target);
}
