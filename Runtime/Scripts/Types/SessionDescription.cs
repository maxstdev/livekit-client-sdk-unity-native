using System;
using LiveKit.Proto;
using Unity.WebRTC;
using static LiveKit.Proto.ClientInfo.Types;
using PB = LiveKit.Proto;   // ProtoBuf

internal static class RTCSessionDescriptionExtension
{
    internal static PB.SessionDescription toPBType(this RTCSessionDescription rtcSessionDescription)
    {
        var sd = new PB.SessionDescription();
        sd.Sdp = rtcSessionDescription.sdp;

        sd.Type = rtcSessionDescription.type switch
        {
            RTCSdpType.Answer   => "answer",
            RTCSdpType.Offer    => "offer",
            RTCSdpType.Pranswer => "pranswer",
            RTCSdpType.Rollback => "rollback",  // NOTE:Thomas: swift코드에 정의 되어 있지 않음
            _ => throw new Exception($"Unknown state {rtcSessionDescription.type}") // This should never happen
        };

        return sd;
    }
}

namespace LiveKit.Proto
{
    public partial class SessionDescription
    {
        public RTCSessionDescription toRTCType()
        {
            RTCSdpType sdpType;

            sdpType = this.Type switch
            {
                "answer" => RTCSdpType.Answer,
                "offer" => RTCSdpType.Offer,
                "pranswer" => RTCSdpType.Pranswer,
                "rollback" => RTCSdpType.Rollback, // NOTE:Thomas: swift 코드에는 정의 되어 있지 않음
                _ => throw new Exception($"Unknown state {this.Type}") // This should never happen
            };

            return Engine.CreateSessionDescription(type: sdpType, sdp: Sdp);
        }
    }
}