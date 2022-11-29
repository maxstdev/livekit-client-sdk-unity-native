using System;
using Unity.WebRTC;
using PB = LiveKit.Proto;

public struct RTCDataChannelLabels
{
    public static string reliable = "_reliable";
    public static string lossy = "_lossy";
}

public static class RTCDataChannelExtension
{
    public static PB.DataChannelInfo toLKInfoType(this RTCDataChannel rtcDataChannel)
    {
        return new PB.DataChannelInfo
        {
            Id = (uint)Math.Max(0, rtcDataChannel.Id),
            Label = rtcDataChannel.Label
        };
    }
}
