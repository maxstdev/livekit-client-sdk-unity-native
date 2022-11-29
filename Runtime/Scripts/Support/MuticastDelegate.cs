using Dispatch;
using System;
using System.Runtime.CompilerServices;

public class MulticastDelegate<T> where T : class
{
    private readonly SerialQueue multicastQueue;

    private readonly ConditionalWeakTable<T, WeakReference> delegates = new();

    public MulticastDelegate(string label = "livekit.multicast")
    {
        this.multicastQueue = new(label);
    }

    public void AddDelegate(T obj)
    {
        if (obj == null) return;

        multicastQueue.Sync(() =>
        {
            delegates.AddOrUpdate(obj, new WeakReference(obj));
        });
    }

    public void RemoveDelegate(T obj)
    {
        if (obj == null) return;

        multicastQueue.Sync(() =>
        {
            delegates.Remove(obj);
        });
    }

    internal void Notify(Action<T> fnc, Func<string> label = null)
    {
        multicastQueue.Async(() =>
        {
            if (label != null && label is Func<string> notiLabel)
            {
                UnityEngine.Debug.Log($"[Notify] {notiLabel()}");
            }

            foreach (var obj in delegates)
            {
                if (obj.Value != null && obj.Value.Target is T rDelegate)
                {
                    DispatchQueue.MainSafeAsync(() =>
                    {
                        fnc.Invoke(rDelegate);
                    });
                }
                else
                {
                    continue;
                }
            }
        });
    }

    internal void Notify(
        Func<T, bool> fnc,
        bool requiresHanle = true,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string caller = null,
        Func<string> label = null
        )
    {
        multicastQueue.Async(() =>
        {
            if (label is Func<string> notiLabel)
            {
                UnityEngine.Debug.Log($"[Notify] {notiLabel()}");
            }

            int count = 0;
            foreach (var obj in delegates)
            {
                if (obj.Value != null && obj.Value.Target is T rDelegate)
                {
                    if (fnc(rDelegate))
                    {
                        count++;
                    };
                }
                else
                {
                    UnityEngine.Debug.Log($"MulticastDelegate: skipping notify for {obj}, not a type of {typeof(T)}");
                    continue;
                }
            }

            bool wasHandled = count > 0;

            UnityEngine.Debug.Assert(!(requiresHanle && !wasHandled), $"notify() was not handled by the delegate, called from {caller} line {line}");
        });
    }
}