using Cysharp.Threading.Tasks;
using Dispatch;
using System;
using System.Threading;
using System.Threading.Tasks;

public static class SerialQueueExtension
{
    public static void Async(this SerialQueue serialQueue, Action action) => serialQueue.DispatchAsync(action);
    public static Task<T> Async<T>(this SerialQueue serialQueue, Func<T> action) => serialQueue.DispatchAsync<T>(action);
    public static void Sync(this SerialQueue serialQueue, Action action) => serialQueue.DispatchSync(action);
    public static T Sync<T>(this SerialQueue serialQueue, Func<T> action) => serialQueue.DispatchSync(action);
}

public class DispatchQueue
{
    internal static SerialQueue WebRTC = new("LiveKitSDK.webRTC");

    public static async void MainSafeAsync(Action work)
    {
        if (Thread.CurrentThread.Equals(UniTask.ReturnToMainThread()))
        {
            work.Invoke();
            return;
        }

        await UniTask.SwitchToMainThread();
        work.Invoke();
    }
}
