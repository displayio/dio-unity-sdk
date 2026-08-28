using System;
using UnityEngine;

namespace DisplayIO.Ads.Internal
{
    /// <summary>
    /// JNI implementation shared by all view-attached formats. The ad view goes into a
    /// wrapper FrameLayout of our own, never straight into the activity content view:
    /// InlineContainer.prepareForBind() calls removeAllViews() on its parent, which would
    /// tear down Unity's own view hierarchy.
    /// </summary>
    internal class DioAndroidInlineAd
    {
        private const string RequestListenerInterface = "com.brandio.ads.listeners.AdRequestListener";
        private const string EventListenerInterface = "com.brandio.ads.listeners.AdEventListener";

        private readonly string placementId;
        private readonly DioAdUnitType adUnitType;
        private readonly IDioInlineAdCallbacks callbacks;

        // State is touched from two threads: Load/Show/Hide/Destroy come from the Unity thread,
        // the listener callbacks and the runOnUiThread bodies from Android threads. Every field
        // below is read and written under this lock.
        private readonly object gate = new object();

        private AndroidJavaObject placement;
        private AndroidJavaObject container;
        private AndroidJavaObject ad;
        private AndroidJavaObject wrapper;
        private bool loading;

        /// <summary>
        /// Optional placement configuration, applied right after the placement is resolved and
        /// before the ad is requested. Runs on the Android UI thread.
        /// </summary>
        internal Action<AndroidJavaObject> ConfigurePlacement { get; set; }

        internal DioAndroidInlineAd(
            string placementId, DioAdUnitType adUnitType, IDioInlineAdCallbacks callbacks)
        {
            this.placementId = placementId;
            this.adUnitType = adUnitType;
            this.callbacks = callbacks;
        }

        internal bool IsLoaded
        {
            get
            {
                lock (gate)
                {
                    return container != null;
                }
            }
        }

        /// <summary>Requests an ad. Never touches the view hierarchy.</summary>
        internal void Load()
        {
            lock (gate)
            {
                if (loading)
                {
                    return;
                }

                loading = true;
            }
            AndroidJavaObject activity = DioAndroidBridge.GetActivity();

            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try
                {
                    AndroidJavaObject resolved = DioAndroidBridge.GetController()
                        .Call<AndroidJavaObject>("getPlacement", placementId);

                    if (resolved == null)
                    {
                        Fail(new DioError($"Placement {placementId} not found"));
                        return;
                    }

                    lock (gate)
                    {
                        placement = resolved;
                    }

                    ConfigurePlacement?.Invoke(resolved);

                    AndroidJavaObject request = resolved.Call<AndroidJavaObject>("newAdRequest");
                    request.Call("setAdRequestListener", new RequestListenerProxy(this));
                    request.Call("requestAd");
                }
                catch (Exception e)
                {
                    Fail(new DioError(e.Message));
                }
            }));
        }

        /// <summary>
        /// Inserts the ad view into the activity content view at the given position, or
        /// re-positions it when already inserted. Requires a loaded ad. Margins are pixels.
        /// </summary>
        internal void Show(DioAdPosition position, int left, int top, int right, int bottom)
        {
            lock (gate)
            {
                if (container == null)
                {
                    DioAdInfo missing = DioAdInfoFactory.Minimal(placementId, adUnitType);
                    DioCallbackDispatcher.Post(() => callbacks.ShowFailed(
                        missing, new DioError("No ad loaded for this placement")));
                    return;
                }
            }

            AndroidJavaObject activity = DioAndroidBridge.GetActivity();

            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try
                {
                    AndroidJavaObject layoutParams = LayoutParams(position, left, top, right, bottom);

                    lock (gate)
                    {
                        if (container == null)
                        {
                            return;
                        }

                        if (wrapper == null)
                        {
                            wrapper = new AndroidJavaObject("android.widget.FrameLayout", activity);
                            ContentView(activity).Call("addView", wrapper, layoutParams);
                            container.Call("bindTo", wrapper);
                        }
                        else
                        {
                            wrapper.Call("setLayoutParams", layoutParams);
                        }
                    }
                }
                catch (Exception e)
                {
                    DioAdInfo info = DioAdInfoFactory.Minimal(placementId, adUnitType);
                    DioCallbackDispatcher.Post(() => callbacks.ShowFailed(info, new DioError(e.Message)));
                }
            }));
        }

        /// <summary>
        /// Detaches the ad view and removes the wrapper from the activity content view.
        /// Nothing of ours is left overlaying the app.
        /// </summary>
        internal void Hide()
        {
            lock (gate)
            {
                if (wrapper == null)
                {
                    return;
                }
            }

            AndroidJavaObject activity = DioAndroidBridge.GetActivity();

            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                lock (gate)
                {
                    if (wrapper == null)
                    {
                        return;
                    }

                    try
                    {
                        // The SDK's own view must be detached from the wrapper first, otherwise
                        // a later bindTo() throws: BannerContainer caches its layout and would
                        // try to add a view that still has a parent.
                        wrapper.Call("removeAllViews");
                        ContentView(activity).Call("removeView", wrapper);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[DioAds] Hide failed: {e.Message}");
                    }

                    Release(ref wrapper);
                }
            }));
        }

        internal void Destroy()
        {
            Hide();

            AndroidJavaObject activity = DioAndroidBridge.GetActivity();
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                lock (gate)
                {
                    Release(ref container);
                    Release(ref ad);
                    Release(ref placement);
                }
            }));
        }

        private static void Release(ref AndroidJavaObject obj)
        {
            try
            {
                obj?.Dispose();
            }
            catch (Exception)
            {
                // already gone; nothing to release
            }

            obj = null;
        }

        private static AndroidJavaObject ContentView(AndroidJavaObject activity)
        {
            int contentId;
            using (var ids = new AndroidJavaClass("android.R$id"))
            {
                contentId = ids.GetStatic<int>("content");
            }

            return activity.Call<AndroidJavaObject>("getWindow")
                           .Call<AndroidJavaObject>("getDecorView")
                           .Call<AndroidJavaObject>("findViewById", contentId);
        }

        private static AndroidJavaObject LayoutParams(
            DioAdPosition position, int left, int top, int right, int bottom)
        {
            int wrapContent;
            using (var lp = new AndroidJavaClass("android.view.ViewGroup$LayoutParams"))
            {
                wrapContent = lp.GetStatic<int>("WRAP_CONTENT");
            }

            var layoutParams = new AndroidJavaObject(
                "android.widget.FrameLayout$LayoutParams", wrapContent, wrapContent, Gravity(position));
            layoutParams.Call("setMargins", left, top, right, bottom);
            return layoutParams;
        }

        private static int Gravity(DioAdPosition position)
        {
            using (var gravity = new AndroidJavaClass("android.view.Gravity"))
            {
                int top = gravity.GetStatic<int>("TOP");
                int bottom = gravity.GetStatic<int>("BOTTOM");
                int start = gravity.GetStatic<int>("START");
                int end = gravity.GetStatic<int>("END");
                int centerH = gravity.GetStatic<int>("CENTER_HORIZONTAL");
                int centerV = gravity.GetStatic<int>("CENTER_VERTICAL");

                switch (position)
                {
                    case DioAdPosition.TopLeft: return top | start;
                    case DioAdPosition.TopCenter: return top | centerH;
                    case DioAdPosition.TopRight: return top | end;
                    case DioAdPosition.CenterLeft: return centerV | start;
                    case DioAdPosition.Center: return centerV | centerH;
                    case DioAdPosition.CenterRight: return centerV | end;
                    case DioAdPosition.BottomLeft: return bottom | start;
                    case DioAdPosition.BottomRight: return bottom | end;
                    default: return bottom | centerH;
                }
            }
        }

        private void Fail(DioError error)
        {
            lock (gate)
            {
                loading = false;
            }

            DioCallbackDispatcher.Post(() => callbacks.LoadFailed(error));
        }

        private DioAdInfo Info(AndroidJavaObject adObject) =>
            DioAdInfoFactory.From(adObject, placementId, adUnitType);

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
            private readonly DioAndroidInlineAd owner;

            internal RequestListenerProxy(DioAndroidInlineAd owner) : base(RequestListenerInterface)
            {
                this.owner = owner;
            }

            public void onAdReceived(AndroidJavaObject receivedAd)
            {
                AndroidJavaObject placement;

                lock (owner.gate)
                {
                    owner.loading = false;
                    owner.ad = receivedAd;
                    placement = owner.placement;
                }

                try
                {
                    receivedAd.Call("setEventListener", new EventListenerProxy(owner));
                    string requestId = receivedAd.Call<string>("getRequestId");
                    AndroidJavaObject resolved =
                        placement.Call<AndroidJavaObject>("getContainer", requestId);

                    lock (owner.gate)
                    {
                        owner.container = resolved;
                    }
                }
                catch (Exception e)
                {
                    owner.Fail(new DioError(e.Message));
                    return;
                }

                DioAdInfo info = owner.Info(receivedAd);
                DioCallbackDispatcher.Post(() => owner.callbacks.Loaded(info));
            }

            public void onNoAds(AndroidJavaObject error)
            {
                lock (owner.gate)
                {
                    owner.loading = false;
                }

                var dioError = new DioError(SafeMessage(error));
                DioCallbackDispatcher.Post(() => owner.callbacks.NoFill(dioError));
            }

            public void onFailedToLoad(AndroidJavaObject error)
            {
                lock (owner.gate)
                {
                    owner.loading = false;
                }

                var dioError = new DioError(SafeMessage(error));
                DioCallbackDispatcher.Post(() => owner.callbacks.LoadFailed(dioError));
            }
        }

        private class EventListenerProxy : AndroidJavaProxy
        {
            private readonly DioAndroidInlineAd owner;

            internal EventListenerProxy(DioAndroidInlineAd owner) : base(EventListenerInterface)
            {
                this.owner = owner;
            }

            public void onShown(AndroidJavaObject shownAd)
            {
                DioAdInfo info = owner.Info(shownAd);
                DioCallbackDispatcher.Post(() => owner.callbacks.Shown(info));
            }

            public void onFailedToShow(AndroidJavaObject failedAd, AndroidJavaObject error)
            {
                DioAdInfo info = owner.Info(failedAd);
                var dioError = new DioError(SafeMessage(error));
                DioCallbackDispatcher.Post(() => owner.callbacks.ShowFailed(info, dioError));
            }

            public void onClicked(AndroidJavaObject clickedAd)
            {
                DioAdInfo info = owner.Info(clickedAd);
                DioCallbackDispatcher.Post(() => owner.callbacks.Clicked(info));
            }

            public void onClosed(AndroidJavaObject closedAd)
            {
                DioAdInfo info = owner.Info(closedAd);
                DioCallbackDispatcher.Post(() => owner.callbacks.Closed(info));
            }

            public void onAdCompleted(AndroidJavaObject completedAd)
            {
                DioAdInfo info = owner.Info(completedAd);
                DioCallbackDispatcher.Post(() => owner.callbacks.Completed(info));
            }
        }
    }
}
