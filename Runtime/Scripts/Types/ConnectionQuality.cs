using PB = LiveKit.Proto;

public enum ConnectionQuality
{
    Unknown,
    Poor,
    Good,
    Excellent
}

public static class PBConnectionQualityExtension
{
    public static ConnectionQuality toLKType(this PB.ConnectionQuality pbQuality)
    {
        return pbQuality switch
        {
            PB.ConnectionQuality.Poor => ConnectionQuality.Poor,
            PB.ConnectionQuality.Good => ConnectionQuality.Good,
            PB.ConnectionQuality.Excellent => ConnectionQuality.Excellent,
            _ => ConnectionQuality.Unknown
        };
    }
} 