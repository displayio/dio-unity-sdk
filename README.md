# display.io Ads for Unity

Unity plugin for the display.io Direct SDK: interstitial, banner, infeed and in-game audio ads.

## Install

In Unity, open **Window → Package Manager → + → Add package from git URL** and enter:

```
https://github.com/displayio/dio-unity-sdk.git#1.0.0
```

Native dependencies are declared by the package and installed by **External Dependency Manager
for Unity** (EDM4U), which must be present in the project. Most projects already have it —
AppLovin MAX and Google Mobile Ads both ship it. If yours does not, add the scoped registry
`https://package.openupm.com` for `com.google.external-dependency-manager`, or install Google's
own `.unitypackage`.

On Android EDM4U writes `com.brandio.ads:sdk` and the display.io maven repository into the
gradle templates; on iOS it adds the `DIOSDK/Core` pod to the Podfile. Nothing has to be edited
by hand.

## Requirements

- Unity 2021.3 or newer
- Android and iOS; there is no editor playback, ads only run on a device

## Initialize

Initialize once, early. Every event below is raised on the Unity main thread, so touching the
scene or logging from a handler is safe.

```csharp
using DisplayIO.Ads;

DioAds.OnInitialized += () => Debug.Log($"DIO {DioAds.SdkVersion} ready");
DioAds.OnInitializationFailed += error => Debug.LogError(error.Message);

DioAds.Initialize("YOUR_APP_ID");
```

`DioAds.IsInitialized` reports the current state. Calling `Initialize` again while a previous
call is in flight is ignored.

## Interstitial

Interstitials are addressed by placement id; the plugin keeps the loaded ad for you.

```csharp
DioAds.Interstitial.OnLoaded  += ad => Debug.Log($"ready: {ad.PlacementId}");
DioAds.Interstitial.OnNoFill  += ad => Debug.Log("no ad available right now");
DioAds.Interstitial.OnClosed  += ad => ResumeGame();

DioAds.Interstitial.Load("PLACEMENT_ID");

// later, when it is a good moment to interrupt the player
if (DioAds.Interstitial.IsReady("PLACEMENT_ID"))
{
    PauseGame();
    DioAds.Interstitial.Show("PLACEMENT_ID");
}
```

Pause gameplay **before** `Show` and resume on `OnClosed`. A fullscreen ad suspends the Unity
player, so `OnShown` and `OnClicked` are delivered in a batch once the ad closes — they are not
a reliable moment to react to.

## Banner, infeed and in-game audio

These are objects you own. `Load` fetches, `Show` puts the ad on screen, `Hide` takes it off
again without discarding it, and `Destroy` releases everything. Nothing is removed
automatically, so call `Destroy` when the ad is no longer needed.

```csharp
var banner = DioAds.Banner.Create("PLACEMENT_ID");   // or DioAds.Infeed / DioAds.InGameAudio

banner.Position = DioAdPosition.BottomCenter;
banner.Offset = new Vector2(0, 8);
banner.SafeArea = DioSafeArea.Horizontal;

banner.OnLoaded += ad => banner.Show();
banner.OnLoadFailed += (ad, error) => Debug.LogError(error.Message);

banner.Load();
```

The ad decides its own size — you choose where it goes, not how big it is.

`Position` covers the nine combinations of top/center/bottom and left/center/right.
`Offset` moves the ad away from the edges it is anchored to, in density-independent units
(dp on Android, points on iOS). `SafeArea` controls, per axis, whether the ad keeps clear of
notches and system bars: `All` by default, or `Horizontal` to sit flush with the bottom edge
while still avoiding side cutouts.

Changing `Position`, `Offset` or `SafeArea` while the ad is on screen moves it immediately.

In-game audio adds a size, a look, and a mode with no card at all:

```csharp
var audio = DioAds.InGameAudio.Create("PLACEMENT_ID");
audio.Size = 120;   // side of the square card, applied on the next Load
```

### Styling the card

Assign a `DioInGameAudioStyle` before `Load`. Every colour is optional — leave one unset and the
SDK keeps its own default.

```csharp
audio.Style = new DioInGameAudioStyle
{
    BackgroundTopLeft = new Color32(0x7B, 0x2F, 0xF7, 0xFF),
    BackgroundBottomRight = new Color32(0xF1, 0x07, 0xA3, 0xFF),
    AccentColor = Color.yellow,          // audio bars, now-playing glyph and progress ring
    BadgeBackgroundColor = Color.yellow,
    BadgeTextColor = Color.black,
    CornerRadius = 20,
    RingWidth = 4,
};
audio.Load();
```

Individual elements can be switched off:

```csharp
audio.Style = new DioInGameAudioStyle
{
    ShowAdBadge = false,
    ShowNowPlayingGlyph = false,
    ShowProgressRing = false,
};
```

### Your own image in the card

The image fills the card's content inset by `IconPadding`, with the background showing through
the gap, and replaces the default audio bars. It is decorative — it takes no touches, so the
whole card stays clickable.

```csharp
audio.Style = new DioInGameAudioStyle
{
    Icon = brandMarkTexture,   // Texture2D, must be readable
    IconPadding = 12,
};
```

**The texture has to be readable.** Tick *Read/Write Enabled* on its import settings, or build it
in code with `new Texture2D(...)`. An unreadable texture cannot be encoded, so the card falls
back to the default bars and the reason is logged; the ad itself still loads and plays normally.

When the image already exists as PNG or JPEG bytes — loaded from disk or downloaded — pass those
instead and skip both the encode and the readability requirement:

```csharp
audio.Style = new DioInGameAudioStyle { IconBytes = pngBytes };
```

### The campaign's companion

When the campaign carries a companion image, it is shown inside the card instead of your
background and image — the advertiser's creative always wins. Turn it off and the SDK does not
even load it:

```csharp
audio.CompanionEnabled = false;
```

### Playing without a card

Some games have nowhere to put a card but can still carry a sponsored audio track. Turning the
card off keeps the audio and drops the UI entirely:

```csharp
audio.ShowCard = false;
audio.Load();

// nothing is heard until you start it
audio.Play();

// later, for example when the player opens a menu
audio.Pause();
```

In this mode `Show` and `Hide` do nothing — there is no card to insert. The impression is counted
when playback actually starts rather than on viewability, and the SDK closes the ad by itself
when the track ends. `Play` and `Pause` also work with a card on screen, where the SDK otherwise
drives playback from viewability.

## Events

Every format raises the same set:

| event | meaning |
|---|---|
| `OnLoaded` | an ad is ready |
| `OnNoFill` | no ad was available; normal operation, not an error |
| `OnLoadFailed` | loading failed, with a `DioError` |
| `OnShown` | the ad was displayed |
| `OnShowFailed` | displaying failed, with a `DioError` |
| `OnClicked` | the user tapped the ad |
| `OnClosed` | the ad was dismissed |
| `OnCompleted` | video or audio playback finished |

Handlers receive a `DioAdInfo` snapshot: `PlacementId`, `RequestId`, `AdUnitType`,
`AdvertiserName`, `AdvertiserDomains`, `CampaignId`, `CreativeId`, `AuctionId`, `Ecpm`,
`TimeToLiveSeconds`. `DioError` carries a `Message`.

## Privacy

The SDK reads IAB TCF consent from the app's own storage, so consent collection stays with your
CMP. The plugin neither collects nor overrides it.
