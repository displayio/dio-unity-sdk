using System;
using DisplayIO.Ads.Internal;

namespace DisplayIO.Ads
{
    /// <summary>Entry point for the display.io Direct SDK.</summary>
    public static class DioAds
    {
        /// <summary>Raised on the Unity main thread once the SDK is ready.</summary>
        public static event Action OnInitialized;

        /// <summary>Raised on the Unity main thread if initialization fails.</summary>
        public static event Action<DioError> OnInitializationFailed;

        private static bool initializing;

        private static DioInterstitialAds interstitial;
        private static DioBannerAds banner;
        private static DioInGameAudioAds inGameAudio;
        private static DioInfeedAds infeed;

        /// <summary>Interstitial ads.</summary>
        public static DioInterstitialAds Interstitial =>
            interstitial ??= new DioInterstitialAds();

        /// <summary>Banner ads.</summary>
        public static DioBannerAds Banner =>
            banner ??= new DioBannerAds();

        /// <summary>In-game audio ads.</summary>
        public static DioInGameAudioAds InGameAudio =>
            inGameAudio ??= new DioInGameAudioAds();

        /// <summary>Infeed ads.</summary>
        public static DioInfeedAds Infeed =>
            infeed ??= new DioInfeedAds();

        public static bool IsInitialized
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return DioAndroidBridge.IsInitialized();
#elif UNITY_IOS && !UNITY_EDITOR
                return DioIosBridge.IsInitialized();
#else
                return false;
#endif
            }
        }

        /// <summary>Native SDK version, or null when unavailable.</summary>
        public static string SdkVersion
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return DioAndroidBridge.GetVersion();
#elif UNITY_IOS && !UNITY_EDITOR
                return DioIosBridge.GetVersion();
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// Initializes the SDK. Safe to call more than once; subsequent calls while a
        /// previous one is in flight are ignored.
        /// </summary>
        public static void Initialize(string appId)
        {
            if (string.IsNullOrEmpty(appId))
            {
                Fail(new DioError("App id must not be empty"));
                return;
            }

            if (IsInitialized)
            {
                Succeed();
                return;
            }

            if (initializing)
            {
                return;
            }

            initializing = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            DioAndroidBridge.Initialize(appId, Succeed, Fail);
#elif UNITY_IOS && !UNITY_EDITOR
            DioIosBridge.Initialize(appId, Succeed, Fail);
#else
            Fail(new DioError("display.io ads require Android or iOS"));
#endif
        }

        private static void Succeed()
        {
            initializing = false;
            OnInitialized?.Invoke();
        }

        private static void Fail(DioError error)
        {
            // No logging here: the failure is reported through OnInitializationFailed, and a
            // library should not also write to a console the publisher cannot silence.
            initializing = false;
            OnInitializationFailed?.Invoke(error);
        }
    }
}
