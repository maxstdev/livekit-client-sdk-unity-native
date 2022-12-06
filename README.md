# UniLiveKit - LiveKit Unity SDK for Native Platforms

UniLiveKit package is for developing Unity apps that target native platforms(Android, iOS, Mac, and Windows). Note that it is not the official Client SDK for [LiveKit](https://github.com/livekit/livekit.git). It doesn't work in Unity WebGL.  

This package is written in C#, referencing the following SDKs: 
* Mainly : LiveKit [Swift SDK](https://github.com/livekit/client-sdk-swift)
* Partially : LiveKit [Unity WebGL SDK](https://github.com/livekit/client-sdk-unity-web)

For WebRTC functions, [Unity WebRTC package](https://github.com/Unity-Technologies/com.unity.webrtc.git)(com.unity.webrtc) is used.

## Requirements
There are some requirements related to Unity WebRTC. Check Unity WebRTC's [Requirements](https://github.com/Unity-Technologies/com.unity.webrtc/blob/main/Documentation~/requirements.md
) specifically for Android builds.

### Unity Version
* Unity 2021.3

### Platform
* Windows
* macOS (Apple Slicon is not tested yet)
* iOS
* Android (ARMv7 is not supported)

## Docs about LiveKit
You can check docs and guides at https://docs.livekit.io 

## Installation
Follow this [unity tutorial](https://docs.unity3d.com/Manual/upm-ui-giturl.html) using the `https://github.com/maxstdev/livekit-client-sdk-unity-native.git` link.
You can then directly import the samples into the package manager.

### Additional Dependencies
    https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.3.3

## Features
* The screen capture function uses the Unity Scene Camera input, so UI components(Canvas, Button, ...) won't show in the capture.

### From Unity WebRTC
Following features are not supported:   
* WebGL
* Simulcast
* Android ARMv7

## Examples
Interface was implemented referring to LiveKit [Unity WebGL SDK](https://github.com/livekit/client-sdk-unity-web). The sample below is conceptual. Check [DemoApp](Samples~/UniLiveKitDemo) in Samples~/UniLiveKitDemo for details.

### Connecting to a room
```cs
public class JoinMenu : MonoBehaviour
{
    public static string LivekitURL { get; private set; }
    public static string RoomToken { get; private set; }
}

public class ExampleRoom : MonoBehaviour
{
    public Room room;

    void Start()
    {
        ConnectRoom();
    }

    async void ConnectRoom()
    {
        room = new Room(this);
        room.PrepareConnection();

        try
        {
            await room.Connect(JoinMenu.LivekitURL, JoinMenu.RoomToken);
            // Connected
            StartCoroutine(WebRTC.Update());
        }
        catch
        {
            // Error
        }
    }
}

```

### Publishing video & audio
```cs
room.LocalParticipant?.SetCamera(true);
room.LocalParticipant?.SetMicrophone(true);
```

### Display a video on a RawImage
```cs
void IRoomDelegate.DidSubscribe(Room room, RemoteParticipant participant, RemoteTrackPublication publication, Track track)
{
    DispatchQueue.MainSafeAsync(() =>
    {
        switch (track.kind)
        {
            case Track.Kind.Video:
                var videoTrack = track.GetMediaTrack as VideoStreamTrack;

                videoTrack.OnVideoReceived += (texture =>
                {
                    GameObject participantViewGO = Instantiate(participantViewPrefab, ViewContainer.transform);
                    var participantView = participantViewGO.GetComponent<ParticipantView>();

                    var videoView = participantView.VideoView;
                    videoView.texture = texture;
                });
                break;

            case Track.Kind.Audio:
                break;

            default:
                break;
        }
    });
}
```

## Known issues
* If a remote peer leaves the room during video call, the remaining peer's Windows Unity Editor will crash.

## License
[Apache-2.0 license](LICENSE)

### Third Party
* [com.unity.nuget.newtonsoft-json](https://github.com/JamesNK/Newtonsoft.Json) : [MIT license](https://github.com/JamesNK/Newtonsoft.Json/blob/master/LICENSE.md)
* [com.unity.webrtc](https://github.com/Unity-Technologies/com.unity.webrtc) : [Apache-2.0 license](https://github.com/Unity-Technologies/com.unity.webrtc/blob/main/LICENSE.md)
* [websocket-sharp](https://github.com/sta/websocket-sharp.git) : [MIT license](https://github.com/sta/websocket-sharp/blob/master/LICENSE.txt)
* [google.protobuf.3.10.1](https://github.com/protocolbuffers/protobuf/releases/tag/v3.10.1) : [Google license](https://github.com/protocolbuffers/protobuf/blob/main/LICENSE)
* [UniTask](https://github.com/Cysharp/UniTask.git) : [MIT license](https://github.com/Cysharp/UniTask/blob/master/LICENSE)
* [SerialQueue](https://github.com/borland/SerialQueue) : [MIT license](https://github.com/borland/SerialQueue/blob/master/LICENSE)