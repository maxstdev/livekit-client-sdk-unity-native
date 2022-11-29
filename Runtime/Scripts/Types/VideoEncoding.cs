using System;

public partial struct VideoEncoding
{
    public int MaxBitrate;
	public int MaxFps;

	public VideoEncoding(int maxBitrate, int maxFps)
	{
		this.MaxBitrate = maxBitrate;
		this.MaxFps = maxFps;
	}
}

public partial struct VideoEncoding : IComparable<VideoEncoding>, IComparable
{
    public int CompareTo(VideoEncoding other)
    {
        if (this.MaxBitrate == other.MaxBitrate)
        {
            return this.MaxFps.CompareTo(other.MaxFps);
        }

        return this.MaxBitrate.CompareTo(other.MaxBitrate);
    }

    int IComparable.CompareTo(object obj)
    {
        if (!(obj is VideoEncoding))
            throw new ArgumentException("Argument is not a VideoEncoding", "obj");

        VideoEncoding videoEncoding = (VideoEncoding)obj;

        return this.CompareTo(videoEncoding);
    }

    public static bool operator <(VideoEncoding lhs, VideoEncoding rhs) => lhs.CompareTo(rhs) < 0;
    public static bool operator >(VideoEncoding lhs, VideoEncoding rhs) => lhs.CompareTo(rhs) > 0;
}

public partial struct VideoEncoding : IEquatable<VideoEncoding>
{
    public override int GetHashCode() => base.GetHashCode();

    public bool Equals(VideoEncoding other)
    {
        if (other == null) return false;
        return (this.MaxBitrate == other.MaxBitrate && this.MaxFps == other.MaxFps) ? true : false;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        return (obj is VideoEncoding casted) ? Equals(casted) : false;
    }

    public static bool operator ==(VideoEncoding lhs, VideoEncoding rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return object.Equals(lhs, rhs);
        return lhs.Equals(rhs);
    }

    public static bool operator !=(VideoEncoding lhs, VideoEncoding rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return !object.Equals(lhs, rhs);
        return !(lhs.Equals(rhs));
    }
}
