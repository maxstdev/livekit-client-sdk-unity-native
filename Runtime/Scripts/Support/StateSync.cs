using Cysharp.Threading.Tasks;
using Dispatch;
using System;
using System.Diagnostics;

public class StateSync<T> where T : struct
{
    public T Value { get; private set; }
    public Action<T, T> OnMutate;
    private readonly SerialQueue queue = new("LiveKitSDK.state");

    public StateSync(T value, Action<T, T> onMutate = null)
    {
        this.Value = value;
        this.OnMutate = onMutate;
    }

    public async UniTask<T> MutateAwait(Func<T, UniTask<T>> mutation)
    {
        var completionSource = new UniTaskCompletionSource<T>();

        await queue.Sync(async () =>
        {
            T oldValue = (T)this.Value;
            this.Value = await mutation(this.Value);
            completionSource.TrySetResult(this.Value);
            OnMutate?.Invoke(this.Value, oldValue);
        });

        return await completionSource.Task;
    }

    public async UniTask<R> MutateAwait<R>(Func<T, UniTask<Tuple<T, R>>> mutation)
    {
        var completionSource = new UniTaskCompletionSource<R>();

        await queue.Sync(async () =>
        {
            T oldValue = (T)this.Value;
            var value = await mutation(this.Value);
            this.Value = value.Item1;
            completionSource.TrySetResult(value.Item2);
            OnMutate?.Invoke(this.Value, oldValue);
        });

        return await completionSource.Task;
    }

    public T Mutate(Func<T, T> mutation)
    {
        return queue.DispatchSync<T>(() =>
        {
            T oldValue = (T)this.Value;
            this.Value = mutation(this.Value);
            OnMutate?.Invoke(this.Value, oldValue);
            return this.Value;
        });
    }

    public R Mutate<R>(Func<T, Tuple<T, R>> mutation)
    {
        return queue.DispatchSync<R>(() =>
        {
            T oldValue = (T)this.Value;
            var value = mutation(this.Value);
            this.Value = value.Item1;
            OnMutate?.Invoke(this.Value, oldValue);
            return value.Item2;
        });
    }

    public void MutateAsync(Func<T, T> mutation)
    {
        queue.Async(() => {
            T oldValue = (T)this.Value;
            this.Value = mutation(this.Value);
            this.OnMutate(this.Value, oldValue);
        });
    }

    public T ReadCopy()
    {
        return queue.Sync<T>(() =>
        {
            return (T)this.Value;
        });
    }

    public R Read<R>(Func<T, R> block)
    {
        return queue.DispatchSync<R>(() =>
        {
            return block(this.Value);
        });
    }

    public void ReadyAsync(Action<T> block)
    {
        queue.Async(() =>
        {
            block(this.Value);
        });
    }
}
