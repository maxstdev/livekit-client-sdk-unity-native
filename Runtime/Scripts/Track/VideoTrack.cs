using System.Drawing;
using Unity.WebRTC;
using UnityEngine;

public partial class VideoTrack : Track
{
    internal VideoTrack(string name, Kind kind, Source source, MediaStreamTrack track)
        : base(name, kind, source, track)
    { }
}

// swift: @objc public protocol VideoRenderer: RTCVideoRenderer
public interface IVideoRenderer
{
    /// Whether this ``VideoRenderer`` should be considered visible or not for AdaptiveStream.
    /// This will be invoked on the .main thread.
    bool AdaptiveStreamIsEnabled { get; }
    /// The size used for AdaptiveStream computation. Return .zero if size is unknown yet.
    /// This will be invoked on the .main thread.
    SizeF AdaptiveStreamSize { get; }

    // dummy 
    bool IsVisible { get; }
}