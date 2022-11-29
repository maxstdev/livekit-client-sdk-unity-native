using PB = LiveKit.Proto;

public static class PBTrackSourceExtension
{
    public static Track.Source ToLKType(this PB.TrackSource pbTrackSource)
    {
        return pbTrackSource switch
        {
            PB.TrackSource.Camera => Track.Source.Camera,
            PB.TrackSource.Microphone => Track.Source.Microphone,
            PB.TrackSource.ScreenShare => Track.Source.ScreenShareVideo,
            PB.TrackSource.ScreenShareAudio => Track.Source.ScreenShareAudio,
            _ => Track.Source.Unknown
        };
    }
}

public static class TrackSourceExtension
{
    public static PB.TrackSource ToPBType(this Track.Source trackSource)
    {
        return trackSource switch
        {
            Track.Source.Camera => PB.TrackSource.Camera,
            Track.Source.Microphone => PB.TrackSource.Microphone,
            Track.Source.ScreenShareVideo => PB.TrackSource.ScreenShare,
            Track.Source.ScreenShareAudio => PB.TrackSource.ScreenShareAudio,
            _ => PB.TrackSource.Unknown
        };
    }
}