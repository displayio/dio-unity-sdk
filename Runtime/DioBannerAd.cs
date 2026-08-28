using System;
using DisplayIO.Ads.Internal;
using UnityEngine;

namespace DisplayIO.Ads
{
    /// <summary>
    /// A banner ad for one placement, created via <see cref="DioAds.Banner"/>.
    /// Load fetches the ad, Show inserts it on screen, Hide removes it again.
    /// </summary>
    public class DioBannerAd : IDioInlineAdCallbacks
    {
        public string PlacementId { get; }

        /// <summary>Where the banner sits. Applied immediately when already shown.</summary>
        public DioAdPosition Position
        {
            get => position;
            set { position = value; ApplyLayoutIfShown(); }
        }

        /// <summary>
        /// Offset from the anchored edges, in density-independent units — dp on Android,
        /// points on iOS. X moves the banner away from the left edge, or from the right edge
        /// for right-anchored positions; Y away from the top edge, or from the bottom edge for
        /// bottom-anchored positions.
        /// </summary>
        public Vector2 Offset
        {
            get => offset;
            set { offset = value; ApplyLayoutIfShown(); }
        }

        /// <summary>
        /// Which axes keep the banner clear of notches and system bars. Defaults to both;
        /// set it per axis to pin the banner to a physical screen edge.
        /// </summary>
        public DioSafeArea SafeArea
        {
            get => safeArea;
            set { safeArea = value; ApplyLayoutIfShown(); }
        }

        public event Action<DioAdInfo> OnLoaded;
        public event Action<DioAdInfo> OnNoFill;
        public event Action<DioAdInfo, DioError> OnLoadFailed;
        public event Action<DioAdInfo> OnShown;
        public event Action<DioAdInfo, DioError> OnShowFailed;
        public event Action<DioAdInfo> OnClicked;
        public event Action<DioAdInfo> OnClosed;
        public event Action<DioAdInfo> OnCompleted;

        private DioAdPosition position = DioAdPosition.BottomCenter;
        private Vector2 offset = Vector2.zero;
        private DioSafeArea safeArea = DioSafeArea.All;
        private bool shown;

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly DioAndroidInlineAd impl;
#endif

        internal DioBannerAd(string placementId)
        {
            PlacementId = placementId;
#if UNITY_ANDROID && !UNITY_EDITOR
            impl = new DioAndroidInlineAd(placementId, DioAdUnitType.Banner, this);
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.RegisterInline(placementId, DioAdUnitType.Banner, this);
#endif
        }

        /// <summary>True once an ad has been fetched and can be shown.</summary>
        public bool IsLoaded
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            get => impl.IsLoaded;
#elif UNITY_IOS && !UNITY_EDITOR
            get => DioIosBridge.InlineIsLoaded(PlacementId);
#else
            get => false;
#endif
        }

        /// <summary>Fetches an ad. Does not put anything on screen.</summary>
        public void Load()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            impl.Load();
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.InlineLoad(PlacementId, 0);
#else
            ((IDioInlineAdCallbacks)this).LoadFailed(
                new DioError("display.io ads are only available on Android"));
#endif
        }

        /// <summary>Inserts the loaded ad on screen.</summary>
        public void Show()
        {
            shown = true;
            ApplyLayout();
        }

        /// <summary>Removes the ad from screen. The fetched ad stays usable for another Show.</summary>
        public void Hide()
        {
            shown = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            impl.Hide();
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.InlineHide(PlacementId);
#endif
        }

        /// <summary>Removes the ad and releases everything held for this placement.</summary>
        public void Destroy()
        {
            shown = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            impl.Destroy();
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.InlineDestroy(PlacementId);
#endif
        }

        private void ApplyLayoutIfShown()
        {
            if (shown)
            {
                ApplyLayout();
            }
        }

        private void ApplyLayout()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            DioScreenMetrics.Margins(position, offset, safeArea,
                out int left, out int top, out int right, out int bottom);
            impl.Show(position, left, top, right, bottom);
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS works in points and resolves safe-area insets natively, so the position
            // is passed through as-is instead of being pre-converted to pixels.
            DioIosBridge.InlineShow(PlacementId, position, offset, safeArea);
#else
            ((IDioInlineAdCallbacks)this).ShowFailed(
                DioAdInfoFactory.Minimal(PlacementId, DioAdUnitType.Banner),
                new DioError("display.io ads are only available on Android"));
#endif
        }

        void IDioInlineAdCallbacks.Loaded(DioAdInfo ad) => OnLoaded?.Invoke(ad);

        void IDioInlineAdCallbacks.NoFill(DioError error) =>
            OnNoFill?.Invoke(DioAdInfoFactory.Minimal(PlacementId, DioAdUnitType.Banner));

        void IDioInlineAdCallbacks.LoadFailed(DioError error) =>
            OnLoadFailed?.Invoke(DioAdInfoFactory.Minimal(PlacementId, DioAdUnitType.Banner), error);

        void IDioInlineAdCallbacks.Shown(DioAdInfo ad) => OnShown?.Invoke(ad);
        void IDioInlineAdCallbacks.ShowFailed(DioAdInfo ad, DioError error) => OnShowFailed?.Invoke(ad, error);
        void IDioInlineAdCallbacks.Clicked(DioAdInfo ad) => OnClicked?.Invoke(ad);
        void IDioInlineAdCallbacks.Closed(DioAdInfo ad) => OnClosed?.Invoke(ad);
        void IDioInlineAdCallbacks.Completed(DioAdInfo ad) => OnCompleted?.Invoke(ad);
    }
}
