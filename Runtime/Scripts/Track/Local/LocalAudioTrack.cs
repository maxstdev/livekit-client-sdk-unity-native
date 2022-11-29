using Cysharp.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

public class LocalAudioTrack : LocalTrack
{
    AudioSource audioSource;

    LocalAudioTrack(string name, Source source, MediaStreamTrack track, AudioSource audioSource)
        : base(name, Kind.Audio, source, track)
    {
        this.audioSource = audioSource;
    }

    public static LocalAudioTrack CreateTrack(AudioSource audioSource,
                                              string name = Track.MicrophoneName,
                                              AudioCaptureOptions? options = null)
    {
        var optionsValue = options ?? new AudioCaptureOptions(null);

        // NOTE:Thomas: Unity WebRTC에 RTCMediaConstraints가 구현되어 있지 않아 의미가 없다.
        //let constraints: [String: String] = [
        //    "googEchoCancellation": options.echoCancellation.toString(),
        //    "googAutoGainControl": options.autoGainControl.toString(),
        //    "googNoiseSuppression": options.noiseSuppression.toString(),
        //    "googTypingNoiseDetection": options.typingNoiseDetection.toString(),
        //    "googHighpassFilter": options.highpassFilter.toString(),
        //    "googNoiseSuppression2": options.experimentalNoiseSuppression.toString(),
        //    "googAutoGainControl2": options.experimentalAutoGainControl.toString()
        //]

        //let audioConstraints = DispatchQueue.webRTC.sync { RTCMediaConstraints(mandatoryConstraints: nil,
        //                                                                       optionalConstraints: constraints) }

        var rtcTrack = Engine.CreateAudioTrack(source: audioSource);
        rtcTrack.Enabled = true;

        return new LocalAudioTrack(name: name,
                                   source: Source.Microphone,
                                   track: rtcTrack,
                                   audioSource: audioSource);
    }

    internal override async UniTask<bool> OnPublish()
    {
        var didPublish = await base.OnPublish();

        await queue.Sync(async () =>
        {
            if (didPublish)
            {
                DispatchQueue.MainSafeAsync(() =>
                {
                    audioSource.Play();
                });
            }
            await UniTask.CompletedTask;
            return;
        });

        return didPublish;
    }

    internal override async UniTask<bool> OnUnpublish()
    {
        var didUnpublish = await base.OnUnpublish();

        await queue.Sync(async () =>
        {
            if (didUnpublish)
            {
                DispatchQueue.MainSafeAsync(() =>
                {
                    audioSource.Stop();
                });
            }
            await UniTask.CompletedTask;
            return;
        });

        return didUnpublish;
    }
}