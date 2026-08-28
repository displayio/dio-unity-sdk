namespace DisplayIO.Ads
{
    /// <summary>Creates in-game audio ads. Reached through <see cref="DioAds.InGameAudio"/>.</summary>
    public class DioInGameAudioAds
    {
        internal DioInGameAudioAds()
        {
        }

        /// <summary>
        /// Creates an in-game audio ad for one placement. The caller owns it and must call
        /// Destroy() when done — the card is never removed automatically.
        /// </summary>
        public DioInGameAudioAd Create(string placementId) => new DioInGameAudioAd(placementId);
    }
}
