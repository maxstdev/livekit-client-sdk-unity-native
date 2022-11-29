using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using Dispatch;
using UnityEngine;

public class DispatchQueueTimer
{
    public enum State
    {
        Suspended, Resumed
    }

    private readonly double intervalPerSecond = 1;
    private SerialQueue queue;
    private PlayerLoopTimer timer = null;
    private UniTaskCompletionSource complete;
    public Action handler;

    public State TimerState { get; private set; } = State.Suspended;

    public DispatchQueueTimer(double intervalPerSecond = 1, SerialQueue queue = null)
    {
        this.queue = queue;
        this.intervalPerSecond = intervalPerSecond;
    }

    public void Reset()
    {
        timer = null;
        TimerState = State.Suspended;
    }

    public void Restart()
    {
        Suspend();
        Reset();
        Resume();
    }

    public void Resume()
    {
        if (TimerState == State.Resumed)
        {
            return;
        }

        TimerState = State.Resumed;

        timer = PlayerLoopTimer.StartNew(
            TimeSpan.FromSeconds(intervalPerSecond),
            true,
            DelayType.DeltaTime,
            PlayerLoopTiming.Update,
            CancellationToken.None, _ =>
        {
            handler?.Invoke();
        }, null);

    }

    public void Suspend()
    {
        if (TimerState == State.Suspended) return;

        TimerState = State.Suspended;
        timer?.Stop();
        timer?.Dispose();
    }
}
