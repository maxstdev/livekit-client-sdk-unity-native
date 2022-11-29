using Google.Protobuf;
using LiveKit.Proto;
using System;
using System.Threading;

/// <summary>
/// ``RoomDelegate`` receives room events as well as ``Participant`` events.
///
/// > Important: The thread which the delegate will be called on, is not guranteed to be the `main` thread.
/// If you will perform any UI update from the delegate, ensure the execution is from the `main` thread.
/// </summary>
public interface IRoomDelegate
{
    /// Successfully connected to the room.
    void DidConnect(Room room, bool isReconnect) { }

    /// Could not connect to the room.
    void DidFailToConnect(Room room, Exception error) { }

    /// Client disconnected from the room unexpectedly.
    void DidDisconnect(Room room, Exception error) { }

    /// When the ``ConnectionState`` has updated.
    void DidUpdate(Room room, ConnectionState connectionState, ConnectionState oldValue) { }

    /// When a ``RemoteParticipant`` joins after the ``LocalParticipant``.
    /// It will not emit events for participants that are already in the room.
    void DidJoin(Room room, RemoteParticipant participant) { }

    /// When a ``RemoteParticipant`` leaves after the ``LocalParticipant`` has joined.
    void DidLeave(Room room, RemoteParticipant participant) { }

    /// Active speakers changed.
    ///
    /// List of speakers are ordered by their ``Participant/audioLevel``, loudest speakers first.
    /// This will include the ``LocalParticipant`` too.
    void DidUpdate(Room room, Participant[] speakers) { }

    /// ``Room``'s metadata has been updated.
    void DidUpdate(Room room, string metadata) { }

    /// Same with ``ParticipantDelegate/participant(_:didUpdate:)-46iut``.
    void DidUpdate(Room room, Participant participant, string metadata) { }

    /// Same with ``ParticipantDelegate/participant(_:didUpdate:)-7zxk1``.
    void DidUpdate(Room room, Participant participant, ConnectionQuality connectionQuality) { }

    /// Same with ``ParticipantDelegate/participant(_:didUpdate:)-84m89``
    void DidUpdate(Room room, Participant participant, TrackPublication publication, bool muted) { }

    void DidUpdate(Room room, Participant participant, ParticipantPermissions permissions) { }

    /// Same with ``ParticipantDelegate/participant(_:didUpdate:streamState:)-1lu8t``.
    void DidUpdate(Room room, RemoteParticipant participant, RemoteTrackPublication publication, StreamState streamState) { }

    /// Same with ``ParticipantDelegate/participant(_:didPublish:)-60en3``.
    void DidPublish(Room room, RemoteParticipant participant, RemoteTrackPublication publication) { }

    /// Same with ``ParticipantDelegate/participant(_:didUnpublish:)-3bkga``.
    void DidUnpublish(Room room, RemoteParticipant participant, RemoteTrackPublication publication) { }

    /// Same with ``ParticipantDelegate/participant(_:didSubscribe:track:)-7mngl``.
    void DidSubscribe(Room room, RemoteParticipant participant, RemoteTrackPublication publication, Track track) { }

    /// Same with ``ParticipantDelegate/participant(_:didFailToSubscribe:error:)-10pn4``.
    void DidFailToSubscribe(Room room, RemoteParticipant participant, string trackSid, Exception error) { }

    /// Same with ``ParticipantDelegate/participant(_:didUnsubscribe:track:)-3ksvp``.
    void DidUnsubscribe(Room room, RemoteParticipant participant, RemoteTrackPublication publication, Track track) { }

    /// Same with ``ParticipantDelegate/participant(_:didReceive:)-2t55a``
    /// participant could be nil if data was sent by server api.
    void DidReceive(Room room, RemoteParticipant participant, ByteString data) { }

    /// Same with ``ParticipantDelegate/localParticipant(_:didPublish:)-90j2m``.
    void DidPublish(Room room, LocalParticipant localParticipant, LocalTrackPublication publication) { }

    /// Same with ``ParticipantDelegate/participant(_:didUnpublish:)-3bkga``.
    void DidUnpublish(Room room, LocalParticipant localParticipant, LocalTrackPublication publication) { }

    /// Same with ``ParticipantDelegate/participant(_:didUpdate:permission:)``.
    void DidUpdate(Room room, RemoteParticipant participant, RemoteTrackPublication publication, bool permissionAllowed) { }
}