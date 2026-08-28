using UnityEngine;

namespace DisplayIO.Ads.Internal
{
    /// <summary>
    /// Converts a position plus an offset in density-independent units into Android pixel
    /// margins. Reads Screen.safeArea, so it must be called on the Unity main thread.
    /// </summary>
    internal static class DioScreenMetrics
    {
        internal static void Margins(
            DioAdPosition position, Vector2 offset, DioSafeArea safeArea,
            out int left, out int top, out int right, out int bottom)
        {
            float scale = Screen.dpi > 0f ? Screen.dpi / 160f : 1f;
            int x = Mathf.RoundToInt(offset.x * scale);
            int y = Mathf.RoundToInt(offset.y * scale);

            int safeLeft = 0, safeTop = 0, safeRight = 0, safeBottom = 0;

            Rect safe = Screen.safeArea;

            if ((safeArea & DioSafeArea.Horizontal) != 0)
            {
                safeLeft = Mathf.RoundToInt(safe.xMin);
                safeRight = Mathf.RoundToInt(Screen.width - safe.xMax);
            }

            if ((safeArea & DioSafeArea.Vertical) != 0)
            {
                safeBottom = Mathf.RoundToInt(safe.yMin);
                safeTop = Mathf.RoundToInt(Screen.height - safe.yMax);
            }

            bool anchoredRight = position == DioAdPosition.TopRight
                              || position == DioAdPosition.CenterRight
                              || position == DioAdPosition.BottomRight;

            bool anchoredBottom = position == DioAdPosition.BottomLeft
                               || position == DioAdPosition.BottomCenter
                               || position == DioAdPosition.BottomRight;

            left = anchoredRight ? safeLeft : safeLeft + x;
            right = anchoredRight ? safeRight + x : safeRight;
            top = anchoredBottom ? safeTop : safeTop + y;
            bottom = anchoredBottom ? safeBottom + y : safeBottom;
        }
    }
}
