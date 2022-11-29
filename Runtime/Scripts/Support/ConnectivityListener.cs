using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using UniLiveKit;
using UnityEngine;


// TODO - ConnectivityListener
internal interface IConnectivityListenerDelegate
{
    void ConnectivityListener(ConnectivityListener listerner, bool didUpdateHasConnectivity) { }

    // network remains to have connectivity but path changed
    //void ConnectivityListener(ConnectivityListener listerner, didSwitch path: NWPath)
}

internal class ConnectivityListener : MulticastDelegate<IConnectivityListenerDelegate> {
    internal static readonly ConnectivityListener shared = new();

    internal ConnectivityListener()
    {
        UnityEngine.Debug.Log("ConnectivityListener");

        NWMonitor.Initialize();
        monitor = NWMonitor.Instance;
        monitor.pathUpdateHandler = (isisAvailable) => {
            HasConnectivity(isisAvailable);
        };

        monitor.MonitorStart();
    }

    public bool? hasConnectivity { get; private set; }
    private NWMonitor monitor;

    public NetworkReachability ActiveInterfaceType => monitor.ActiveInterfaceType;

    private void HasConnectivity(bool hasConnectivity)
    {
        var oldValue = this.hasConnectivity;

        if (oldValue == null || oldValue != hasConnectivity)
        {
           
            this.hasConnectivity = hasConnectivity;
            Notify((e) => {
                e.ConnectivityListener(this, hasConnectivity); 
            });
        }
    }
}