using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;
using System.Text;
using Dispatch;
using System;
using System.Linq;
using UniLiveKit.ErrorException;

public class HTTP
{
    // TODO: string? Unit8[]?
    public UniTask<string> Get(SerialQueue queue, Uri url)
    {
        return queue.Async<UniTask<string>>(async () =>
        {
            var request = UnityWebRequest.Get(url);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(request.error);
            }

            var rValue = request.downloadHandler.text;

            if (string.IsNullOrEmpty(rValue)) throw new EnumException<NetworkError>(NetworkError.Response, "data is null");

            return rValue;
        }).Unwrap();
    }
}
