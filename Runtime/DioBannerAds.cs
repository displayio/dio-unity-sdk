namespace DisplayIO.Ads
{
    /// <summary>Creates banner ads. Reached through <see cref="DioAds.Banner"/>.</summary>
    public class DioBannerAds
    {
        internal DioBannerAds()
        {
        }

        /// <summary>
        /// Creates a banner for one placement. The caller owns it and must call Destroy()
        /// when done — the ad view is not removed automatically.
        /// </summary>
        public DioBannerAd Create(string placementId) => new DioBannerAd(placementId);
    }
}
