using System;
using Unity.WebRTC;
using PB = LiveKit.Proto;
using Sid = System.String;

public enum Reliability
{
    Reliable,
    Lossy
}

public static class ReliabilityExtension
{
    public static PB.DataPacket.Types.Kind toPBType(this Reliability reliability)
    {
        if (reliability == Reliability.Lossy) { return PB.DataPacket.Types.Kind.Lossy; }
        return PB.DataPacket.Types.Kind.Reliable;
    }
}

public enum SimulateScenario
{
    NodeFailure,
    Migration,
    ServerLeave,
    SpeakerUpdate
}