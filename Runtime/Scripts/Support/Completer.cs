using Cysharp.Threading.Tasks;
using Dispatch;
using System;
using UniLiveKit.ErrorException;

public class Completer<T>
{
    private T value;

    private bool hasValue = false;
    private Action<T> onFulFilled = null;
    private Action<Exception> onRejected = null;
    private DispatchQueueTimer queueTimer = null;

    public void Set<R>(R? newValue) where R : struct
    {
        if (newValue == null)
        {
            if (hasValue)
            {
                Reset();
            }
            return;
        }

        if (newValue.Value is T rV)
        {
            Set(rV);
        }
    }

    public void Set(T newValue)
    {
        if (newValue == null)
        {
            if (hasValue)
            {
                Reset();
            }
            return;
        }

        value = newValue;
        onFulFilled?.Invoke(newValue);

        onFulFilled = null;
        onRejected = null;
        hasValue = true;

        if (queueTimer == null) { return; }
        if (queueTimer.TimerState == DispatchQueueTimer.State.Resumed)
        {
            queueTimer.Suspend();
            queueTimer = null;
        }
    }

    public async UniTask<T> Wait(SerialQueue queue, double interval, Exception ex)
    {
        if (hasValue)
        {
            return value;
        }

        var completionSource = new UniTaskCompletionSource<T>();

        onFulFilled = (newValue) =>
        {
            completionSource.TrySetResult(newValue);
        };

        onRejected = (e) =>
        {
            completionSource.TrySetException(e);
        };

        CreateDispatchQueueTimer(interval, ex, queue);

        return await completionSource.Task;
    }

    private void CreateDispatchQueueTimer(double interval, Exception ex, SerialQueue queue)
    {
        queueTimer = new DispatchQueueTimer(interval, queue);
        queueTimer.handler = () =>
        {
            queueTimer.Suspend();
            if (onRejected != null)
            {
                onRejected.Invoke(ex);
            }
        };
        queueTimer.Resume();
    }

    public void Reset()
    {
        onRejected?.Invoke(new EnumException<InternalError>(InternalError.State, "resetting pending promise"));

        if (queueTimer != null)
        {
            if (queueTimer.TimerState == DispatchQueueTimer.State.Resumed)
            {
                queueTimer.Suspend();
            }
            queueTimer = null;
        }

        onFulFilled = null;
        onRejected = null;
        hasValue = false;
        value = default;
    }
}
