using Unity.WebRTC;
using System.Linq;
using LiveKit.Proto;

// NOTE:Thomas: Unity.WebRTC.RTCConfiguration The interface provided is simple compared to other platforms
//public struct RTCConfiguration
//{
//    public RTCIceServer[] iceServers;
//    public RTCIceTransportPolicy? iceTransportPolicy;
//    public RTCBundlePolicy? bundlePolicy;
//    public int? iceCandidatePoolSize;
//    public bool? enableDtlsSrtp;
//}

public static class RTCConfigurationExtension
{
    public static string[] defaultIceServers = { "stun:stun.l.google.com:19302",
                                                 "stun:stun1.l.google.com:19302" };

    public static RTCConfiguration liveKitDefault()
    {
        var result = DispatchQueue.WebRTC.Sync(() => { return new RTCConfiguration(); });

        // Unexposed in Unity.WebRTC.RTCConfiguration
        //result.sdpSemantics = .unifiedPlan
        //result.continualGatheringPolicy = .gatherContinually
        //result.candidateNetworkPolicy = .all

        // don't send TCP candidates, they are passive and only server should be sending
        //result.tcpCandidatePolicy = .disabled

        result.iceTransportPolicy = RTCIceTransportPolicy.All;

        result.iceServers = DispatchQueue.WebRTC.Sync(() => { return new RTCIceServer[] { new RTCIceServer { urls = defaultIceServers, } }; });

        // Only in Unity.WebRTC.RTCConfiguration
        result.enableDtlsSrtp = true;

        return result;
    }

    internal static void Set(this RTCConfiguration configuration, LiveKit.Proto.ICEServer[] pbIceServers)
    {
        // convert to a list of RTCIceServer
        var rtcIceServers = pbIceServers.Select(iceServer => iceServer.ToRTCType()).ToArray();

        if (rtcIceServers.Length == 0)
        {
            // set new iceServers if not empty
            configuration.iceServers = rtcIceServers;
        }
    }
}
