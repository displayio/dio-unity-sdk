using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace DisplayIO.Ads.Internal
{
    /// <summary>
    /// Marshals callbacks arriving from native threads onto the Unity main thread.
    /// Touching the Unity API from a JNI callback thread is undefined behaviour,
    /// so every native callback must go through here.
    /// </summary>
    internal class DioCallbackDispatcher : MonoBehaviour
    {
        private static DioCallbackDispatcher instance;
        private static readonly ConcurrentQueue<Action> pending = new ConcurrentQueue<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance != null)
            {
                return;
            }

            var go = new GameObject("DioAdsCallbackDispatcher");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DioCallbackDispatcher>();
        }

        /// <summary>Queues an action to run on the next Unity frame.</summary>
        internal static void Post(Action action)
        {
            if (action == null)
            {
                return;
            }

            pending.Enqueue(action);
        }

        private void Update()
        {
            while (pending.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DioAds] Callback threw: {e}");
                }
            }
        }
    }
}
