using System;
using Unity.WebRTC;
using Newtonsoft.Json;

partial struct IceCandidate
{
    [JsonProperty("candidate")]     public string Sdp;
    [JsonProperty("sdpMLineIndex")] public Int32 SdpMLineIndex;
    [JsonProperty("sdpMid")]        public string SdpMid;
}

partial struct IceCandidate
{
    // HACK:Thomas:swift: C#에서 예외는 굳이 필요하지 않아 보임
    internal string toJsonString() => JsonConvert.SerializeObject(this);

    //func toJsonString() throws -> String {
    //    let data = try JSONEncoder().encode(self)
    //    guard let string = String(data: data, encoding: .utf8) else {
    //        throw InternalError.convert(message: "Failed to convert Data to String")
    //    }
    //    return string
    //}
}



internal static class RTCIceCandidateExtension
{
    internal static IceCandidate toLKType(this RTCIceCandidate rtcIceCandidate)
    {
        return new IceCandidate
        {
            Sdp = rtcIceCandidate.Candidate,
            SdpMLineIndex = rtcIceCandidate.SdpMLineIndex ?? 0,
            SdpMid = rtcIceCandidate.SdpMid
        };
    }

    // HACK:Thomas:swift: C#에 convenience init을 구현할 방법이 안보임, + Engine에서 사용해야 해서 우선 직접 구현
    internal static RTCIceCandidate FromJsonString(string jsonString)
    {
        var iceCandidate = JsonConvert.DeserializeObject<IceCandidate>(jsonString);

        var option = new RTCIceCandidateInit
        {
            sdpMid = iceCandidate.SdpMid,
            sdpMLineIndex = iceCandidate.SdpMLineIndex,
            candidate = iceCandidate.Sdp
        };

        return new RTCIceCandidate(option);
    }

    //convenience init(fromJsonString string: String) throws {
    //    // String to Data
    //    guard let data = string.data(using: .utf8) else {
    //        throw InternalError.convert(message: "Failed to convert String to Data")
    //    }
    //    // Decode JSON
    //    let iceCandidate: IceCandidate = try JSONDecoder().decode(IceCandidate.self, from: data)

    //    self.init(sdp: iceCandidate.sdp,
    //              sdpMLineIndex: iceCandidate.sdpMLineIndex,
    //              sdpMid: iceCandidate.sdpMid)
    //}
}