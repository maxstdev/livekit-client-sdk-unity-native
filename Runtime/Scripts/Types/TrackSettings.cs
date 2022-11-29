using System;

internal partial struct TrackSettings
{
    internal bool enabled;
    internal Dimensions dimensions;
    internal VideoQuality videoQuality;

    internal TrackSettings(bool enabled,
                           Dimensions dimensions,
                           VideoQuality videoQuality = VideoQuality.Low)
    {
        this.enabled = enabled;
        this.dimensions = dimensions;
        this.videoQuality = videoQuality;
    }

    internal TrackSettings CopyWith(bool? enabled = null,
                                    Dimensions? dimensions = null,
                                    VideoQuality? videoQuality = null)
    {
        return new TrackSettings(enabled: enabled ?? this.enabled,
                                 dimensions: dimensions ?? this.dimensions,
                                 videoQuality: videoQuality ?? this.videoQuality);
    }
}

internal partial struct TrackSettings: IEquatable<TrackSettings>
{
    public override int GetHashCode() => base.GetHashCode();

    public bool Equals(TrackSettings other)
    {
        if (other == null) return false;
        return (this.enabled == other.enabled &&
            this.dimensions == other.dimensions &&
            this.videoQuality == other.videoQuality) ? true : false;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        return (obj is TrackSettings casted) ? Equals(casted) : false;
    }

    public static bool operator ==(TrackSettings lhs, TrackSettings rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return object.Equals(lhs, rhs);
        return lhs.Equals(rhs);
    }

    public static bool operator !=(TrackSettings lhs, TrackSettings rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return !object.Equals(lhs, rhs);
        return !(lhs.Equals(rhs));
    }
}
