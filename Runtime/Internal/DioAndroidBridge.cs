using System;
using UnityEngine;

namespace DisplayIO.Ads.Internal
{
    /// <summary>JNI access to com.brandio.ads.Controller.</summary>
    internal static class DioAndroidBridge
    {
        private const string ControllerClass = "com.brandio.ads.Controller";
        private const string InitListenerInterface = "com.brandio.ads.listeners.SdkInitListener";

        internal static AndroidJavaObject GetActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return player.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }

        internal static AndroidJavaObject GetController()
        {
            using (var controller = new AndroidJavaClass(ControllerClass))
            {
                return controller.CallStatic<AndroidJavaObject>("getInstance");
            }
        }

        internal static bool IsInitialized()
        {
            try
            {
                return GetController().Call<bool>("isInitialized");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DioAds] isInitialized failed: {e.Message}");
                return false;
            }
        }

        internal static string GetVersion()
        {
            try
            {
                return GetController().Call<string>("getVer");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DioAds] getVer failed: {e.Message}");
                return null;
            }
        }

        internal static void Initialize(string appId, Action onInit, Action<DioError> onError)
        {
            try
            {
                AndroidJavaObject activity = GetActivity();
                AndroidJavaObject controller = GetController();
                var listener = new SdkInitListenerProxy(onInit, onError);

                // Controller.init must run on the Android UI thread: the Open Measurement
                // layer creates Handlers during activation, and Unity scripts execute on
                // the UnityMain thread, which has no Looper.
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        controller.Call("init", activity, appId, listener);
                    }
                    catch (Exception e)
                    {
                        DioCallbackDispatcher.Post(() => onError?.Invoke(new DioError(e.Message)));
                    }
                }));
            }
            catch (Exception e)
            {
                DioCallbackDispatcher.Post(() => onError?.Invoke(new DioError(e.Message)));
            }
        }

        private class SdkInitListenerProxy : AndroidJavaProxy
        {
            private readonly Action initCallback;
            private readonly Action<DioError> errorCallback;

            internal SdkInitListenerProxy(Action initCallback, Action<DioError> errorCallback)
                : base(InitListenerInterface)
            {
                this.initCallback = initCallback;
                this.errorCallback = errorCallback;
            }

            public void onInit()
            {
                DioCallbackDispatcher.Post(() => initCallback?.Invoke());
            }

            public void onInitError(AndroidJavaObject dioError)
            {
                string message = null;
                try
                {
                    message = dioError?.Call<string>("getMessage");
                }
                catch (Exception)
                {
                    // the error object is best-effort; a missing message is not fatal
                }

                DioCallbackDispatcher.Post(() => errorCallback?.Invoke(new DioError(message)));
            }
        }
    }
}
