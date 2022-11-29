using Cysharp.Threading.Tasks;
using Dispatch;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class UniTaskExtension
{
    static public async UniTask Retry(SerialQueue queue, int attempts, double delay, Func<UniTask> work, Func<int, Exception, bool?> condition = null)
    {
        var taskQueue = queue;
        if (taskQueue == null)
        {
            taskQueue = new SerialQueue("RetrySerialQueue");
        }

        await taskQueue.Sync(async () =>
        {
            if (attempts <= 0) return;

            var source = new UniTaskCompletionSource();

            try
            {
                await work();
                source.TrySetResult();
            }
            catch (Exception error)
            {
                var isRetry = true;

                if (condition != null)
                {
                    isRetry = condition(attempts, error) ?? true;
                }

                if (!isRetry)
                {
                    source.TrySetException(error);
                    return;
                }

                await UniTask.Delay((int)delay * 1000);
                await Retry(taskQueue, attempts - 1, delay, work, condition);
                source.TrySetException(error);
            }

            await source.Task;
        });

        return;
    }
}


