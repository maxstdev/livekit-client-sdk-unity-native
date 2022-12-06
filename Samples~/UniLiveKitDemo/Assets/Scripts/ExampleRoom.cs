using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using LiveKit;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.WebRTC;
using System;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;
using System.Threading;

public partial class ExampleRoom : MonoBehaviour
{
    [SerializeField] GridLayoutGroup ViewContainer;
    [SerializeField] GameObject participantViewPrefab;
    [SerializeField] Button DisconnectButton;

    [SerializeField] Camera screenCaptureCamera;
    [SerializeField] Transform rotateObject;    // cube for demo

    [SerializeField] Toggle toggleWebCam;
    [SerializeField] Toggle toggleMicrophone;
    [SerializeField] Toggle toggleScreenShare;

    Room room;
    ParticipantView localParticipantView;
    Dictionary<RemoteParticipant, ParticipantView> remoteParticipantViews = new ();

    Coroutine videoUpdateCoroutine;

    void OnDestroy()
    {
        if (videoUpdateCoroutine != null)
        {
            StopCoroutine(videoUpdateCoroutine);
        }

        //RemoveAllParticipant();
    }

    void Awake()
    {
        toggleWebCam.onValueChanged.AddListener(OnToggleWebCam);
        toggleMicrophone.onValueChanged.AddListener(OnToggleMicrophone);
        toggleScreenShare.onValueChanged.AddListener(OnToggleScreenShare);
    }

    void OnToggleWebCam(bool enable)
    {
        room.LocalParticipant?.SetCamera(enable);
    }

    void OnToggleMicrophone(bool enable)
    {
        room.LocalParticipant?.SetMicrophone(enable);
    }

    void OnToggleScreenShare(bool enable)
    {
        var screenCamera = (enable) ? screenCaptureCamera : null;
        room.LocalParticipant?.SetScreenShare(enable, screenCamera);
    }

    void Start()
    {
        Debug.Log("ExampleRoom.Start()");

        toggleWebCam.enabled = false;
        toggleMicrophone.enabled = false;
        toggleScreenShare.enabled = false;

        ConnectRoom();
    }

    async void ConnectRoom()
    {
        Debug.Log("ExampleRoom.ConnectRoom()");

        room = new Room(this);
        room.PrepareConnection();

        Debug.Log($"url:{JoinMenu.LivekitURL}, token:{JoinMenu.RoomToken}");

        try
        {
            await room.Connect(JoinMenu.LivekitURL, JoinMenu.RoomToken);

            DisconnectButton.onClick.AddListener(async () =>
            {
                DisconnectRoom();
            });

            Debug.Log("Connected to the room");

            videoUpdateCoroutine = StartCoroutine(WebRTC.Update());

            DispatchQueue.MainSafeAsync(() =>
            {
                CreateInitParticipant();
            });

            Debug.Log(room.LocalParticipant);


            toggleWebCam.enabled = true;
            toggleMicrophone.enabled = true;
            toggleScreenShare.enabled = true;

            //toggleMicrophone.isOn = true;
            //toggleScreenShare.isOn = true;
        }
        catch
        {
            Debug.LogWarning("Failed to connect to the room !");
            DisconnectRoom();
        }
    }

    async void DisconnectRoom()
    {
        await room.Disconnect();
        room.CleanupDisconnection();

        SceneManager.LoadScene("JoinScene", LoadSceneMode.Single);
    }

    void Update()
    {
        if (rotateObject != null)
        {
            float t = Time.deltaTime;
            var rotation = new Vector3(100 * t, 200 * t, 300 * t) / 2;
            rotateObject.Rotate(rotation);
        }
    }
}

public partial class ExampleRoom : IRoomDelegate
{
    void IRoomDelegate.DidUpdate(Room room, ConnectionState connectionState, ConnectionState oldValue)
    {
        Debug.Log($"IRoomDelegate.DidUpdate(): {oldValue.State} -> {connectionState.State}");
    }

    void IRoomDelegate.DidJoin(Room room, RemoteParticipant participant)
    {
        Debug.Log($"IRoomDelegate.DidJoin(): {participant.sid}, {participant}");
        DispatchQueue.MainSafeAsync(() =>
        {
            AddParticipant(participant);
        });
    }

    void IRoomDelegate.DidLeave(Room room, RemoteParticipant participant)
    {
        Debug.Log($"IRoomDelegate.DidLeave(): {participant.sid}");
        DispatchQueue.MainSafeAsync(() =>
        {
            RemoveParticipant(participant);
        });
    }

    void IRoomDelegate.DidPublish(Room room, LocalParticipant localParticipant, LocalTrackPublication publication)
    {
        Debug.Log($"IRoomDelegate.DidPublish()");
        DispatchQueue.MainSafeAsync(() =>
        {
            HandleAddedTrack(localParticipant, publication.Track);
        });
    }
    void IRoomDelegate.DidUnpublish(Room room, LocalParticipant localParticipant, LocalTrackPublication publication)
    {
        Debug.Log($"IRoomDelegate.DidUnpublish()");
        DispatchQueue.MainSafeAsync(() =>
        {
            HandleRemovedTrack(localParticipant, publication.Track);
        });
    }

    void IRoomDelegate.DidSubscribe(Room room, RemoteParticipant participant, RemoteTrackPublication publication, Track track)
    {
        Debug.Log($"IRoomDelegate.DidSubscribe()");
        DispatchQueue.MainSafeAsync(() =>
        {
            HandleAddedTrack(participant, track);
        });
    }
    void IRoomDelegate.DidUnsubscribe(Room room, RemoteParticipant participant, RemoteTrackPublication publication, Track track)
    {
        Debug.Log($"IRoomDelegate.DidUnsubscribe()");
        DispatchQueue.MainSafeAsync(() =>
        {
            HandleRemovedTrack(participant, track);
        });
    }

    void IRoomDelegate.DidUpdate(Room room, Participant participant, TrackPublication publication, bool muted)
    {
        Debug.Log($"IRoomDelegate.DidUpdate() muted: " + muted.ToString());
        DispatchQueue.MainSafeAsync(() =>
        {
            HandleDidUpdateTrack(participant, publication.Track, muted);
        });
    }
}

// Function
public partial class ExampleRoom
{
    void CreateInitParticipant()
    {
        AddParticipant(room.LocalParticipant);

        foreach(var remoteParticipant in room.RemoteParticipants.Values)
        {
            AddParticipant(remoteParticipant);
        }
    }

    void AddParticipant(Participant participant = null)
    {
        if (ViewContainer.transform.childCount >= 6)
        {
            Debug.LogWarning($"No space to show more than 6 tracks: {ViewContainer.transform.childCount}");
            return;
        }

        switch (participant)
        {
            case LocalParticipant:
                if (localParticipantView != null)
                {
                    Debug.LogWarning("localParticipantView != null");
                    break;
                }

                GameObject localParticipantViewGO = Instantiate(participantViewPrefab, ViewContainer.transform);
                localParticipantView = localParticipantViewGO.GetComponent<ParticipantView>();
                localParticipantView.Identity.text = "local: " + participant.identity;
                break;

            case RemoteParticipant:
                GameObject remoteParticipantViewGO = Instantiate(participantViewPrefab, ViewContainer.transform);
                ParticipantView remoteParticipantView = remoteParticipantViewGO.GetComponent<ParticipantView>();
                remoteParticipantView.Identity.text = participant.identity;

                remoteParticipantViews[participant as RemoteParticipant] = remoteParticipantView;
                break;

            default:
                Debug.LogWarning("participant type: " + participant.GetType());
                break;
        }
    }

    void RemoveParticipant(Participant participant)
    {
        switch (participant)
        {
            case LocalParticipant:
                if (localParticipantView == null)
                {
                    Debug.LogWarning("localParticipantView == null");
                    break;
                }

                if (localParticipantView.gameObject != null)
                {
                    Destroy(localParticipantView.gameObject);
                }

                localParticipantView = null;
                break;

            case RemoteParticipant:
                var remoteParticipant = participant as RemoteParticipant;
                if (remoteParticipant == null)
                {
                    Debug.LogWarning("remoteParticipant == null");
                    break;
                }

                if (remoteParticipantViews.TryGetValue(remoteParticipant, out var participantView))
                {
                    if (participantView != null && participantView.gameObject != null)
                    {
                        Destroy(participantView.gameObject);
                    }

                    remoteParticipantViews.Remove(remoteParticipant);
                }
                break;

            default:
                Debug.LogWarning("participant type: " + participant?.GetType());
                break;
        }
    }

    void RemoveAllParticipant()
    {
        if (localParticipantView == null)
        {
            Debug.LogWarning("localParticipantView == null");
        } 
        else
        {
            if (localParticipantView.gameObject != null)
            {
                Destroy(localParticipantView.gameObject);
            }
        }

        localParticipantView = null;

        var remoteParticipantViewsValues = remoteParticipantViews.Values;
        foreach (var remoteParticipantViewsValue in remoteParticipantViewsValues)
        {
            if (remoteParticipantViewsValue != null && remoteParticipantViewsValue.gameObject != null)
            {
                Destroy(remoteParticipantViewsValue.gameObject);
            }
        }
        remoteParticipantViews.Clear();

        var remoteAudioSourcesKeys = room.remoteAudioSources.Keys;
        foreach (var remoteAudioSourcesKey in remoteAudioSourcesKeys)
        {
            room.remoteAudioSources[remoteAudioSourcesKey].Stop();
        }
        room.remoteAudioSources.Clear();
    }
}

public partial class ExampleRoom
{
    void HandleAddedTrack(Participant participant, Track track)
    {
        Debug.Log($"HandleAddedTrack {track.kind} ,{participant}");

        switch (track.kind)
        {
            case Track.Kind.Video:
                if (track.GetMediaTrack is not VideoStreamTrack)
                {
                    Debug.Log($"track.GetMediaTrack.GetType() {track.GetMediaTrack.GetType()}");
                    break;
                }

                var videoTrack = track.GetMediaTrack as VideoStreamTrack;

                switch (participant)
                {
                    case LocalParticipant:
                        if (localParticipantView == null)
                        {
                            Debug.LogWarning("localParticipantView == null");
                            break;
                        }

                        var localVideoView = localParticipantView.VideoView;
                        localVideoView.texture = videoTrack.Texture;
                        localVideoView.enabled = true;

                        // vertical flip : graphicsDeviceType
                        var graphicsDeviceType = SystemInfo.graphicsDeviceType;
                        Debug.Log($"graphicsDeviceType {graphicsDeviceType}");

                        switch (graphicsDeviceType)
                        {
                            // Right hand coordinate system
                            case UnityEngine.Rendering.GraphicsDeviceType.Vulkan:       // AOS
                            case UnityEngine.Rendering.GraphicsDeviceType.Metal:        // iOS, OSX
                            case UnityEngine.Rendering.GraphicsDeviceType.Direct3D11:   // Win
                            case UnityEngine.Rendering.GraphicsDeviceType.Direct3D12:   // Win
                                var localScale = localVideoView.rectTransform.localScale;
                                localScale.y = -1;
                                localVideoView.rectTransform.localScale = localScale;
                                break;

                            // left hand coordinate system
                            case UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore:
                            case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2:    // AOS
                            case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3:    // AOS
                            default:
                                break;
                        }

                        // horizontal flip : front camera
                        if (track is LocalVideoTrack localVideoTrack)
                        {
                            if (localVideoTrack.Capturer is WebCamCapturer webCamCapturer)
                            {
                                if (webCamCapturer.MWebCamDevice?.isFrontFacing == true)
                                {
                                    var localScale = localVideoView.rectTransform.localScale;
                                    localScale.x = -1;
                                    localVideoView.rectTransform.localScale = localScale;
                                }
                            }
                        }

                        var localVideoViewARF = (AspectRatioFitter)localVideoView.GetComponent<AspectRatioFitter>();
                        localVideoViewARF.aspectRatio = (float)videoTrack.Texture.width / videoTrack.Texture.height;

                        break;

                    case RemoteParticipant:
                        var remoteParticipant = participant as RemoteParticipant;
                        if (remoteParticipant == null)
                        {
                            Debug.LogWarning("remoteParticipant == null");
                            break;
                        }

                        videoTrack.OnVideoReceived += (texture =>
                        {
                            Debug.Log($"videoTrack.OnVideoReceived: {remoteParticipant}");

                            if (remoteParticipantViews.ContainsKey(remoteParticipant) is false)
                            {
                                Debug.LogWarning($"remoteVideos.ContainsKey(publication) is false: {remoteParticipant}");
                                return;
                            }

                            if (remoteParticipantViews.TryGetValue(remoteParticipant, out var remoteParticipantView))
                            {
                                var remoteVideoView = remoteParticipantView.VideoView;
                                remoteVideoView.texture = texture;
                                remoteVideoView.enabled = true;

                                Debug.Log($"RemoteParticipant wrapMode {texture.wrapMode}, {texture.wrapModeU}, {texture.wrapModeV}");

                                var remoteVideoViewARF = (AspectRatioFitter)remoteVideoView.GetComponent<AspectRatioFitter>();
                                remoteVideoViewARF.aspectRatio = (float)texture.width / texture.height;
                            }
                        });
                        break;

                    default:
                        Debug.LogWarning("participant type: " + participant.GetType());
                        break;
                }
                break;

            case Track.Kind.Audio:
                switch (participant)
                {
                    case LocalParticipant:
                        if (localParticipantView == null)
                        {
                            Debug.LogWarning("localParticipantView == null");
                            break;
                        }

                        localParticipantView.MicState.text = "Mic On";
                        break;

                    case RemoteParticipant:
                        if (track.GetMediaTrack is not AudioStreamTrack)
                        {
                            Debug.Log($"track.GetMediaTrack.GetType() {track.GetMediaTrack.GetType()}");
                            break;
                        }

                        var audioTrack = track.GetMediaTrack as AudioStreamTrack;

                        Debug.Log("rtcTrackEvent audioTrack " + track.sid);

                        var remoteAudio = gameObject.AddComponent<AudioSource>();

                        remoteAudio.SetTrack(audioTrack);
                        remoteAudio.loop = true;
                        remoteAudio.Play();

                        room.remoteAudioSources.Add(track.sid, remoteAudio);

                        var remoteParticipant = participant as RemoteParticipant;
                        if (remoteParticipant == null)
                        {
                            Debug.LogWarning("remoteParticipant == null");
                            break;
                        }

                        if (remoteParticipantViews.TryGetValue(remoteParticipant, out var remoteParticipantView))
                        {
                            remoteParticipantView.MicState.text = "Mic On";
                        }
                        break;

                    default:
                        Debug.LogWarning("participant type: " + participant.GetType());
                        break;
                }
                break;

            default:
                Debug.LogWarning($"track.kind {track.kind}");
                break;
        }
    }

    void HandleRemovedTrack(Participant participant, Track track)
    {
        Debug.Log($"HandleRemovedTrack {track.kind} ,{participant}");

        switch (track.kind)
        {
            case Track.Kind.Video:
                switch (participant)
                {
                    case LocalParticipant:
                        if (localParticipantView == null)
                        {
                            Debug.LogWarning("localParticipantView == null");
                            break;
                        }

                        //localParticipantView.videoView.enabled = false;
                        break;

                    case RemoteParticipant:
                        var remoteParticipant = participant as RemoteParticipant;
                        if (remoteParticipant == null)
                        {
                            Debug.LogWarning("remoteParticipant == null");
                            break;
                        }

                        if (remoteParticipantViews.TryGetValue(remoteParticipant, out var remoteParticipantView))
                        {
                            //remoteParticipantView.videoView.enabled = false;
                        }
                        break;

                    default:
                        Debug.LogWarning("participant type: " + participant.GetType());
                        break;
                }
                break;

            case Track.Kind.Audio when participant is RemoteParticipant:
                if (room.remoteAudioSources.TryGetValue(track.sid, out var remoteAudioSource))
                {
                    remoteAudioSource.Stop();
                    //room.remoteAudioSources.Remove(track.sid);
                }
                break;

            default:
                Debug.LogWarning($"track.kind {track.kind}");
                break;
        }
    }

    void HandleDidUpdateTrack(Participant participant, Track track, bool muted)
    {
        Debug.Log($"HandleDidUpdateTrack {track.kind} ,{participant}, {muted}");

        switch (track.kind)
        {
            case Track.Kind.Video:
                switch (participant)
                {
                    case LocalParticipant:
                        if (localParticipantView == null)
                        {
                            Debug.LogWarning("localParticipantView == null");
                            break;
                        }

                        localParticipantView.VideoView.enabled = !muted;
                        break;

                    case RemoteParticipant:
                        var remoteParticipant = participant as RemoteParticipant;
                        if (remoteParticipant == null)
                        {
                            Debug.LogWarning("remoteParticipant == null");
                            break;
                        }

                        if (remoteParticipantViews.TryGetValue(remoteParticipant, out var remoteParticipantView))
                        {
                            remoteParticipantView.VideoView.enabled = !muted;
                        }
                        break;

                    default:
                        Debug.LogWarning("participant type: " + participant.GetType());
                        break;
                }
                break;

            case Track.Kind.Audio:
                switch (participant)
                {
                    case LocalParticipant:
                        if (localParticipantView == null)
                        {
                            Debug.LogWarning("localParticipantView == null");
                            break;
                        }

                        localParticipantView.MicState.text = muted ? "Mic Off" : "Mic On";
                        break;

                    case RemoteParticipant:
                        var remoteParticipant = participant as RemoteParticipant;
                        if (remoteParticipant == null)
                        {
                            Debug.LogWarning("remoteParticipant == null");
                            break;
                        }

                        if (remoteParticipantViews.TryGetValue(remoteParticipant, out var remoteParticipantView))
                        {
                            remoteParticipantView.MicState.text = muted ? "Mic Off" : "Mic On";
                        }

                        if (room.remoteAudioSources.TryGetValue(track.sid, out var remoteAudioSource))
                        {
                            if (muted)
                                remoteAudioSource.Stop();
                            else
                                remoteAudioSource.Play();
                        }
                        break;

                    default:
                        Debug.LogWarning("participant type: " + participant.GetType());
                        break;
                }
                break;

            default:
                Debug.LogWarning($"track.kind {track.kind}");
                break;
        }
    }
}