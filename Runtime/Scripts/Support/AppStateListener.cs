using System;
using System.Collections;
using System.Collections.Generic;
using UniLiveKit;
using UnityEngine;

internal interface IAppStateDelegate
{
    public void AppDidEnterBackground() { }
    public void AppWillEnterForeground() { }
    public void AppWillTerminate() { }
}

internal class AppStateListener : Singleton<AppStateListener>, IAppStateDelegate
{
    public MulticastDelegate<IAppStateDelegate> listener = new();

    private void Awake()
    {

    }

    /*
     * Mobile
     * Enter Background : pause-true > focus-false
     * Enter foreground : foucs-true > pause-false
     */
    private void OnApplicationFocus(bool focus)
    {
// FIXME:Thomas:221018 작업을 편의를 위해 우선 막음
//#if UNITY_EDITOR
//        if (focus)
//            listener.Notify((e) => { e.AppWillEnterForeground(); });
//        else
//            listener.Notify((e) => { e.AppDidEnterBackground(); });
//#endif
    }

    private void OnApplicationPause(bool pause)
    {
#if !UNITY_EDITOR
        if (!pause)
            listener.Notify((e) => { e.AppWillEnterForeground(); });
        else
            listener.Notify((e) => { e.AppDidEnterBackground(); });
#endif
    }

    private void OnApplicationQuit()
    {
        //TODO : Doesn't work on mobile.
        //Debug.Log("Recording : OnApplicationQuit");
    }
}