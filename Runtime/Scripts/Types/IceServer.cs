using System;
using Unity.WebRTC;
using System.Linq;

namespace LiveKit.Proto
{
    public partial class ICEServer
    {
        public RTCIceServer ToRTCType()
        {
            var rtcUsername = (Username == string.Empty) ? Username : null;
            var rtcCredential = (Credential == string.Empty) ? Credential : null;

            var rtcUrls = new string[Urls.Count];
            Urls.CopyTo(rtcUrls, 0);

            var a = Urls.ToArray();

            var rtcIceServer = new RTCIceServer
            {
                urls = Urls.ToArray(),
                username = rtcUsername,
                credential = rtcCredential
            };

            return DispatchQueue.WebRTC.Sync(() =>
            {
                return new RTCIceServer
                {
                    urls = Urls.ToArray(),
                    username = rtcUsername,
                    credential = rtcCredential
                };
            });
        }
    }
}
