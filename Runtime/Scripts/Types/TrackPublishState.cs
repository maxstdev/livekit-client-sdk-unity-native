using System;

// TODO:Thomas:필수: enum Associated Values
/// A enum that represents the published state of a ``LocalTrackPublication``.
public enum TrackPublishState {
    /// Not published yet, has been unpublished, or an error occured while publishing or un-publishing.
    /// `error` wil be non-nil if an error occurred while publishing.
    NotPublished,   //(error: Error? = nil);
    /// In the process of publishing or unpublishing.
    Busy,           //(isPublishing: Bool = true)
    /// Sucessfully published.
    Published       //(LocalTrackPublication)
}

/// Convenience extension for ``TrackPublishState``.
public static class TrackPublishStateExtension {
    /// Checks whether the state is ``TrackPublishState/published(_:)`` regardless of the error value.
    public static bool isPublished(this TrackPublishState trackPublishState)
    {
        return trackPublishState switch
        {
            TrackPublishState.Published => true,
            _ => false
        };
    }

    /// Checks whether the state is ``TrackPublishState/busy(isPublishing:)`` regardless of the `isPublishing` value.
    public static bool isBusy(this TrackPublishState trackPublishState)
    {
        return trackPublishState switch
        {
            TrackPublishState.Busy => true,
            _ => false
        };
    }
}