using Unity.WebRTC;

internal static class RTCPeerConnectionStateExtension
{
    internal static string ToString(this RTCPeerConnectionState state)
    {
        return state switch
        {
            RTCPeerConnectionState.New => "new",
            RTCPeerConnectionState.Connecting => "connecting",
            RTCPeerConnectionState.Connected => "connected",
            RTCPeerConnectionState.Failed => "failed",
            RTCPeerConnectionState.Disconnected => "disconnected",
            RTCPeerConnectionState.Closed => "closed",
            _ => "unknown",
        };
    }
} 
