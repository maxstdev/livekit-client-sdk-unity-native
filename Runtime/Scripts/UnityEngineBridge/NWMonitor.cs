using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO : CodeCleanup - NWMonitor
namespace UniLiveKit
{
    public class NWMonitor : MonoBehaviour
    {
        private enum State
        {
            NotAvailable, Available
        }

        public NWMonitor()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += Update;
#endif
        }

        public static NWMonitor Instance { get; private set; }
        static bool initialized;
        static bool isQuitting = false;

        private const bool allowCarrierDataNetwork = true;

        private Ping ping;
        private const string pingAddress = "8.8.8.8"; // Google Public DNS server
        private const float pongWaitingTime = 5.0F;
        private float pingDeltaTime = 0F;

        private float updateWaitingTime = 5.0F;
        private float updateDeltaTime = 0F;

        private bool isStop = false;

        private State? nwState;
        public Action<bool> pathUpdateHandler;

        public NetworkReachability ActiveInterfaceType => Application.internetReachability;

        void Awake()
        {

            if (Instance == null)
            {
                Instance = this;
                initialized = true;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                if (this != Instance)
                {
                    Debug.LogWarning("There is already a NWMonitor in the scene. Removing myself...");
                    DestroyNWMonitor(this);
                }
                else
                {
                    Debug.LogWarning("There is already a NWMonitor in the scene.");
                }
            }
        }

        void Update()
        {
            if (!isStop)
            {
                StartCoroutine(CheckConnectionToMasterServer());   
            }
        }

        void OnApplicationQuit()
        {
            isQuitting = true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Stop();
                Instance = GameObject.FindObjectOfType<NWMonitor>();
                initialized = Instance != null;

                /*
                // Although `this` still refers to a gameObject, it won't be found.
                var foundNWMonitor = GameObject.FindObjectOfType<NWMonitor>();

                if (foundDispatcher != null)
                {
                    // select another game object
                    Debug.Log("new instance: " + foundNWMonitor.name);
                    instance = foundNWMonitor;
                    initialized = true;
                }
                */
            }
        }

        public static void Initialize()
        {
            if (!initialized)
            {
#if UNITY_EDITOR
                // Don't try to add a GameObject when the scene is not playing. Only valid in the Editor, EditorView.
                if (!ScenePlaybackDetector.IsPlaying) return;
#endif
                NWMonitor monitor = null;

                try
                {
                    monitor = GameObject.FindObjectOfType<NWMonitor>();
                }
                catch
                {
                    // Throw exception when calling from a worker thread.
                    var ex = new Exception("NWMonitor requires a NWMonitor component created on the main thread. Make sure it is added to the scene before calling LiveKit from a worker thread.");
                    UnityEngine.Debug.LogException(ex);
                    throw ex;
                }

                if (isQuitting)
                {
                    // don't create new instance after quitting
                    // avoid "Some objects were not cleaned up when closing the scene find target" error.
                    return;
                }

                if (monitor == null)
                {
                    // awake call immediately from UnityEngine
                    new GameObject("NWMonitor").AddComponent<NWMonitor>();
                }
                else
                {
                    monitor.Awake(); // force awake
                }
            }
        }

        static void DestroyNWMonitor(NWMonitor aNWMonitor)
        {
            if (aNWMonitor != Instance)
            {
                // Try to remove game object if it's empty
                var components = aNWMonitor.gameObject.GetComponents<Component>();
                if (aNWMonitor.gameObject.transform.childCount == 0 && components.Length == 2)
                {
                    if (components[0] is Transform && components[1] is NWMonitor)
                    {
                        Destroy(aNWMonitor.gameObject);
                    }
                }
                else
                {
                    // Remove component
                    MonoBehaviour.Destroy(aNWMonitor);
                }
            }
        }

        private IEnumerator CheckConnectionToMasterServer()
        {

            if (updateDeltaTime < updateWaitingTime)
            {
                updateDeltaTime += Time.deltaTime;
            }
            else
            {
                if (this.ping != null)
                {
                    pingDeltaTime += Time.deltaTime;

                    if (IsInternetPossiblyAvailable() && ping.isDone)
                    {
                        if (ping.time >= 0)
                            InternetAvailable();
                        else
                            InternetIsNotAvailable();

                        this.ping = null;
                        updateDeltaTime = 0F;

                    }
                    else if (pingDeltaTime < pongWaitingTime)
                    {
                        InternetCheckWaiting();
                    }
                    else
                    {
                        InternetIsNotAvailable();
                        this.ping = null;
                        updateDeltaTime = 0F;

                    }
                }
                else
                {
                    if (!IsInternetPossiblyAvailable())
                    {
                        InternetIsNotAvailable();
                        this.ping = null;
                        updateDeltaTime = 0F;
                    }
                    else
                    {
                        ping = new Ping(pingAddress);
                        pingDeltaTime = 0F;
                    }
                }
            }
            yield return null;
        }

        private void InternetIsNotAvailable()
        {
            //Debug.Log("Internet is NotAvailable!)");
            CheckUpdatedState(State.NotAvailable);
        }

        private void InternetAvailable()
        {
            //Debug.Log("Internet is Available!)");
            CheckUpdatedState(State.Available);
        }

        private void InternetCheckWaiting()
        {
            //Debug.Log("Internet is Check Waiting!)");
        }

        private void CheckUpdatedState(State nwState)
        {
            var isAvailable = nwState == State.Available;
            if (this.nwState == null)
            {
                this.nwState = nwState;
                pathUpdateHandler?.Invoke(isAvailable);
                return;
            }

            var oldStae = this.nwState;
            if (oldStae != nwState)
            {
                this.nwState = nwState;
                pathUpdateHandler?.Invoke(isAvailable);
            }
        }

        public void MonitorStart(float updateWaitingTime = 1.0F)
        {
            isStop = false;
            this.updateWaitingTime = updateWaitingTime;
        }

        public void Stop()
        {
            isStop = true;
        }

        private bool IsInternetPossiblyAvailable()
        {
            return Application.internetReachability switch
            {
                NetworkReachability.ReachableViaLocalAreaNetwork => true,
                NetworkReachability.ReachableViaCarrierDataNetwork => allowCarrierDataNetwork,
                NetworkReachability.NotReachable => false,
                _ => false,
            };
        }
    }
}