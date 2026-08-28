namespace DisplayIO.Ads.Internal
{
    /// <summary>Callbacks the native layer reports to the facade, on the Unity main thread.</summary>
    internal interface IDioInterstitialCallbacks
    {
        void Loaded(DioAdInfo ad);
        void NoFill(string placementId, DioError error);
        void LoadFailed(string placementId, DioError error);
        void Shown(DioAdInfo ad);
        void ShowFailed(DioAdInfo ad, DioError error);
        void Clicked(DioAdInfo ad);
        void Closed(DioAdInfo ad);
        void Completed(DioAdInfo ad);
    }
}
