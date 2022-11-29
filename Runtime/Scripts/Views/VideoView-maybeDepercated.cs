using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC;
using System;
using System.Drawing;

///// A ``NativeViewType`` that conforms to ``RTCVideoRenderer``.
//public typealias NativeRendererView = NativeViewType & RTCVideoRenderer

// HACK:Thomas: 차후 VideoView => CameraBackgroundBehaviour 변경 필요
// swift : public class VideoView: NativeView, MulticastDelegateCapable, Loggable {
public partial class VideoView
{
    // - Static

    //private static let mirrorTransform = CATransform3DMakeScale(-1.0, 1.0, 1.0)
    private const double _freezeDetectThreshold = 2.0;

    // - Public

    //public typealias DelegateType = VideoViewDelegate
    //public var delegates = MulticastDelegate<DelegateType>()

    // swift : public enum LayoutMode : String, Codable, CaseIterable {
    /// Specifies how to render the video withing the ``VideoView``'s bounds.
    public enum LayoutMode
    {
        /// Video will be fully visible within the ``VideoView``.
        Fit,
        /// Video will fully cover up the ``VideoView``.
        Fill
    }

    // swift : public enum MirrorMode : String, Codable, CaseIterable {
    public enum MirrorMode
    {
        /// Will mirror if the track is a front facing camera track.
        Auto,
        Off,
        Mirror
    }

    /// ``LayoutMode-swift.enum`` of the ``VideoView``.
    public LayoutMode layoutMode
    {
        get => _state.Value.LayoutMode;
        set => _state.Mutate(t => { t.LayoutMode = value; return t; });
    }

    /// Flips the video horizontally, useful for local VideoViews.
    public MirrorMode mirrorMode
    {
        get => _state.Value.MirrorMode;
        set => _state.Mutate(t => { t.MirrorMode = value; return t; });
    }

    // TODO:Thomas: VideoRotation 대안 후
    ///// Force video to be rotated to preferred ``VideoRotation``
    ///// Currently, only for iOS.
    //public var rotationOverride: VideoRotation? {
    //    get { _state.rotationOverride }
    //    set { _state.mutate { $0.rotationOverride = newValue } }
    //}

    /// Calls addRenderer and/or removeRenderer internally for convenience.
    public WeakReference<VideoTrack> Track
    {
        get => _state.Value.Track;

        set => _state.Mutate((t) =>
        {
            if (t.Track.TryGetTarget(out VideoTrack track)
                && value.TryGetTarget(out VideoTrack newTrack))
            {
                if (!TrackIsEqualWith(track, newTrack))
                {
                    t.RenderDate = null;
                    t.DidRenderFirstFrame = false;
                    t.IsRendering = false;
                    t.RendererSize = null;
                }
            }

            t.Track = value;

            return t;
        });
    }

    /// If set to false, rendering will be paused temporarily. Useful for performance optimizations with UICollectionViewCell etc.
    public bool IsEnabled
    {
        get => _state.Value.IsEnabled;
        set => _state.Mutate(t => { t.IsEnabled = value; return t; });
    }

    public bool IsHidden
    {
        get => _state.Value.IsHidden;
        set
        {
            _state.Mutate(t => { t.IsHidden = value; return t; });

            DispatchQueue.MainSafeAsync(() =>
            {
                IsHidden = value;
            });
        }
    }

    public bool DebugMode
    {
        get => _state.Value.DebugMode;
        set => _state.Mutate(t => { t.DebugMode = value; return t; });
    }

    public bool IsRendering => _state.Value.IsRendering;
    public bool DidRenderFirstFrame => _state.Value.DidRenderFirstFrame;
    
    // - Internal
    internal struct State
    {
        internal WeakReference<VideoTrack> Track;
        internal bool IsEnabled; //= true;
        internal bool IsHidden; //= false;

        // layout related
        internal Vector2 ViewSize;
        internal Vector2? RendererSize;
        internal bool DidLayout; //= false
        internal LayoutMode LayoutMode; //= .fill
        internal MirrorMode MirrorMode; //= .auto
        // TODO: VideoRotation 대안 후 
        //var rotationOverride: VideoRotation?

        internal bool DebugMode; //= false

        // render states
        internal DateTime? RenderDate;
        internal bool DidRenderFirstFrame; //= false
        internal bool IsRendering; //= false

        State(int dummy = 0)
        {
            this.Track = default;
            this.IsEnabled = true;
            this.IsHidden = false;

            // layout related
            this.ViewSize = default;
            this.RendererSize = default;
            this.DidLayout = false;
            this.LayoutMode = LayoutMode.Fill;
            this.MirrorMode = MirrorMode.Auto;

            this.DebugMode = false;

            // render states
            this.RenderDate = default;
            this.DidRenderFirstFrame = false;
            this.IsRendering = false;
        }
    }

    internal StateSync<State> _state;
}

public partial class VideoView : IVideoRenderer
{
    //public async UniTask<bool> AdaptiveStreamIsEnabled()
    //{
    //    return await _state.Read<bool>((state) => {
    //        return state.DidLayout && !state.IsHidden && state.IsEnabled;
    //    });
    //}

    public bool AdaptiveStreamIsEnabled => throw new NotImplementedException();

    public SizeF AdaptiveStreamSize => throw new NotImplementedException();

    // for dummy
    public bool IsVisible => throw new NotImplementedException();

    //public var adaptiveStreamIsEnabled: Bool {
    //    _state.read { $0.didLayout && !$0.isHidden && $0.isEnabled }
    //}


    //public var adaptiveStreamSize: CGSize
    //{
    //    _state.rendererSize ?? .zero
    //    }

    //public func setSize(_ size: CGSize) {
    //    guard let nr = nativeRenderer else { return }
    //    nr.setSize(size)
    //    }

    //public func renderFrame(_ frame: RTCVideoFrame ?) {

    //    let state = _state.readCopy()

    //        // prevent any extra rendering if already !isEnabled etc.
    //        guard state.shouldRender, let nr = nativeRenderer else
    //    {
    //        log("canRender is false, skipping render...")
    //            return
    //        }

    //    var _needsLayout = false
    //        defer {
    //        if _needsLayout {
    //            DispatchQueue.main.async {
    //                [weak self] in
    //                    guard let self = self else { return }
    //                self.setNeedsLayout()
    //                }
    //        }
    //    }

    //    if let frame = frame {

    //#if os(iOS)
    //            let rotation = state.rotationOverride ?? frame.rotation
    //# elseif os(macOS)
    //            let rotation = frame.rotation
    //#endif

    //        let dimensions = Dimensions(width: frame.width,
    //                                    height: frame.height)
    //            .apply(rotation: rotation)

    //            guard dimensions.isRenderSafe else
    //        {
    //            log("skipping render for dimension \(dimensions)", .warning)
    //                // renderState.insert(.didSkipUnsafeFrame)
    //                return
    //            }

    //        if track?.set(dimensions: dimensions) == true {
    //            _needsLayout = true
    //            }

    //    } else
    //    {
    //        if track?.set(dimensions: nil) == true {
    //            _needsLayout = true
    //            }
    //    }

    //    nr.renderFrame(frame)

    //        // cache last rendered frame
    //        track?.set(videoFrame: frame)

    //        _state.mutateAsync {
    //            $0.didRenderFirstFrame = true
    //            $0.isRendering = true
    //            $0.renderDate = Date()
    //        }
    //}
}

// - Internal

public partial class VideoView
{
    internal static bool TrackIsEqualWith(VideoTrack track1, VideoTrack track2)
    {
        // equal if both tracks are nil
        if ((track1 == null) && (track2 == null)) { return true; }

        // not equal if a single track is nil
        if ((track1 == null) || (track2 == null)) { return false; }

        // use isEqual
        return track1.Equals(track2);
    }
}
