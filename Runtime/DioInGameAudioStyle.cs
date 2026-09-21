using UnityEngine;

namespace DisplayIO.Ads
{
    /// <summary>
    /// Look of the in-game audio card. Assign it to <see cref="DioInGameAudioAd.Style"/> before
    /// Load; changing it afterwards has no effect on an ad that is already fetched.
    /// Every colour is optional — leave one null and the SDK keeps its own default.
    /// </summary>
    public class DioInGameAudioStyle
    {
        /// <summary>Top-left colour of the card's background gradient.</summary>
        public Color? BackgroundTopLeft;

        /// <summary>Bottom-right colour of the card's background gradient.</summary>
        public Color? BackgroundBottomRight;

        /// <summary>Unfilled part of the progress ring around the card.</summary>
        public Color? RingTrackColor;

        /// <summary>Filled part of the progress ring. Falls back to <see cref="AccentColor"/>.</summary>
        public Color? RingProgressColor;

        /// <summary>
        /// Colours the audio bars, the now-playing indicator and the progress ring at once.
        /// Set a ring or badge colour explicitly only when it should differ from the accent.
        /// </summary>
        public Color? AccentColor;

        /// <summary>Background of the "AD" badge.</summary>
        public Color? BadgeBackgroundColor;

        /// <summary>Text of the "AD" badge.</summary>
        public Color? BadgeTextColor;

        /// <summary>Corner radius of the card, in density-independent units. Default 14.</summary>
        public int CornerRadius = 14;

        /// <summary>Stroke width of the progress ring, in density-independent units. Default 3.</summary>
        public int RingWidth = 3;

        /// <summary>Draw the progress ring around the card.</summary>
        public bool ShowProgressRing = true;

        /// <summary>Draw the "AD" badge in the bottom-left corner.</summary>
        public bool ShowAdBadge = true;

        /// <summary>Draw the animated now-playing indicator in the bottom-right corner.</summary>
        public bool ShowNowPlayingGlyph = true;

        /// <summary>
        /// Your own image inside the card. It fills the card's content inset by
        /// <see cref="IconPadding"/>, with the background showing through the gap, and it
        /// replaces the default audio bars. The image is decorative: it takes no touches, so the
        /// whole card stays clickable.
        ///
        /// The texture must be readable — tick <b>Read/Write Enabled</b> on its import settings,
        /// or build it in code with <c>new Texture2D(...)</c>. An unreadable texture cannot be
        /// encoded, so the card falls back to the default bars and the reason is logged — the ad
        /// itself still loads and plays. Use <see cref="IconBytes"/> to bypass encoding entirely.
        /// </summary>
        public Texture2D Icon;

        /// <summary>
        /// The card image as already-encoded PNG or JPEG bytes. Takes precedence over
        /// <see cref="Icon"/> and skips the readability requirement, so it is the cheaper option
        /// when the image arrives from disk or the network rather than from the project.
        /// </summary>
        public byte[] IconBytes;

        /// <summary>Gap between the card edge and your image, in density-independent units. Default 8.</summary>
        public int IconPadding = 8;

        /// <summary>
        /// Resolves the image to bytes, preferring <see cref="IconBytes"/>. Returns null when no
        /// image is set, and reports why through <paramref name="error"/> when one is set but
        /// cannot be encoded.
        /// </summary>
        internal byte[] ResolveIconBytes(out string error)
        {
            error = null;

            if (IconBytes != null && IconBytes.Length > 0)
            {
                return IconBytes;
            }

            if (Icon == null)
            {
                return null;
            }

            if (!Icon.isReadable)
            {
                error = $"In-game audio icon texture '{Icon.name}' is not readable. " +
                        "Enable Read/Write on its import settings, or set IconBytes instead.";
                return null;
            }

            try
            {
                byte[] encoded = Icon.EncodeToPNG();

                if (encoded == null || encoded.Length == 0)
                {
                    error = $"In-game audio icon texture '{Icon.name}' could not be encoded to PNG.";
                    return null;
                }

                return encoded;
            }
            catch (System.Exception e)
            {
                error = $"In-game audio icon texture '{Icon.name}' could not be encoded: {e.Message}";
                return null;
            }
        }

        /// <summary>Packs a colour as 0xAARRGGBB, or -1 when it is not set.</summary>
        internal static long Packed(Color? color)
        {
            if (!color.HasValue)
            {
                return -1;
            }

            Color32 c = color.Value;
            return ((long)c.a << 24) | ((long)c.r << 16) | ((long)c.g << 8) | c.b;
        }
    }
}
