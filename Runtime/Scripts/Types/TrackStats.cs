using System;
using System.Collections.Generic;
using Unity.WebRTC;

public static class DoubleExtension
{
    public static double Rounded(this double value, int to)
    {
        return Math.Round(value, to);
    }
}

public partial struct TrackStats
{
    private static double bpsDivider = 1000;

	private string Format(int bps)
    {
        var ordinals = new[] { "", "K", "M", "G", "T", "P", "E" };

        var rate = (double)bps;
        var ordinal = 0;

        while (rate > bpsDivider)
        {
            rate /= bpsDivider;
            ordinal += 1;
        }

        return rate.Rounded(2).ToString() + ordinals[ordinal] + "bps";
    }

    string FormattedBpsSent()
    {
        return Format(BpsSent);
    }

    string FormattedBpsReceived()
    {
        return Format(BpsReceived);
    }
}

public partial struct TrackStats
{
    public static string KeyTypeSSRC = "ssrc";
    public static string KeyTrackId = "googTrackId";

    public static string KeyBytesSent = "bytesSent";
    public static string KeyBytesReceived = "bytesReceived";
    public static string KeyLastDate = "lastDate";
    public static string KeyMediaTypeKey = "mediaType";
    public static string KeyCodecName = "googCodecName";

    // date and time of this stats created
    DateTime Created; // let created = Date()

    internal string Ssrc;
    internal string TrackId;

    // TODO: add more values
    public int BytesSent;
	public int BytesReceived;
	public string CodecName;

    public int BpsSent;
	public int BpsReceived;

    //  video
    //  "googCpuLimitedResolution": "false",
    //  "hugeFramesSent": "0",
    //  "googRtt": "0",
    //  "mediaType": "video",
    //  "googAdaptationChanges": "0",
    //  "googEncodeUsagePercent": "0",
    //  "googFrameHeightInput": "450",
    //  "googTrackId": "B6D8300D-53AC-4C10-A9AC-4403CE1EE7E0",
    //  "ssrc": "2443805324",
    //  "googBandwidthLimitedResolution": "false",
    //  "googContentType": "realtime",
    //  "googFrameHeightSent": "112",
    //  "codecImplementationName": "SimulcastEncoderAdapter (libvpx, libvpx)",
    //  "framesEncoded": "1",
    //  "bytesSent": "44417",
    //  "googCodecName": "VP8",
    //  "packetsSent": "181",
    //  "googPlisReceived": "0",
    //  "packetsLost": "0",
    //  "googAvgEncodeMs": "0",
    //  "googFirsReceived": "0",
    //  "googNacksReceived": "0",
    //  "qpSum": "86",
    //  "transportId": "Channel-0-1",
    //  "googHasEnteredLowResolution": "false",
    //  "googFrameRateSent": "1",
    //  "googFrameWidthInput": "800",
    //  "googFrameWidthSent": "200",
    //  "googFrameRateInput": "1"

    public static TrackStats? From(IDictionary<String, object> values, TrackStats? previous)
    {
        //  ssrc is required
        if (!values.TryGetValue(TrackStats.KeyTypeSSRC, out var ssrc)) { return null; }
        if (!values.TryGetValue(TrackStats.KeyTrackId, out var trackId)) { return null; }

        TrackStats trackStats = new()
        {
            Ssrc = ssrc.ToString(),
            TrackId = trackId.ToString()
        };

        if (values.TryGetValue(TrackStats.KeyBytesSent, out var byteSentObject)
            && Int32.TryParse(byteSentObject.ToString(), out var byteSent))
        {
            trackStats.BytesSent = byteSent;
        }
        else
        {
            trackStats.BytesSent = 0;
        }

        if (values.TryGetValue(TrackStats.KeyBytesReceived, out var bytesReceivedObject)
            && Int32.TryParse(bytesReceivedObject.ToString(), out var bytesReceived))
        {
            trackStats.BytesReceived = bytesReceived;
        }
        else
        {
            trackStats.BytesReceived = 0;
        }

        if (values.TryGetValue(TrackStats.KeyCodecName, out var codecNamed))
        {
            trackStats.CodecName = codecNamed.ToString();
        }
        else
        {
            trackStats.CodecName = null;
        }

        if (previous != null)
        {
            var secondsDiff = ((TimeSpan)(trackStats.Created - previous?.Created)).TotalSeconds;
            trackStats.BpsSent = (int)((double)(((trackStats.BytesSent - previous?.BytesSent) * 8)) / Math.Abs(secondsDiff));
            trackStats.BpsReceived = (int)((double)(((trackStats.BytesReceived - previous?.BytesReceived) * 8)) / Math.Abs(secondsDiff));
        }
        else
        {
            trackStats.BpsSent = 0;
            trackStats.BpsReceived = 0;
        }

        return trackStats;
    }

    public TrackStats(DateTime? dateTime = null)
    {
        Created = dateTime ?? DateTime.Now;

        Ssrc = string.Empty;
        TrackId = string.Empty;

        BytesSent = 0;
        BytesReceived = 0;
        CodecName = null;

        BpsSent = 0;
        BpsReceived = 0;
    }
}

public partial struct TrackStats : IEquatable<TrackStats>
{
    public override int GetHashCode() => base.GetHashCode();

    public bool Equals(TrackStats other)
    {
        if (other == null) return false;
        return (this.Created == other.Created &&
               this.Ssrc == other.Ssrc &&
               this.TrackId == other.TrackId) ? true : false;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        return (obj is TrackStats casted) ? Equals(casted) : false;
    }

    public static bool operator ==(TrackStats lhs, TrackStats rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return object.Equals(lhs, rhs);
        return lhs.Equals(rhs);
    }

    public static bool operator !=(TrackStats lhs, TrackStats rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return !object.Equals(lhs, rhs);
        return !(lhs.Equals(rhs));
    }
}