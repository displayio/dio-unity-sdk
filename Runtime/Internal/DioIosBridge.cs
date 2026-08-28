#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace DisplayIO.Ads.Internal
{
    /// <summary>Native bridge to DIOSDK.framework. Event codes mirror DioAdsBridge.mm.</summary>
    internal static class DioIosBridge
    {
        private enum BridgeEvent
        {
            Initialized = 0,
            InitializationFailed = 1,

            InterstitialLoaded = 10,
            InterstitialNoFill = 11,
            InterstitialLoadFailed = 12,
            InterstitialShown = 13,
            InterstitialShowFailed = 14,
            InterstitialClicked = 15,
            InterstitialClosed = 16,
            InterstitialCompleted = 17,

            InlineLoaded = 20,
            InlineNoFill = 21,
            InlineLoadFailed = 22,
            InlineShown = 23,
            InlineShowFailed = 24,
            InlineClicked = 25,
            InlineClosed = 26,
            InlineCompleted = 27
        }

        [Serializable]
        private class Payload
        {
            public string placementId;
            public string requestId;
            public string advertiserName;
            public string campaignId;
            public string creativeId;
            public string auctionId;
            public string[] adomain;
            public double ecpm;
            public long ttl;
            public string message;
        }

        private delegate void EventCallback(int eventType, string payloadJson);

        [DllImport("__Internal")] private static extern void dioSetEventCallback(EventCallback callback);
        [DllImport("__Internal")] private static extern void dioInitialize(string appId);
        [DllImport("__Internal")] private static extern bool dioIsInitialized();
        [DllImport("__Internal")] private static extern IntPtr dioGetSdkVersion();

        [DllImport("__Internal")] private static extern void dioInterstitialLoad(string placementId);
        [DllImport("__Internal")] private static extern bool dioInterstitialIsReady(string placementId);
        [DllImport("__Internal")] private static extern void dioInterstitialShow(string placementId);

        [DllImport("__Internal")] private static extern void dioInlineLoad(string placementId, int customSize);
        [DllImport("__Internal")] private static extern bool dioInlineIsLoaded(string placementId);
        [DllImport("__Internal")] private static extern void dioInlineShow(
            string placementId, int position, float offsetX, float offsetY, int safeArea);
        [DllImport("__Internal")] private static extern void dioInlineHide(string placementId);
        [DllImport("__Internal")] private static extern void dioInlineDestroy(string placementId);

        private static Action onInit;
        private static Action<DioError> onInitError;
        private static IDioInterstitialCallbacks interstitial;
        private static readonly Dictionary<string, InlineTarget> inlineTargets =
            new Dictionary<string, InlineTarget>();
        private static bool callbackInstalled;

        private class InlineTarget
        {
            internal IDioInlineAdCallbacks Callbacks;
            internal DioAdUnitType AdUnitType;
        }

        // ---------- init ----------

        internal static bool IsInitialized() => dioIsInitialized();

        internal static string GetVersion()
        {
            IntPtr pointer = dioGetSdkVersion();
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            string value = Marshal.PtrToStringAnsi(pointer);
            Marshal.FreeHGlobal(pointer);
            return value;
        }

        internal static void Initialize(string appId, Action onInitialized, Action<DioError> onError)
        {
            onInit = onInitialized;
            onInitError = onError;
            EnsureCallback();
            dioInitialize(appId);
        }

        // ---------- interstitial ----------

        internal static void RegisterInterstitial(IDioInterstitialCallbacks callbacks)
        {
            interstitial = callbacks;
            EnsureCallback();
        }

        internal static void InterstitialLoad(string placementId) => dioInterstitialLoad(placementId);

        internal static bool InterstitialIsReady(string placementId) => dioInterstitialIsReady(placementId);

        internal static void InterstitialShow(string placementId) => dioInterstitialShow(placementId);

        // ---------- inline ----------

        internal static void RegisterInline(
            string placementId, DioAdUnitType adUnitType, IDioInlineAdCallbacks callbacks)
        {
            inlineTargets[placementId] = new InlineTarget
            {
                Callbacks = callbacks,
                AdUnitType = adUnitType
            };

            EnsureCallback();
        }

        internal static void InlineLoad(string placementId, int customSize) =>
            dioInlineLoad(placementId, customSize);

        internal static bool InlineIsLoaded(string placementId) => dioInlineIsLoaded(placementId);

        internal static void InlineShow(
            string placementId, DioAdPosition position, Vector2 offset, DioSafeArea safeArea) =>
            dioInlineShow(placementId, (int)position, offset.x, offset.y, (int)safeArea);

        internal static void InlineHide(string placementId) => dioInlineHide(placementId);

        internal static void InlineDestroy(string placementId)
        {
            dioInlineDestroy(placementId);
            inlineTargets.Remove(placementId);
        }

        // ---------- plumbing ----------

        private static void EnsureCallback()
        {
            if (callbackInstalled)
            {
                return;
            }

            dioSetEventCallback(HandleEvent);
            callbackInstalled = true;
        }

        [MonoPInvokeCallback(typeof(EventCallback))]
        private static void HandleEvent(int eventType, string payloadJson)
        {
            Payload payload = Parse(payloadJson) ?? new Payload();
            var kind = (BridgeEvent)eventType;

            switch (kind)
            {
                case BridgeEvent.Initialized:
                    DioCallbackDispatcher.Post(() => onInit?.Invoke());
                    return;

                case BridgeEvent.InitializationFailed:
                    DioCallbackDispatcher.Post(() => onInitError?.Invoke(new DioError(payload.message)));
                    return;
            }

            if (eventType >= 10 && eventType <= 17)
            {
                DispatchInterstitial(kind, payload);
                return;
            }

            if (eventType >= 20 && eventType <= 27)
            {
                DispatchInline(kind, payload);
            }
        }

        private static void DispatchInterstitial(BridgeEvent kind, Payload payload)
        {
            IDioInterstitialCallbacks target = interstitial;
            if (target == null)
            {
                return;
            }

            DioAdInfo info = Info(payload, DioAdUnitType.Interstitial);
            var error = new DioError(payload.message);

            switch (kind)
            {
                case BridgeEvent.InterstitialLoaded:
                    DioCallbackDispatcher.Post(() => target.Loaded(info));
                    break;
                case BridgeEvent.InterstitialNoFill:
                    DioCallbackDispatcher.Post(() => target.NoFill(payload.placementId, error));
                    break;
                case BridgeEvent.InterstitialLoadFailed:
                    DioCallbackDispatcher.Post(() => target.LoadFailed(payload.placementId, error));
                    break;
                case BridgeEvent.InterstitialShown:
                    DioCallbackDispatcher.Post(() => target.Shown(info));
                    break;
                case BridgeEvent.InterstitialShowFailed:
                    DioCallbackDispatcher.Post(() => target.ShowFailed(info, error));
                    break;
                case BridgeEvent.InterstitialClicked:
                    DioCallbackDispatcher.Post(() => target.Clicked(info));
                    break;
                case BridgeEvent.InterstitialClosed:
                    DioCallbackDispatcher.Post(() => target.Closed(info));
                    break;
                case BridgeEvent.InterstitialCompleted:
                    DioCallbackDispatcher.Post(() => target.Completed(info));
                    break;
            }
        }

        private static void DispatchInline(BridgeEvent kind, Payload payload)
        {
            if (payload.placementId == null
                || !inlineTargets.TryGetValue(payload.placementId, out InlineTarget target))
            {
                return;
            }

            IDioInlineAdCallbacks callbacks = target.Callbacks;
            DioAdInfo info = Info(payload, target.AdUnitType);
            var error = new DioError(payload.message);

            switch (kind)
            {
                case BridgeEvent.InlineLoaded:
                    DioCallbackDispatcher.Post(() => callbacks.Loaded(info));
                    break;
                case BridgeEvent.InlineNoFill:
                    DioCallbackDispatcher.Post(() => callbacks.NoFill(error));
                    break;
                case BridgeEvent.InlineLoadFailed:
                    DioCallbackDispatcher.Post(() => callbacks.LoadFailed(error));
                    break;
                case BridgeEvent.InlineShown:
                    DioCallbackDispatcher.Post(() => callbacks.Shown(info));
                    break;
                case BridgeEvent.InlineShowFailed:
                    DioCallbackDispatcher.Post(() => callbacks.ShowFailed(info, error));
                    break;
                case BridgeEvent.InlineClicked:
                    DioCallbackDispatcher.Post(() => callbacks.Clicked(info));
                    break;
                case BridgeEvent.InlineClosed:
                    DioCallbackDispatcher.Post(() => callbacks.Closed(info));
                    break;
                case BridgeEvent.InlineCompleted:
                    DioCallbackDispatcher.Post(() => callbacks.Completed(info));
                    break;
            }
        }

        private static DioAdInfo Info(Payload payload, DioAdUnitType type) =>
            new DioAdInfo(
                payload.placementId,
                payload.requestId,
                type,
                payload.advertiserName,
                payload.adomain,
                payload.campaignId,
                payload.creativeId,
                payload.auctionId,
                payload.ecpm,
                payload.ttl);

        private static Payload Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<Payload>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DioAds] Failed to parse native payload: {e.Message}");
                return null;
            }
        }
    }
}
#endif
