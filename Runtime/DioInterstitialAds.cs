using System;
using DisplayIO.Ads.Internal;

namespace DisplayIO.Ads
{
    /// <summary>Interstitial ads. Reached through <see cref="DioAds.Interstitial"/>.</summary>
    public class DioInterstitialAds : IDioInterstitialCallbacks
    {
        /// <summary>An ad is ready to show.</summary>
        public event Action<DioAdInfo> OnLoaded;

        /// <summary>No ad was available. Normal operation, not an error.</summary>
        public event Action<DioAdInfo> OnNoFill;

        /// <summary>Loading failed.</summary>
        public event Action<DioAdInfo, DioError> OnLoadFailed;

        public event Action<DioAdInfo> OnShown;
        public event Action<DioAdInfo, DioError> OnShowFailed;
        public event Action<DioAdInfo> OnClicked;
        public event Action<DioAdInfo> OnClosed;

        /// <summary>Video or audio playback finished.</summary>
        public event Action<DioAdInfo> OnCompleted;

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly DioAndroidInterstitial impl;
#endif

        internal DioInterstitialAds()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            impl = new DioAndroidInterstitial(this);
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.RegisterInterstitial(this);
#endif
        }

        /// <summary>Requests an ad for the given placement.</summary>
        public void Load(string placementId)
        {
            if (string.IsNullOrEmpty(placementId))
            {
                RaiseLoadFailed(placementId, new DioError("Placement id must not be empty"));
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            impl.Load(placementId);
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.InterstitialLoad(placementId);
#else
            RaiseLoadFailed(placementId, new DioError("display.io ads are only available on Android"));
#endif
        }

        /// <summary>True when an ad for this placement is loaded and can be shown.</summary>
        public bool IsReady(string placementId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return !string.IsNullOrEmpty(placementId) && impl.IsReady(placementId);
#elif UNITY_IOS && !UNITY_EDITOR
            return !string.IsNullOrEmpty(placementId) && DioIosBridge.InterstitialIsReady(placementId);
#else
            return false;
#endif
        }

        /// <summary>Shows the loaded ad for this placement.</summary>
        public void Show(string placementId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            impl.Show(placementId);
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.InterstitialShow(placementId);
#else
            RaiseShowFailed(
                DioAdInfoFactory.Minimal(placementId, DioAdUnitType.Interstitial),
                new DioError("display.io ads are only available on Android"));
#endif
        }

        void IDioInterstitialCallbacks.Loaded(DioAdInfo ad) => OnLoaded?.Invoke(ad);

        void IDioInterstitialCallbacks.NoFill(string placementId, DioError error) =>
            OnNoFill?.Invoke(DioAdInfoFactory.Minimal(placementId, DioAdUnitType.Interstitial));

        void IDioInterstitialCallbacks.LoadFailed(string placementId, DioError error) =>
            RaiseLoadFailed(placementId, error);

        void IDioInterstitialCallbacks.Shown(DioAdInfo ad) => OnShown?.Invoke(ad);

        void IDioInterstitialCallbacks.ShowFailed(DioAdInfo ad, DioError error) =>
            RaiseShowFailed(ad, error);

        void IDioInterstitialCallbacks.Clicked(DioAdInfo ad) => OnClicked?.Invoke(ad);

        void IDioInterstitialCallbacks.Closed(DioAdInfo ad) => OnClosed?.Invoke(ad);

        void IDioInterstitialCallbacks.Completed(DioAdInfo ad) => OnCompleted?.Invoke(ad);

        private void RaiseLoadFailed(string placementId, DioError error) =>
            OnLoadFailed?.Invoke(
                DioAdInfoFactory.Minimal(placementId, DioAdUnitType.Interstitial), error);

        private void RaiseShowFailed(DioAdInfo ad, DioError error) =>
            OnShowFailed?.Invoke(ad, error);
    }
}
