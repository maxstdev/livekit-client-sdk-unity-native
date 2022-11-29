using Cysharp.Threading.Tasks;
using Dispatch;
using Google.Protobuf;
using LiveKit.Proto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UniLiveKit.ErrorException;
using Unity.WebRTC;
using UnityEngine;
using static Track;
using PB = LiveKit.Proto;
using Sid = System.String;

public partial class LocalParticipant : Participant
{
    public List<LocalTrackPublication> LocalAudioTracks =>
        audioTracks.Select(pub => pub as LocalTrackPublication)
                   .Where(rPub => rPub != null).ToList();

    public List<LocalTrackPublication> LocalVideoTracks =>
        videoTracks.Select(pub => pub as LocalTrackPublication)
                   .Where(rPub => rPub != null).ToList();

    private bool allParticipantsAllowed = true;
    private List<ParticipantTrackPermission> trackPermissions = new();

    #region Unity Custom : about WebRTC
    RTCRtpCodecCapability[] preferredCodecs
    {
        get
        {
            var codecs = RTCRtpSender.GetCapabilities(TrackKind.Video).codecs;
            return codecs.Where(codec => codec.mimeType == "video/H264").ToArray();
        }
    }
    #endregion

    internal LocalParticipant(ParticipantInfo info, Room room)
        : base(info.Sid, info.Identity, info.Name, room)
    {
        UpdateFromInfo(info: info);
    }

    public LocalTrackPublication GetLocalTrackPublication(Sid sid)
    {
        return tracks[sid] as LocalTrackPublication;
    }

    internal async UniTask<LocalTrackPublication> Publish(LocalTrack track,
                                                          IPublishOptions publishOptions = null)
    {
        Debug.Log($"[publish] {track} options: {JsonConvert.SerializeObject(publishOptions)}...");

        var publisher = Room.engine.Publisher;
        if (publisher == null)
        {
            throw new EnumException<EngineError>(EngineError.State, "publisher is null");
        }

        if (_state.Value.tracks.Values.FirstOrDefault(pub => ReferenceEquals(pub.Track, track)) != null)
        {
            throw new EnumException<TrackError>(TrackError.State, "This track has already been published.");
        }

        if (track is not LocalVideoTrack && track is not LocalAudioTrack)
        {
            throw new EnumException<TrackError>(TrackError.State, "Unknown LocalTrack type");
        }

        // try to start the track
        try
        {
            await track.Start();

            var a = await queue.Async<UniTask<LocalTrackPublication>>(async () =>
            {
                Dimensions? dimensions = null;

                // ensure dimensions are resolved for VideoTracks
                var localVideoTrack = track as LocalVideoTrack;
                if (localVideoTrack is not null)
                {
                    Debug.Log("[publish] waiting for dimensions to resolve...");

                    // wait for dimensions
                    dimensions = await localVideoTrack.Capturer._state.MutateAwait<Dimensions>(async state =>
                    {
                        var rDimensions = await state.dimensionsCompleter.Wait(
                            this.queue,
                            TimeInterval.DefaultCaptureStart,
                            new EnumException<TrackError>(TrackError.TimeOut, "unable to resolve dimensions"));

                        return new(state, rDimensions);
                    });
                }

                // request a new track to the server
                var sendedTrack = await this.Room.engine.signalClient.SendAddTrack<RTCRtpTransceiverInit>(
                    cid: track.MediaTrack.Id,
                    name: track.name,
                    type: track.kind.ToPBType(),
                    source: track.source.ToPBType(),
                    (populator) =>
                    {
                        RTCRtpTransceiverInit transInit = DispatchQueue.WebRTC.Sync(() => { return new RTCRtpTransceiverInit(); });
                        transInit.direction = RTCRtpTransceiverDirection.SendOnly;

                        if (track is LocalVideoTrack localVideoTrack)
                        {
                            if (dimensions == null)
                            {
                                throw new EnumException<TrackError>(TrackError.State, "VideoCapturer dimensions are unknown");
                            }

                            Debug.Log($"[publish] computing encode settings with dimensions: {dimensions}...");

                            var videoPublishOptions = (publishOptions as VideoPublishOptions?) ?? this.Room._state.Value.options.defaultVideoPublishOptions;

                            var encodings = Utils.ComputeEncodings(dimensions: dimensions.Value,
                                publishOptions: videoPublishOptions,
                                isScreenShare: localVideoTrack.source == Source.ScreenShareVideo);

                            Debug.Log($"[publish] using encodings: {encodings}");
                            transInit.sendEncodings = encodings;

                            var videoLayers = dimensions.Value.VideoLayers(encodings);

                            Debug.Log($"[publish] using layers: {String.Join(", ", videoLayers.Select(v => v.ToString()))}");

                            populator.Width = (uint)dimensions.Value.Width;
                            populator.Height = (uint)dimensions.Value.Height;
                            populator.Layers.Add(videoLayers);

                            Debug.Log($"[publish] requesting add track to server with {populator}...");
                        }
                        else if (track is LocalAudioTrack)
                        {
                            // additional params for Audio
                            var audioPublishOptions = (publishOptions as AudioPublishOptions?) ?? this.Room._state.Value.options.defaultAudioPublishOptions;
                            populator.DisableDtx = !audioPublishOptions.dtx;
                        }

                        return new(populator, transInit);
                    });

                var transInit = sendedTrack.Item1;
                var trackInfo = sendedTrack.Item2;
                Debug.Log($"[publish] server responded trackInfo: {trackInfo}");

                // add transceiver to pc
                var transceiver = await publisher.AddTransceiver(track.MediaTrack, transInit);
                Debug.Log("[publish] added transceiver: " + trackInfo + "...");

                #region Unity Custom : about WebRTC
#if UNITY_IOS || UNITY_ANDROID
                if (localVideoTrack is not null)
                {
                    var error = transceiver.SetCodecPreferences(preferredCodecs);
                    if (error != RTCErrorType.None)
                    {
                        Debug.LogError("SetCodecPreferences failed");
                    }
                }
#endif
                #endregion

                await track.OnPublish();

                // store publishOptions used for this track
                track.publishOptions = publishOptions;
                track.Transceiver = transceiver;

                // prefer to maintainResolution for screen share
                if (track.source == Source.ScreenShareVideo)
                {
                    Debug.Log($"[publish] set degradationPreference to .maintainResolution");

                    // NOTE:Thomas Unity.WebRTC..DegradationPreference does not exist
                    //let params = transceiver.sender.parameters
                    //params.degradationPreference = NSNumber(value: RTCDegradationPreference.maintainResolution.rawValue)
                    //// changing params directly doesn't work so we need to update params
                    //// and set it back to sender.parameters
                    //transceiver.sender.parameters = params
                }

                await Room.engine.PublisherShouldNegotiate();

                var publication = new LocalTrackPublication(trackInfo, this, track);
                AddTrack(publication);

                // notify didPublish
                Notify((iParticipantDelegate) =>
                {
                    iParticipantDelegate.LocalParticipantDidPublish(this, publication);
                },
                () =>
                {
                    return "localParticipant.didPublish " + publication;
                });

                Room.Notify((iRoomDelegate) =>
                {
                    iRoomDelegate.DidPublish(this.Room, this, publication);
                },
                () =>
                {
                    return "localParticipant.didPublish " + publication;
                });

                Debug.Log($"[publish] success {publication}");

                return publication;
            });
        }
        catch (Exception error)
        {
            Debug.Log($"[publish] failed {track}, error: {error}");

            // stop the track
            try
            {
                await track.Stop();
            }
            catch (Exception trackStopError)
            {
                Debug.LogError($"[publish] failed to stop track, error: " + trackStopError);
            }
        }

        return null;
    }

    /// publish a new audio track to the Room
    public UniTask<LocalTrackPublication> PublishAudioTrack(LocalAudioTrack track,
                                                            AudioPublishOptions? publishOptions = null)
    {
        return Publish(track: track, publishOptions: publishOptions);
    }

    /// publish a new video track to the Room
    public UniTask<LocalTrackPublication> PublishVideoTrack(LocalVideoTrack track,
                                                            VideoPublishOptions? publishOptions = null)
    {
        return Publish(track: track, publishOptions: publishOptions);
    }

    public override async UniTask UnpublishAll(bool notify = true)
    {
        // build a list of promises
        var promises = _state.Value.tracks.Values
            .Select(v => v as LocalTrackPublication)
            .Where(v => v != null)
            .Select(v => Unpublish(publication: v, notify: notify));

        // combine promises to wait all to complete
        await queue.Async(async () =>
        {
            await UniTask.WhenAll(promises);
        });
    }

    /// unpublish an existing published track
    /// this will also stop the track
    public async UniTask Unpublish(LocalTrackPublication publication, bool notify = true)
    {
        UniTask NotifyDidUnpublish()
        {
            return queue.Async(() =>
            {
                if (notify == false) { return UniTask.CompletedTask; }

                // notify unpublish
                this.Notify((iParticipantDelegate) =>
                {
                    iParticipantDelegate.LocalParticipantDidUnpublish(this, publication: publication);
                },
                () => // label:
                {
                    return $"localParticipant.didUnpublish {publication}";
                });

                this.Room.Notify((iRoomDelegate) =>
                {
                    iRoomDelegate.DidUnpublish(this.Room, localParticipant: this, publication: publication);
                },
                () => // label:
                {
                    return $"room.didUnpublish {publication}";
                });
                return UniTask.CompletedTask;
            }).Unwrap();
        }

        var engine = this.Room.engine;

        // remove the publication
        _state.Mutate(state => { state.tracks.Remove(publication.sid); return state; });

        // if track is nil, only notify unpublish and return
        var track = publication.Track as LocalTrack;
        if (track == null)
        {
            await NotifyDidUnpublish();
            return;
        }

        // build a conditional promise to stop track if required by option
        async UniTask<bool> StopTrackIfRequired()
        {
            if (Room._state.Value.options.stopLocalTrackOnUnpublish)
            {
                return await track.Stop();
            }
            // Do nothing
            return false;
        }

        // wait for track to stop (if required)
        // engine.publisher must be accessed from engine.queue

        await StopTrackIfRequired();
        await engine.queue.Async(async () =>
        {
            var publisher = engine.Publisher;
            var sender = track.Sender;

            if (publisher == null || sender == null)
            {
                return;
            }

            await publisher.RemoveTrack(sender);

            await queue.Async(async () =>
            {
                await engine.PublisherShouldNegotiate();
            });
        });

        await queue.Async(async () =>
        {
            await track.OnUnpublish();
            await NotifyDidUnpublish();
        });
    }

    /**
    publish data to the other participants in the room

    Data is forwarded to each participant in the room. Each payload must not exceed 15k.
    - Parameter data: Data to send
    - Parameter reliability: Toggle between sending relialble vs lossy delivery.
    For data that you need delivery guarantee (such as chat messages), use Reliable.
    For data that should arrive as quickly as possible, but you are ok with dropped packets, use Lossy.
    - Parameter destination: SIDs of the participants who will receive the message. If empty, deliver to everyone
    */
    public async UniTask PublishData(ByteString data,
                                     Reliability reliability = Reliability.Reliable,
                                     List<string> destination = null)
    {
        if (destination == null) { destination = new List<string>(); }

        var userPacket = new UserPacket
        {
            //DestinationSids = destination,
            Payload = data,
            ParticipantSid = this.sid
        };
        userPacket.DestinationSids.Add(destination);

        await Room.engine.Send(userPacket: userPacket,
                               reliability: reliability);

        return;
    }

    /**
     * Control who can subscribe to LocalParticipant's published tracks.
     *
     * By default, all participants can subscribe. This allows fine-grained control over
     * who is able to subscribe at a participant and track level.
     *
     * Note: if access is given at a track-level (i.e. both ``allParticipantsAllowed`` and
     * ``ParticipantTrackPermission/allTracksAllowed`` are false), any newer published tracks
     * will not grant permissions to any participants and will require a subsequent
     * permissions update to allow subscription.
     *
     * - Parameter allParticipantsAllowed Allows all participants to subscribe all tracks.
     *  Takes precedence over ``participantTrackPermissions`` if set to true.
     *  By default this is set to true.
     * - Parameter participantTrackPermissions Full list of individual permissions per
     *  participant/track. Any omitted participants will not receive any permissions.
     */
    public async UniTask SetTrackSubscriptionPermissions(bool allParticipantsAllowed,
                                                         List<ParticipantTrackPermission> trackPermissions = null)
    {
        this.allParticipantsAllowed = allParticipantsAllowed;
        this.trackPermissions = trackPermissions;

        await SendTrackSubscriptionPermissions();
        return;
    }

    internal UniTask SendTrackSubscriptionPermissions()
    {
        if (Room.engine._state.Value.connectionState.State != ConnectionState.States.Connected)
        {
            return UniTask.CompletedTask;
        }

        return Room.engine.signalClient.SendUpdateSubscriptionPermission(allParticipants: allParticipantsAllowed,
                                                                         trackPermissions: trackPermissions.ToArray());
    }

    internal void OnSubscribedQualitiesUpdate(string trackSid, SubscribedQuality[] subscribedQualities)
    {
        if (!Room._state.Value.options.dynacast) { return; }

        var pub = GetLocalTrackPublication(sid: trackSid);
        var track = pub.Track as LocalVideoTrack;
        var sender = track.Transceiver?.Sender;

        if (sender is null) { return; }

        var parameters = sender.GetParameters();
        var encodings = parameters.encodings;

        var hasChanged = false;
        foreach (var quality in subscribedQualities)
        {
            string rid;
            switch (quality.Quality)
            {
                case PB.VideoQuality.High: rid = "f"; break;
                case PB.VideoQuality.Medium: rid = "h"; break;
                case PB.VideoQuality.Low: rid = "q"; break;
                default: continue;
            }

            var encoding = encodings.First(value => value.rid == rid);
            if (encoding is null) { continue; }

            if (encoding.active != quality.Enabled)
            {
                hasChanged = true;
                encoding.active = quality.Enabled;
                Debug.Log($"setting layer {quality.Quality} to {quality.Enabled}");
            }
        }

        // Non simulcast streams don't have rids, handle here.
        if (encodings.Length == 1 && subscribedQualities.Length >= 1)
        {
            var encoding = encodings[0];
            var quality = subscribedQualities[0];

            if (encoding.active != quality.Enabled)
            {
                hasChanged = true;
                encoding.active = quality.Enabled;
                Debug.Log($"setting layer {quality.Quality} to {quality.Enabled}");
            }
        }

        if (hasChanged)
        {
            sender.SetParameters(parameters);
        }
    }

    internal override bool SetPermissions(ParticipantPermissions newValue)
    {
        var didUpdate = base.SetPermissions(newValue);

        if (didUpdate)
        {
            this.Notify((iParticipantDelegate) =>
            {
                iParticipantDelegate.DidUpdate(this, permissions: newValue);
            },
            () => // label:
            {
                return $"participant.didUpdate permissions: {JsonConvert.SerializeObject(newValue)}";
            });

            this.Room.Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidUpdate(this.Room, participant: this, permissions: newValue);
            },
            () => // label:
            {
                return $"room.didUpdate permissions: {JsonConvert.SerializeObject(newValue)}";
            });
        }

        return didUpdate;
    }
}

// - Session Migration

public partial class LocalParticipant
{
    internal List<TrackPublishedResponse> PublishedTracksInfo()
    {
        return _state.Value.tracks.Values
                .Where(v => v.Track != null)
                .Select(publication =>
                new TrackPublishedResponse
                {
                    Cid = publication.Track.MediaTrack.Id,
                    Track = publication.LatestInfo ?? default
                }).ToList();
    }

    internal async UniTask RepublishTracks()
    {
        var mediaTracks = _state.Value.tracks.Values
            .Select(v => v.Track)
            .Where(track => track != null);

        await UnpublishAll();

        var promises = mediaTracks
                .Where(track => track is LocalTrack && track.muted)
                .Select(track =>
                {
                    var localTrack = track as LocalTrack;
                    return this.Publish(track: localTrack, publishOptions: localTrack.publishOptions);
                });

        await queue.Async(async () =>
        {
            await UniTask.WhenAll(promises);
        });
    }
}

// - Simplified API

public partial class LocalParticipant
{
    public async UniTask<LocalTrackPublication> SetCamera(bool enabled)
    {
        return await Set(Source.Camera, enabled);
    }

    public async UniTask<LocalTrackPublication> SetMicrophone(bool enabled)
    {
        return await Set(Source.Microphone, enabled, audioSource: Room.MicAudioSource);
    }

    public async UniTask<LocalTrackPublication> SetScreenShare(bool enabled, Camera screenCamera)
    {
        return await Set(Source.ScreenShareVideo, enabled, screenCamera: screenCamera);
    }

    public UniTask<LocalTrackPublication> Set(Track.Source source,
                                              bool enabled,
                                              AudioSource audioSource = null,
                                              Camera screenCamera = null)
    {
        // attempt to get existing publication
        var publication = GetTrackPublication(source: source) as LocalTrackPublication;
        if (publication != null)
        {
            if (enabled)
            {
                return queue.Async<UniTask<LocalTrackPublication>>(async () =>
                {
                    await publication.Unmute();
                    return publication;
                }).Unwrap();
            }
            else
            {
                return queue.Async<UniTask<LocalTrackPublication>>(async () =>
                {
                    await publication.Mute();
                    return publication;
                }).Unwrap();
            }
        }
        else if (enabled)
        {
            // try to create a new track
            switch (source)
            {
                case Source.Camera:
                    {
                        var options = Room._state.Value.options.defaultCameraCaptureOptions;
                        var capturer = new WebCamCapturer(options);

                        if (capturer.MWebCamTexture == null)
                        {
                            Debug.LogError("capturer.MWebCamTexture == null");
                            return UniTask.Create<LocalTrackPublication>(() => { return new(null); });
                        }

                        return queue.Async<UniTask<LocalTrackPublication>>(async () =>
                        {
                            await UniTask.SwitchToMainThread();
                            await UniTask.WaitUntil(() => capturer.MWebCamTexture.didUpdateThisFrame);

                            var localTrack = new LocalVideoTrack(Track.CameraName,
                                                                 Source.Camera,
                                                                 capturer);

                            var published = await PublishVideoTrack(track: localTrack);
                            return published;
                        }).Unwrap();
                    }

                case Source.Microphone:
                    {
                        if (audioSource == null)
                        {
                            Debug.LogError("unityAudioSource == null");
                            return UniTask.Create<LocalTrackPublication>(() => { return new(null); });
                        }

                        var options = Room._state.Value.options.defaultAudioCaptureOptions;
                        var localTrack = LocalAudioTrack.CreateTrack(audioSource, options: options);

                        return queue.Async<UniTask<LocalTrackPublication>>(async () =>
                        {
                            var published = await PublishAudioTrack(track: localTrack);
                            return published;
                        }).Unwrap();
                    }

                case Source.ScreenShareVideo:
                    {
                        if (screenCamera == null)
                        {
                            Debug.LogError("screenCamera == null");
                            return UniTask.Create<LocalTrackPublication>(() => { return new(null); });
                        }

                        var options = Room._state.Value.options.defaultScreenShareCaptureOptions;
                        var localTrack = LocalVideoTrack.CreateScreenCapturerTrack(screenCamera, options: options);

                        return queue.Async<UniTask<LocalTrackPublication>>(async () =>
                        {
                            var published = await PublishVideoTrack(track: localTrack);
                            return published;
                        }).Unwrap();
                    }

                case Source.ScreenShareAudio:
                    //NOTE:Thomas: not implemented in swift code
                    break;

                default:
                    break;
            }
        }

        return UniTask.Create<LocalTrackPublication>(() => { return new(null); });
    }
}
