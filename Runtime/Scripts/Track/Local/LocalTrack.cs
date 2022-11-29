using System;
using Cysharp.Threading.Tasks;
using Unity.WebRTC;

public class LocalTrack : Track
{
    internal LocalTrack(string name, Kind kind, Source source, MediaStreamTrack track)
        : base(name, kind, source, track) { }

    public enum PublishState
    {
        Unpublished,
        Published
    }

    public PublishState publishState { get; private set; } = PublishState.Unpublished;

    /// ``publishOptions`` used for this track if already published.
    public IPublishOptions publishOptions { get; internal set; }

    public async UniTask Mute()
    {
        // Already muted
        if (muted) { return; }

        await Disable();
        await queue.Sync(async () =>
        {
            await Stop();
            SetMuted(newValue: true, shouldSendSignal: true);
        });
        return;
    }

    public async UniTask Unmute()
    {
        // Already un-muted
        if (!muted) { return; }

        await Enable();
        await queue.Sync(async () =>
        {
            await Start();
            SetMuted(newValue: false, shouldSendSignal: true);
        });
        return;
    }

    // returns true if state updated
    internal virtual UniTask<bool> OnPublish()
    {
        return queue.Sync<UniTask<bool>>(async () =>
        {
            if (publishState == PublishState.Published)
            {
                // already published
                return await UniTask.FromResult(false);
            }

            publishState = PublishState.Published;

            return await UniTask.FromResult(true);
        });
    }

    // returns true if state updated
    internal virtual UniTask<bool> OnUnpublish()
    {
        return queue.Sync<UniTask<bool>>(async () =>
        {
            if (publishState == PublishState.Unpublished)
            {
                // already published
                return await UniTask.FromResult(false);
            }

            publishState = PublishState.Unpublished;

            return await UniTask.FromResult(true);
        });

    }
}
