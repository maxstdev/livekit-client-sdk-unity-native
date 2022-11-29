using Cysharp.Threading.Tasks;
using Unity.WebRTC;

public class RemoteTrack : Track
{
    internal RemoteTrack(string name, Kind kind, Source source, MediaStreamTrack track)
        : base(name, kind, source, track)
    { }

    public override async UniTask<bool> Start()
    {
        var didStart = await base.Start();

        return await queue.Sync(async () =>
        {
            await Enable();
            return didStart;
        });
    }

    public override async UniTask<bool> Stop()
    {
        var didStop = await base.Stop();
        return await queue.Sync(async () =>
        {
            await Disable();
            return didStop;
        });
    }
}
