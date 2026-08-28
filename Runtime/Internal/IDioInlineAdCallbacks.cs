namespace DisplayIO.Ads.Internal
{
    /// <summary>Callbacks for inline (view-attached) ads, raised on the Unity main thread.</summary>
    internal interface IDioInlineAdCallbacks
    {
        void Loaded(DioAdInfo ad);
        void NoFill(DioError error);
        void LoadFailed(DioError error);
        void Shown(DioAdInfo ad);
        void ShowFailed(DioAdInfo ad, DioError error);
        void Clicked(DioAdInfo ad);
        void Closed(DioAdInfo ad);
        void Completed(DioAdInfo ad);
    }
}
