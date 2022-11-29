
public interface ITrackDelegate
{
    /// Dimensions of the video track has updated
    void DidUpdate(VideoTrack track, Dimensions? dimensions) { }
    /// A ``VideoView`` was attached to the ``VideoTrack``
    //void DidAttach(VideoTrack track, VideoView videoView) { }   // NOTE:Thomas: There is nothing to use until Unity implements VideoView.
    /// A ``VideoView`` was detached from the ``VideoTrack``
    //void DidDetach(VideoTrack track, VideoView videoView) { }   // NOTE:Thomas: There is nothing to use until Unity implements VideoView.
    /// ``Track/muted`` has updated.
    void DidUpdate(Track track, bool muted, bool shouldSendSignal) { }
    /// Statistics for the track has been generated.
    void DidUpdate(Track track, TrackStats stats) { }
}