using PB = LiveKit.Proto;

public static class PBTrackTypeExtension
{
    public static Track.Kind ToLKType(this PB.TrackType pbTrackType)
    {
        return pbTrackType switch
        {
            PB.TrackType.Audio => Track.Kind.Audio,
            PB.TrackType.Video => Track.Kind.Video,
            PB.TrackType.Data => Track.Kind.Data,   // NOTE:Thomas: from proto buf
            _ => Track.Kind.None
        };
    }
}

public static class TrackKindExtension
{
    public static PB.TrackType ToPBType(this Track.Kind trackType)
    {
        return trackType switch
        {
            Track.Kind.Audio => PB.TrackType.Audio,
            Track.Kind.Video => PB.TrackType.Video,
            Track.Kind.Data => PB.TrackType.Data,   // NOTE:Thomas: from proto buf
            _ => (PB.TrackType)10   // NOTE:Thomas:swift // return .UNRECOGNIZED(10)
        };
    }
}