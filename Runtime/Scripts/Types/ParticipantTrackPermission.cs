using LiveKit.Proto;

public partial struct ParticipantTrackPermission {
    /**
     * The participant id this permission applies to.
     */
    string ParticipantSid;

    /**
     * If set to true, the target participant can subscribe to all tracks from the local participant.
     *
     * Takes precedence over ``allowedTrackSids``.
     */
    bool AllTracksAllowed;

    /**
     * The list of track ids that the target participant can subscribe to.
     */
    string[] AllowedTrackSids;

    public ParticipantTrackPermission(string participantSid,
                                      bool allTracksAllowed,
                                      string[] allowedTrackSids = null)
    {
        this.ParticipantSid = participantSid;
        this.AllTracksAllowed = allTracksAllowed;
        this.AllowedTrackSids = allowedTrackSids ?? new string[0];
    }
}

public partial struct ParticipantTrackPermission
{
    internal TrackPermission ToPBType()
    {
        TrackPermission permissson = new()
        {
            ParticipantSid = ParticipantSid,
            AllTracks = AllTracksAllowed,
        };

        permissson.TrackSids.Add(AllowedTrackSids);
        return permissson;
    }
}