using System;

namespace DisplayIO.Ads
{
    /// <summary>
    /// Which axes keep clear of notches and system bars. Applied per axis so a publisher can,
    /// for example, respect the notch at the top while pinning an ad to the very bottom edge.
    /// </summary>
    [Flags]
    public enum DioSafeArea
    {
        None = 0,
        Horizontal = 1,
        Vertical = 2,
        All = Horizontal | Vertical
    }
}
