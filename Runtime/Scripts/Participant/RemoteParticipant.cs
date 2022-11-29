using Cysharp.Threading.Tasks;
using LiveKit.Proto;
using System;
using System.Collections.Generic;
using System.Linq;
using UniLiveKit.ErrorException;
using Unity.WebRTC;
using UnityEngine;
using Sid = System.String;

public class RemoteParticipant : Participant
{
    public RemoteParticipant(Sid sid, ParticipantInfo info, Room room)
        : base(sid, info?.Identity ?? "", info?.Name ?? "", room)
    {
        if (info != null)
        {
            UpdateFromInfo(info: info);
        }
    }

    public RemoteTrackPublication GetRemoteTrackPublication(Sid sid)
    {
        if (_state.Value.tracks.TryGetValue(sid, out var track) && track is RemoteTrackPublication trackPublication)
        {
            return trackPublication;
        }

        return null;

        //return _state.Value.tracks[sid] as RemoteTrackPublication;
    }

    internal override void UpdateFromInfo(ParticipantInfo info)
    {
        base.UpdateFromInfo(info: info);

        var validTrackPublications = new Dictionary<string, RemoteTrackPublication>();
        var newTrackPublications = new Dictionary<string, RemoteTrackPublication>();

        foreach (var trackInfo in info.Tracks)
        {
            var publication = GetRemoteTrackPublication(sid: trackInfo.Sid);
            if (publication == null)
            {
                publication = new RemoteTrackPublication(info: trackInfo, participant: this);
                newTrackPublications[trackInfo.Sid] = publication;
                AddTrack(publication: publication!);
            }
            else
            {
                publication!.UpdateFromInfo(info: trackInfo);
            }
            validTrackPublications[trackInfo.Sid] = publication!;
        }

        Room.engine.ExecuteIfConnected(() =>
        {
            foreach (var publication in newTrackPublications.Values)
            {
                this.Notify((iParticipantDelegate) =>
                {
                    iParticipantDelegate.DidPublish(this, publication: publication);
                },
                () => // label:
                {
                    return $"participant.didPublish {publication}";
                });

                this.Room.Notify((iRoomDelegate) =>
                {
                    iRoomDelegate.DidPublish(this.Room, participant: this, publication: publication);
                },
                () => // label:
                {
                    return $"room.didPublish {publication}";
                });
            }
        });

        var unpublishPromises = _state.Value.tracks.Values
            .Where(pub => !validTrackPublications.TryGetValue(pub.sid, out var _))
            .Select(pub => pub as RemoteTrackPublication)
            .Where(rPub => rPub != null)
            .Select(rPub => Unpublish(publication: rPub));

        queue.Async(async () =>
        {
            try
            {
                await UniTask.WhenAll(unpublishPromises);

            }
            catch (Exception error)
            {
                Debug.Log("Failed to unpublish with error: " + error);
            }
        });
    }

    internal async UniTask AddSubscribedMediaTrack(MediaStreamTrack rtcTrack, Sid sid)
    {
        Track track;

        var publication = GetRemoteTrackPublication(sid: sid);
        if (publication == null)
        {
            Debug.LogError($"Could not subscribe to mediaTrack {sid}, unable to locate track publication. existing sids: {String.Join(", ", _state.Value.tracks.Keys)}");

            var error = new EnumException<TrackError>(TrackError.State, $"Could not find published track with sid: {sid}");

            this.Notify((iParticipantDelegate) =>
            {
                iParticipantDelegate.DidFailToSubscribe(this, trackSid: sid, error: error);
            },
            () => // label:
            {
                return $"participant.didFailToSubscribe trackSid: {sid}";
            });

            this.Room.Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidFailToSubscribe(this.Room, participant: this, trackSid: sid, error: error);
            },
            () => // label:
            {
                return $"room.didFailToSubscribe trackSid: {sid}";
            });

            throw error;
        }

        switch (rtcTrack.Kind)
        {
            case TrackKind.Audio:
                track = new RemoteAudioTrack(name: publication.Name,
                                             source: publication.Source,
                                             track: rtcTrack);
                break;

            case TrackKind.Video:
                track = new RemoteVideoTrack(name: publication.Name,
                                             source: publication.Source,
                                             track: rtcTrack);
                break;

            default:
                var error = new EnumException<TrackError>(TrackError.Type, $"Unsupported type: {rtcTrack.Kind.ToString()}");

                this.Notify((iParticipantDelegate) =>
                {
                    iParticipantDelegate.DidFailToSubscribe(this, trackSid: sid, error: error);
                },
                () => // label:
                {
                    return $"participant.didFailToSubscribe trackSid: {sid}";
                });

                this.Room.Notify((iRoomDelegate) =>
                {
                    iRoomDelegate.DidFailToSubscribe(this.Room, participant: this, trackSid: sid, error: error);
                },
                () => // label:
                {
                    return $"room.didFailToSubscribe trackSid: {sid}";
                });

                throw error;
        }

        publication.SetTrack(track);
        publication.SetSubscriptionAllowed(true);
        track._state.Mutate(state => { state.sid = publication.sid; return state; });

        AddTrack(publication: publication);

        await queue.Async(async () =>
        {
            await track.Start();

            Notify((iParticipantDelegate) =>
            {
                iParticipantDelegate.DidSubscribe(this, publication, track);
            },
            () =>
            {
                return "participant.didSubScribe : " + publication;
            });

            Room.Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidSubscribe(this.Room, this, publication, track);
            },
            () =>
            {
                return "room.didSubscribe " + publication;
            });
        });
    }

    internal override async UniTask CleanUp(bool notify = true)
    {
        await base.CleanUp(notify);
        await queue.Sync(async () =>
        {
            this.Room.Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidLeave(this.Room, participant: this);
            },
            () =>
            {
                return $"room.participantDidLeave";
            });
            await UniTask.CompletedTask;
            return;
        });
    }

    public override UniTask UnpublishAll(bool notify = true)
    {
        // build a list of promises
        var promises = _state.Value.tracks.Values
            .Select(pub => pub as RemoteTrackPublication)
            .Where(rPub => rPub != null)
            .ToList()
            .Select(rPub => Unpublish(publication: rPub, notify: notify))
            .ToList();

        return UniTask.WhenAll(promises);
    }

    internal async UniTask Unpublish(RemoteTrackPublication publication, bool notify = true)
    {
        UniTask NotifyUnpublish()
        {
            return queue.Async(() =>
            {
                if (!notify)
                {
                    return UniTask.CompletedTask;
                }

                return UniTask.Create(() =>
                {
                    this.Notify((iParticipantDelegate) =>
                    {
                        iParticipantDelegate.DidUnpublish(this, publication: publication);
                    },
                    () =>
                    {
                        return $"participant.didUnpublish {publication}";
                    });

                    this.Room.Notify((iRoomDelegate) =>
                    {
                        iRoomDelegate.DidUnpublish(this.Room, participant: this, publication: publication);
                    },
                    () =>
                    {
                        return $"room.didUnpublish {publication}";
                    });
                    return UniTask.CompletedTask;
                });
            }).Unwrap();
        }

        // remove the publication
        _state.Mutate(state => { state.tracks.Remove(publication.sid); return state; });

        // continue if the publication has a track
        var track = publication.Track;

        if (track == null)
        {
            // if track is null, only notify unpublish
            await NotifyUnpublish();
            return;
        }

        await queue.Async(async () =>
        {
            await track.Stop();

            if (!notify)
            {
                return;
            }

            this.Notify((iParticipantDelegate) =>
            {
                iParticipantDelegate.DidUnsubscribe(this, publication, track);
            }, () =>
            {
                return $"participant.didUnsubscribe {publication}";
            });

            this.Room.Notify((iRoomDelegate) =>
            {
                iRoomDelegate.DidUnsubscribe(this.Room, participant: this, publication, track);
            },
            () =>
            {
                return $"room.didUnsubscribe {publication}";
            });

            await NotifyUnpublish();
        });
    }
}
