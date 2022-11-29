using System;

internal partial struct TrackSettings
{
    public override string ToString()
    {
        return $"TrackSettings(enabled: {enabled}, dimensions: {dimensions}, videoQuality: {videoQuality})";
    }
}

public partial class TrackPublication
{
    public override string ToString()
    {
        return $"TrackPublication(sid: {sid}, kind: {Kind}, source: {Source})";
    }
}


public partial class Room
{
    public override string ToString()
    {
        return $"Room(sid: {Sid}, name: {Name}, serverVersion: {ServerVersion}, serverRegion: {ServerRegion})";
    }
}

public partial class Participant
{
    public override string ToString()
    {
        return $"Participant(sid: {sid})";
    }
}

public partial class Track
{
    public override string ToString()
    {
        return $"Track(sid: {sid}, name: {name}, source: {source})";
    }
}