using System;
using System.Collections.Generic;
using UnityEngine;

namespace DisplayIO.Ads.Internal
{
    /// <summary>
    /// JNI implementation of interstitial ads, keyed by placement id.
    ///
    /// State is touched from two threads: Load/IsReady/Show come from the Unity thread, while
    /// the listener callbacks arrive on Android threads. Every access to <see cref="loaded"/>
    /// and <see cref="loading"/> therefore goes through <see cref="gate"/> — a plain Dictionary
    /// corrupts silently under concurrent writes.
    /// </summary>
    internal class DioAndroidInterstitial
    {
        private const string RequestListenerInterface = "com.brandio.ads.listeners.AdRequestListener";
        private const string EventListenerInterface = "com.brandio.ads.listeners.AdEventListener";

        private readonly IDioInterstitialCallbacks callbacks;
        private readonly object gate = new object();
        private readonly Dictionary<string, AndroidJavaObject> loaded =
            new Dictionary<string, AndroidJavaObject>();
        private readonly HashSet<string> loading = new HashSet<string>();

        internal DioAndroidInterstitial(IDioInterstitialCallbacks callbacks)
        {
            this.callbacks = callbacks;
        }

        internal void Load(string placementId)
        {
            lock (gate)
            {
                if (loading.Contains(placementId))
                {
                    return;
                }

                loading.Add(placementId);
            }

            AndroidJavaObject activity = DioAndroidBridge.GetActivity();

            // Every SDK entry point is driven from the Android UI thread: Unity scripts run
            // on UnityMain, which has no Looper, and the SDK creates Handlers internally.
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try
                {
                    AndroidJavaObject placement =
                        DioAndroidBridge.GetController().Call<AndroidJavaObject>("getPlacement", placementId);

                    if (placement == null)
                    {
                        FailLoad(placementId, new DioError($"Placement {placementId} not found"));
                        return;
                    }

                    AndroidJavaObject request = placement.Call<AndroidJavaObject>("newAdRequest");
                    request.Call("setAdRequestListener", new RequestListenerProxy(this, placementId));
                    request.Call("requestAd");
                }
                catch (Exception e)
                {
                    FailLoad(placementId, new DioError(e.Message));
                }
            }));
        }

        internal bool IsReady(string placementId)
        {
            AndroidJavaObject ad;

            lock (gate)
            {
                if (!loaded.TryGetValue(placementId, out ad))
                {
                    return false;
                }
            }

            try
            {
                return ad.Call<bool>("isLoaded");
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal void Show(string placementId)
        {
            AndroidJavaObject ad;

            lock (gate)
            {
                loaded.TryGetValue(placementId, out ad);
            }

            if (ad == null)
            {
                DioCallbackDispatcher.Post(() => callbacks.ShowFailed(
                    DioAdInfoFactory.Minimal(placementId, DioAdUnitType.Interstitial),
                    new DioError("No interstitial loaded for this placement")));
                return;
            }

            AndroidJavaObject activity = DioAndroidBridge.GetActivity();

            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try
                {
                    ad.Call("showAd", activity);
                }
                catch (Exception e)
                {
                    DioAdInfo info = Info(ad, placementId);
                    DioCallbackDispatcher.Post(() => callbacks.ShowFailed(info, new DioError(e.Message)));
                }
            }));
        }

        private static DioAdInfo Info(AndroidJavaObject ad, string placementId) =>
            DioAdInfoFactory.From(ad, placementId, DioAdUnitType.Interstitial);

        private void FailLoad(string placementId, DioError error)
        {
            lock (gate)
            {
                loading.Remove(placementId);
            }

            DioCallbackDispatcher.Post(() => callbacks.LoadFailed(placementId, error));
        }

        /// <summary>
        /// Drops the cached ad and releases its JNI global reference. AndroidJavaObject holds
        /// a global ref, so removing it from the dictionary without disposing leaks both the
        /// ref slot and the Java ad — creative and WebView included. Caller must hold the lock.
        /// </summary>
        private void ForgetLocked(string placementId)
        {
            if (!loaded.TryGetValue(placementId, out AndroidJavaObject ad))
            {
                return;
            }

            loaded.Remove(placementId);

            try
            {
                ad?.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DioAds] Failed to dispose ad: {e.Message}");
            }
        }

        private static string SafeMessage(AndroidJavaObject dioError)
        {
            try
            {
                return dioError?.Call<string>("getMessage");
            }
            catch (Exception)
            {
                return null;
            }
        }

        private class RequestListenerProxy : AndroidJavaProxy
        {
            private readonly DioAndroidInterstitial owner;
            private readonly string placementId;

            internal RequestListenerProxy(DioAndroidInterstitial owner, string placementId)
                : base(RequestListenerInterface)
            {
                this.owner = owner;
                this.placementId = placementId;
            }

            public void onAdReceived(AndroidJavaObject ad)
            {
                lock (owner.gate)
                {
                    owner.loading.Remove(placementId);
                    owner.ForgetLocked(placementId);
                    owner.loaded[placementId] = ad;
                }

                try
                {
                    ad.Call("setEventListener", new EventListenerProxy(owner, placementId));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DioAds] setEventListener failed: {e.Message}");
                }

                DioAdInfo info = Info(ad, placementId);
                DioCallbackDispatcher.Post(() => owner.callbacks.Loaded(info));
            }

            public void onNoAds(AndroidJavaObject error)
            {
                lock (owner.gate)
                {
                    owner.loading.Remove(placementId);
                }

                var dioError = new DioError(SafeMessage(error));
                DioCallbackDispatcher.Post(() => owner.callbacks.NoFill(placementId, dioError));
            }

            public void onFailedToLoad(AndroidJavaObject error)
            {
                lock (owner.gate)
                {
                    owner.loading.Remove(placementId);
                }

                var dioError = new DioError(SafeMessage(error));
                DioCallbackDispatcher.Post(() => owner.callbacks.LoadFailed(placementId, dioError));
            }
        }

        private class EventListenerProxy : AndroidJavaProxy
        {
            private readonly DioAndroidInterstitial owner;
            private readonly string placementId;

            internal EventListenerProxy(DioAndroidInterstitial owner, string placementId)
                : base(EventListenerInterface)
            {
                this.owner = owner;
                this.placementId = placementId;
            }

            public void onShown(AndroidJavaObject ad)
            {
                DioAdInfo info = Info(ad, placementId);
                DioCallbackDispatcher.Post(() => owner.callbacks.Shown(info));
            }

            // AdEventListener.onFailedToShow takes (Ad, DIOError) — both arguments are required
            // for the proxy method to match.
            public void onFailedToShow(AndroidJavaObject ad, AndroidJavaObject error)
            {
                DioAdInfo info = Info(ad, placementId);
                var dioError = new DioError(SafeMessage(error));
                DioCallbackDispatcher.Post(() => owner.callbacks.ShowFailed(info, dioError));
            }

            public void onClicked(AndroidJavaObject ad)
            {
                DioAdInfo info = Info(ad, placementId);
                DioCallbackDispatcher.Post(() => owner.callbacks.Clicked(info));
            }

            public void onClosed(AndroidJavaObject ad)
            {
                DioAdInfo info = Info(ad, placementId);

                lock (owner.gate)
                {
                    owner.ForgetLocked(placementId);
                }

                DioCallbackDispatcher.Post(() => owner.callbacks.Closed(info));
            }

            public void onAdCompleted(AndroidJavaObject ad)
            {
                DioAdInfo info = Info(ad, placementId);
                DioCallbackDispatcher.Post(() => owner.callbacks.Completed(info));
            }
        }
    }
}
