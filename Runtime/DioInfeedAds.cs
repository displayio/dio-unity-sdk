namespace DisplayIO.Ads
{
    /// <summary>Creates infeed ads. Reached through <see cref="DioAds.Infeed"/>.</summary>
    public class DioInfeedAds
    {
        internal DioInfeedAds()
        {
        }

        /// <summary>
        /// Creates an infeed ad for one placement. The caller owns it and must call Destroy()
        /// when done — the ad view is not removed automatically.
        /// </summary>
        public DioInfeedAd Create(string placementId) => new DioInfeedAd(placementId);
    }
}
