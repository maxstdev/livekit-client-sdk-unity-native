using Unity.WebRTC;
using Cysharp.Threading.Tasks;

class RemoteAudioTrack : RemoteTrack
{
    internal RemoteAudioTrack(string name,
                              Track.Source source,
                              MediaStreamTrack track) :
    base(name: name,
         kind: Kind.Audio,
         source: source,
         track: track)
    { }

    public override async UniTask<bool> Start()
    {
        var didStart = await base.Start();

        return await queue.Sync(async () =>
        {
            if (didStart)
            {
                // TODO:Thomas:필수: AudioManager 구현후
                //AudioManager.shared.trackDidStart(.remote)
            }
            await UniTask.CompletedTask;
            return didStart;
        });
    }

    public override async UniTask<bool> Stop()
    {
        var didStop = await base.Stop();
        return await queue.Sync(async () =>
        {
            if (didStop)
            {
                // TODO:Thomas:필수: AudioManager 구현후
                //AudioManager.shared.trackDidStop(.remote)
            }
            await UniTask.CompletedTask;
            return didStop;
        });
    }
}
