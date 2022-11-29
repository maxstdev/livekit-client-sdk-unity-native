using Unity.WebRTC;

class RemoteVideoTrack : RemoteTrack
{
    internal RemoteVideoTrack(string name,
                              Track.Source source,
                              MediaStreamTrack track)
        : base(name: name,
               kind: Kind.Video,
               source: source,
               track: track)
    { }
}
