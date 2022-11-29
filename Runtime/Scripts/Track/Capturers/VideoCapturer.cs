using System;
using UnityEngine;
using Unity.WebRTC;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

public interface VideoCapturerDelegate
{
    void Capturer(VideoCapturer capturer, Dimensions? didUpdateDimensions) { }
    void Capturer(VideoCapturer capturer, VideoCapturer.CapturerState didUpdateState) { }
}

// Intended to be a base class for video capturers
public class VideoCapturer : MulticastDelegate<VideoCapturerDelegate>
{
    // TODO:Thomas:선택: 필요하면 Unity RenderTextureFormat로 하는게...
    //public static let supportedPixelFormats = DispatchQueue.webRTC.sync { RTCCVPixelBuffer.supportedPixelFormats()

    public static Int64 CreateTimeStampNs()
    {
        double systemTime = System.Diagnostics.Stopwatch.GetTimestamp();
        double nanoseconds = 1_000_000_000.0 * systemTime / System.Diagnostics.Stopwatch.Frequency;

        return (Int64)nanoseconds;
    }

    public enum CapturerState
    {
        Stopped,
        Started
    }

    internal struct State
    {
        internal Completer<Dimensions> dimensionsCompleter;

        internal State(Completer<Dimensions> completer = null)
        {
            dimensionsCompleter = completer ?? new Completer<Dimensions>();
        }
    }

    internal StateSync<State> _state = new(new State(null));

    private Dimensions? _dimensions;
    public Dimensions? dimensions
    {
        get => _dimensions;

        internal set
        {
            var oldValue = _dimensions;
            _dimensions = value;

            // didSet

            if (oldValue == _dimensions) { return; }

            Debug.Log($"[publish] {JsonConvert.SerializeObject(oldValue)} -> {JsonConvert.SerializeObject(_dimensions)}");
            Notify((dlgt) => { dlgt.Capturer(this, didUpdateDimensions: _dimensions); });

            Debug.Log($"[publish] dimensions: {JsonConvert.SerializeObject(_dimensions)}");
            _state.Value.dimensionsCompleter.Set(_dimensions);
        }
    }

    public CapturerState captureState { get; private set; } = CapturerState.Stopped;

    // returns true if state updated
    public virtual async UniTask<bool> StartCapture()
    {
        await UniTask.Yield();  // CS1998

        if (captureState == CapturerState.Started)
        {
            return false;
        }

        captureState = CapturerState.Started;

        Notify((dlgt) =>
        {
            dlgt.Capturer(this, CapturerState.Started);
        }, () =>
        {
            return $"capturer.didUpdate state: {CapturerState.Started}";
        });

        return true;
    }

    // returns true if state updated
    public virtual async UniTask<bool> StopCapture()
    {
        await UniTask.Yield();  // CS1998

        if (captureState == CapturerState.Stopped)
        {
            // already stopped
            return false;
        }

        captureState = CapturerState.Stopped;

        Notify((dlgt) =>
        {
            dlgt.Capturer(this, CapturerState.Stopped);
        }, () =>
        {
            return $"capturer.didUpdate state: {CapturerState.Stopped}";
        });

        _state.Mutate(t => { t.dimensionsCompleter.Reset(); return t; });

        return true;
    }

    public async UniTask<bool> RestartCapture()
    {
        await StopCapture();
        return await StartCapture();
    }
}
