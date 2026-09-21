using System;
using DisplayIO.Ads.Internal;
using UnityEngine;

namespace DisplayIO.Ads
{
    /// <summary>
    /// An in-game audio ad for one placement, created via <see cref="DioAds.InGameAudio"/>.
    /// Load fetches the ad, Show inserts the card on screen, Hide removes it again.
    /// The card never closes itself — Hide or Destroy is the publisher's responsibility.
    /// </summary>
    public class DioInGameAudioAd : IDioInlineAdCallbacks
    {
        public string PlacementId { get; }

        /// <summary>Where the card sits. Applied immediately when already shown.</summary>
        public DioAdPosition Position
        {
            get => position;
            set { position = value; ApplyLayoutIfShown(); }
        }

        /// <summary>
        /// Offset from the anchored edges, in density-independent units — dp on Android,
        /// points on iOS. X moves the card away from the left edge, or from the right edge for
        /// right-anchored positions; Y away from the top edge, or from the bottom edge for
        /// bottom-anchored positions.
        /// </summary>
        public Vector2 Offset
        {
            get => offset;
            set { offset = value; ApplyLayoutIfShown(); }
        }

        /// <summary>
        /// Which axes keep the card clear of notches and system bars. Defaults to both;
        /// set it per axis to pin the card to a physical screen edge.
        /// </summary>
        public DioSafeArea SafeArea
        {
            get => safeArea;
            set { safeArea = value; ApplyLayoutIfShown(); }
        }

        /// <summary>
        /// Side of the square card, in density-independent units — dp on Android, points on
        /// iOS. Zero leaves the platform default (100 on Android).
        /// Applied on the next Load — changing it while an ad is loaded has no effect.
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Look of the card — colours, corner radius, ring width, which overlay elements are
        /// drawn, and an image of your own. Null keeps the SDK defaults.
        /// Applied on the next Load — changing it while an ad is loaded has no effect.
        /// </summary>
        public DioInGameAudioStyle Style { get; set; }

        /// <summary>
        /// Whether the card is drawn at all. Set it to false to play the ad as background audio
        /// with no UI: Show and Hide then do nothing, and <see cref="Play"/> and
        /// <see cref="Pause"/> drive playback instead. Nothing is heard until Play is called.
        /// Applied on the next Load.
        /// </summary>
        public bool ShowCard { get; set; } = true;

        /// <summary>
        /// Whether the campaign's companion image may be shown inside the card. Set it to false
        /// and the SDK does not even load the companion, leaving the card on its background.
        /// Applied on the next Load, because the companion is resolved while the bid is parsed.
        /// </summary>
        public bool CompanionEnabled { get; set; } = true;

        public event Action<DioAdInfo> OnLoaded;
        public event Action<DioAdInfo> OnNoFill;
        public event Action<DioAdInfo, DioError> OnLoadFailed;
        public event Action<DioAdInfo> OnShown;
        public event Action<DioAdInfo, DioError> OnShowFailed;
        public event Action<DioAdInfo> OnClicked;
        public event Action<DioAdInfo> OnClosed;
        public event Action<DioAdInfo> OnCompleted;

        private DioAdPosition position = DioAdPosition.BottomLeft;
        private Vector2 offset = new Vector2(16f, 32f);
        private DioSafeArea safeArea = DioSafeArea.All;
        private bool shown;

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly DioAndroidInlineAd impl;
#endif

        internal DioInGameAudioAd(string placementId)
        {
            PlacementId = placementId;
#if UNITY_ANDROID && !UNITY_EDITOR
            impl = new DioAndroidInlineAd(placementId, DioAdUnitType.InGameAudio, this);
            impl.ConfigurePlacement = placement =>
            {
                if (Size > 0)
                {
                    placement.Call("setCustomWidth", Size);
                }

                placement.Call("setShowCard", ShowCard);
                placement.Call("setCompanionEnabled", CompanionEnabled);
                ApplyStyleAndroid(placement);
            };
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.RegisterInline(placementId, DioAdUnitType.InGameAudio, this);
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
            ApplyStyleIos();
            DioIosBridge.InlineLoad(PlacementId, Size);
#else
            ((IDioInlineAdCallbacks)this).LoadFailed(
                new DioError("display.io ads are only available on Android"));
#endif
        }

        /// <summary>
        /// Inserts the loaded card on screen. Does nothing when <see cref="ShowCard"/> is false,
        /// because that mode has no card to insert.
        /// </summary>
        public void Show()
        {
            if (!ShowCard)
            {
                return;
            }

            shown = true;
            ApplyLayout();
        }

        /// <summary>
        /// Removes the card from screen. The fetched ad stays usable for another Show.
        /// Does nothing when <see cref="ShowCard"/> is false.
        /// </summary>
        public void Hide()
        {
            if (!ShowCard)
            {
                return;
            }

            shown = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            impl.Hide();
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.InlineHide(PlacementId);
#endif
        }

        /// <summary>Removes the card and releases everything held for this placement.</summary>
        public void Destroy()
        {
            shown = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            impl.Destroy();
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.InlineDestroy(PlacementId);
#endif
        }

        /// <summary>
        /// Starts or resumes playback. Required when <see cref="ShowCard"/> is false, where
        /// nothing plays on its own. With a card the SDK drives playback from viewability and
        /// this is only needed to resume after <see cref="Pause"/>.
        /// </summary>
        public void Play()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            impl.CallOnAd("play");
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.InGamePlay(PlacementId);
#endif
        }

        /// <summary>Pauses playback. Resume with <see cref="Play"/>.</summary>
        public void Pause()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            impl.CallOnAd("pause");
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.InGamePause(PlacementId);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void ApplyStyleAndroid(AndroidJavaObject placement)
        {
            if (Style == null)
            {
                placement.Call("setStyle", (AndroidJavaObject)null);
                return;
            }

            using (var style = new AndroidJavaObject("com.brandio.ads.placements.InGameAudioStyle"))
            {
                SetColorAndroid(style, "setRingTrackColor", Style.RingTrackColor);
                SetColorAndroid(style, "setRingProgressColor", Style.RingProgressColor);
                SetColorAndroid(style, "setAccentColor", Style.AccentColor);
                SetColorAndroid(style, "setBadgeBackgroundColor", Style.BadgeBackgroundColor);
                SetColorAndroid(style, "setBadgeTextColor", Style.BadgeTextColor);

                // setBackgroundColors takes both ends at once, so it cannot reuse the helper.
                if (Style.BackgroundTopLeft.HasValue || Style.BackgroundBottomRight.HasValue)
                {
                    style.Call("setBackgroundColors",
                        BoxedColorAndroid(Style.BackgroundTopLeft),
                        BoxedColorAndroid(Style.BackgroundBottomRight));
                }

                style.Call("setCornerRadiusDp", Style.CornerRadius);
                style.Call("setRingWidthDp", Style.RingWidth);
                style.Call("setShowProgressRing", Style.ShowProgressRing);
                style.Call("setShowAdBadge", Style.ShowAdBadge);
                style.Call("setShowNowPlayingGlyph", Style.ShowNowPlayingGlyph);
                style.Call("setIconPaddingDp", Style.IconPadding);

                byte[] icon = Style.ResolveIconBytes(out string iconError);

                if (iconError != null)
                {
                    // No event expresses this: the ad itself is fine, only the decoration is
                    // missing, and it is a project setup mistake the publisher has to see.
                    Debug.LogError(iconError);
                }

                if (icon != null)
                {
                    style.Call("setIcon", icon);
                }

                placement.Call("setStyle", style);
            }
        }

        private static void SetColorAndroid(
            AndroidJavaObject style, string setter, Color? color)
        {
            if (color.HasValue)
            {
                style.Call(setter, BoxedColorAndroid(color));
            }
        }

        /// <summary>
        /// Wraps a colour as java.lang.Integer, which is what the SDK's nullable colour setters
        /// take — a C# int would bind to the primitive overload and lose the "unset" meaning.
        /// </summary>
        private static AndroidJavaObject BoxedColorAndroid(Color? color)
        {
            if (!color.HasValue)
            {
                return null;
            }

            return new AndroidJavaObject("java.lang.Integer", (int)DioInGameAudioStyle.Packed(color));
        }
#endif

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
                DioAdInfoFactory.Minimal(PlacementId, DioAdUnitType.InGameAudio),
                new DioError("display.io ads are only available on Android"));
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private void ApplyStyleIos()
        {
            DioInGameAudioStyle style = Style ?? new DioInGameAudioStyle();

            DioIosBridge.InGameSetOptions(
                PlacementId, ShowCard, CompanionEnabled,
                DioInGameAudioStyle.Packed(style.BackgroundTopLeft),
                DioInGameAudioStyle.Packed(style.BackgroundBottomRight),
                DioInGameAudioStyle.Packed(style.RingTrackColor),
                DioInGameAudioStyle.Packed(style.RingProgressColor),
                DioInGameAudioStyle.Packed(style.AccentColor),
                DioInGameAudioStyle.Packed(style.BadgeBackgroundColor),
                DioInGameAudioStyle.Packed(style.BadgeTextColor),
                style.CornerRadius, style.RingWidth,
                style.ShowProgressRing, style.ShowAdBadge, style.ShowNowPlayingGlyph,
                style.IconPadding);

            if (Style == null)
            {
                DioIosBridge.InGameSetIcon(PlacementId, null, 0);
                return;
            }

            byte[] icon = Style.ResolveIconBytes(out string iconError);

            if (iconError != null)
            {
                // No event expresses this: the ad itself is fine, only the decoration is
                // missing, and it is a project setup mistake the publisher has to see.
                Debug.LogError(iconError);
            }

            DioIosBridge.InGameSetIcon(PlacementId, icon, icon?.Length ?? 0);
        }
#endif

        void IDioInlineAdCallbacks.Loaded(DioAdInfo ad) => OnLoaded?.Invoke(ad);

        void IDioInlineAdCallbacks.NoFill(DioError error) =>
            OnNoFill?.Invoke(DioAdInfoFactory.Minimal(PlacementId, DioAdUnitType.InGameAudio));

        void IDioInlineAdCallbacks.LoadFailed(DioError error) =>
            OnLoadFailed?.Invoke(
                DioAdInfoFactory.Minimal(PlacementId, DioAdUnitType.InGameAudio), error);

        void IDioInlineAdCallbacks.Shown(DioAdInfo ad) => OnShown?.Invoke(ad);
        void IDioInlineAdCallbacks.ShowFailed(DioAdInfo ad, DioError error) => OnShowFailed?.Invoke(ad, error);
        void IDioInlineAdCallbacks.Clicked(DioAdInfo ad) => OnClicked?.Invoke(ad);
        void IDioInlineAdCallbacks.Closed(DioAdInfo ad) => OnClosed?.Invoke(ad);
        void IDioInlineAdCallbacks.Completed(DioAdInfo ad) => OnCompleted?.Invoke(ad);
    }
}
