using Unity.WebRTC;

public class AudioTrack : Track
{
    internal AudioTrack(string name, Kind kind, Source source, MediaStreamTrack track) : base(name, kind, source, track)
    {
    }
}