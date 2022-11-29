using Google.Protobuf;
using System;

///<summary>
/// Delegate methods for a participant.
///
/// Since ``Participant`` inherits from ``MulticastDelegate``,
/// you can call `add(delegate:)` on ``Participant`` to add as many delegates as you need.
/// All delegate methods are optional.
///
/// To ensure each participant's delegate is registered, you can look through ``Room/localParticipant`` and ``Room/remoteParticipants`` on connect
/// and register it on new participants inside ``RoomDelegate/room(_:participantDidJoin:)-9bkm4``
///</summary>
public interface IParticipantDelegate
{
    /// A ``Participant``'s metadata has updated.
    /// `participant` Can be a ``LocalParticipant`` or a ``RemoteParticipant``.
    void DidUpdate(Participant participant, string metadata) { }

    /// The isSpeaking status of a ``Participant`` has changed.
    /// `participant` Can be a ``LocalParticipant`` or a ``RemoteParticipant``.
    void DidUpdate(Participant participant, bool speaking) { }

    /// The connection quality of a ``Participant`` has updated.
    /// `participant` Can be a ``LocalParticipant`` or a ``RemoteParticipant``.
    void DidUpdate(Participant participant, ConnectionQuality connectionQuality) { }

    /// `muted` state has updated for the ``Participant``'s ``TrackPublication``.
    ///
    /// For the ``LocalParticipant``, the delegate method will be called if setMute was called on ``LocalTrackPublication``,
    /// or if the server has requested the participant to be muted.
    ///
    /// `participant` Can be a ``LocalParticipant`` or a ``RemoteParticipant``.
    void DidUpdate(Participant participant, TrackPublication publication, bool muted) { }

    void DidUpdate(Participant participant, ParticipantPermissions permissions) { }

    /// ``RemoteTrackPublication/streamState`` has updated for the ``RemoteParticipant``.
    void DidUpdate(RemoteParticipant participant, RemoteTrackPublication publication, StreamState streamState) { }

    /// ``RemoteTrackPublication/subscriptionAllowed`` has updated for the ``RemoteParticipant``.
    void DidUpdate(RemoteParticipant participant, RemoteTrackPublication publication, bool permissionAllowed) { }

    /// When a new ``RemoteTrackPublication`` is published to ``Room`` after the ``LocalParticipant`` has joined.
    ///
    /// This delegate method will not be called for tracks that are already published.
    void DidPublish(RemoteParticipant participant, RemoteTrackPublication publication) { }

    /// The ``RemoteParticipant`` has unpublished a ``RemoteTrackPublication``.
    void DidUnpublish(RemoteParticipant participant, RemoteTrackPublication publication) { }

    /// The ``LocalParticipant`` has subscribed to a new ``RemoteTrackPublication``.
    ///
    /// This event will always fire as long as new tracks are ready for use.
    void DidSubscribe(RemoteParticipant participant, RemoteTrackPublication publication, Track track) { }

    /// Could not subscribe to a track.
    ///
    /// This is an error state, the subscription can be retried.
    void DidFailToSubscribe(RemoteParticipant participant, string trackSid, Exception error) { }

    /// Unsubscribed from a ``RemoteTrackPublication`` and  is no longer available.
    ///
    /// Clients should listen to this event and handle cleanup.
    void DidUnsubscribe(RemoteParticipant participant, RemoteTrackPublication publication, Track track) { }

    /// Data was received from a ``RemoteParticipant``.
    void DidReceive(RemoteParticipant participant, ByteString data) { }


    /// The ``LocalParticipant`` has published a ``LocalTrackPublication``.
    void LocalParticipantDidPublish(LocalParticipant participant, LocalTrackPublication publication) { }

    /// The ``LocalParticipant`` has unpublished a ``LocalTrackPublication``.
    void LocalParticipantDidUnpublish(LocalParticipant participant, LocalTrackPublication publication) { }
}