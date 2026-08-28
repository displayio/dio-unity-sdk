#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <DIOSDK/DIOSDK.h>

// Declared by the iOS player, not by a public header.
extern "C" UIViewController *UnityGetGLViewController();
extern "C" UIWindow *UnityGetMainWindow();

/// Ads go into the application window, not into Unity's rendering view. That view carries its
/// own layer scaling, and a UIKit subview inside it comes out geometrically stretched and
/// blurry — visible on device but not in a screen capture, which recomposites the layers.
static UIView *DioAdParentView(void) {
    UIWindow *window = UnityGetMainWindow();
    if (window != nil) {
        return window;
    }

    return UnityGetGLViewController().view;
}

// Payloads travel to C# as JSON so the native surface stays one function per concern
// instead of a dozen positional parameters.
typedef void (*DioEventCallback)(int eventType, const char *payloadJson);

typedef NS_ENUM(int, DioBridgeEvent) {
    DioBridgeEventInitialized = 0,
    DioBridgeEventInitializationFailed = 1,

    DioBridgeEventInterstitialLoaded = 10,
    DioBridgeEventInterstitialNoFill = 11,
    DioBridgeEventInterstitialLoadFailed = 12,
    DioBridgeEventInterstitialShown = 13,
    DioBridgeEventInterstitialShowFailed = 14,
    DioBridgeEventInterstitialClicked = 15,
    DioBridgeEventInterstitialClosed = 16,
    DioBridgeEventInterstitialCompleted = 17,

    DioBridgeEventInlineLoaded = 20,
    DioBridgeEventInlineNoFill = 21,
    DioBridgeEventInlineLoadFailed = 22,
    DioBridgeEventInlineShown = 23,
    DioBridgeEventInlineShowFailed = 24,
    DioBridgeEventInlineClicked = 25,
    DioBridgeEventInlineClosed = 26,
    DioBridgeEventInlineCompleted = 27
};

// Mirrors DioAdPosition in C#.
typedef NS_ENUM(int, DioPosition) {
    DioPositionTopLeft = 0,
    DioPositionTopCenter = 1,
    DioPositionTopRight = 2,
    DioPositionCenterLeft = 3,
    DioPositionCenter = 4,
    DioPositionCenterRight = 5,
    DioPositionBottomLeft = 6,
    DioPositionBottomCenter = 7,
    DioPositionBottomRight = 8
};

// Mirrors DioSafeArea in C#.
typedef NS_OPTIONS(int, DioSafeArea) {
    DioSafeAreaNone = 0,
    DioSafeAreaHorizontal = 1,
    DioSafeAreaVertical = 2
};

static DioEventCallback gEventCallback = NULL;

static NSMutableDictionary<NSString *, DIOAd *> *gInterstitials(void) {
    static NSMutableDictionary *store = nil;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ store = [NSMutableDictionary dictionary]; });
    return store;
}

static NSMutableDictionary<NSString *, DIOAd *> *gInlineAds(void) {
    static NSMutableDictionary *store = nil;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ store = [NSMutableDictionary dictionary]; });
    return store;
}

static NSMutableDictionary<NSString *, NSArray<NSLayoutConstraint *> *> *gConstraints(void) {
    static NSMutableDictionary *store = nil;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ store = [NSMutableDictionary dictionary]; });
    return store;
}

static const char *DioCopyString(NSString *value) {
    if (value == nil) {
        return NULL;
    }

    const char *utf8 = [value UTF8String];
    char *copy = (char *)malloc(strlen(utf8) + 1);
    strcpy(copy, utf8);
    return copy;
}

static NSString *DioString(const char *value) {
    return value ? [NSString stringWithUTF8String:value] : @"";
}

static void DioSend(DioBridgeEvent event, NSDictionary *payload) {
    if (gEventCallback == NULL) {
        return;
    }

    NSDictionary *body = payload ?: @{};
    NSData *data = [NSJSONSerialization dataWithJSONObject:body options:0 error:nil];
    NSString *json = data ? [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] : @"{}";
    gEventCallback((int)event, [json UTF8String]);
}

static NSDictionary *DioAdPayload(DIOAd *ad, NSString *placementId, NSString *message) {
    NSMutableDictionary *payload = [NSMutableDictionary dictionary];
    payload[@"placementId"] = placementId ?: @"";

    if (ad != nil) {
        payload[@"requestId"] = ad.requestId ?: @"";
        payload[@"advertiserName"] = [ad advertiserName] ?: @"";
        payload[@"campaignId"] = ad.cid ?: @"";
        payload[@"creativeId"] = ad.crid ?: @"";
        payload[@"auctionId"] = ad.auctionId ?: @"";
        payload[@"adomain"] = ad.adomain ?: @[];
        payload[@"ecpm"] = ad.ecpm ?: @0;
        payload[@"ttl"] = @([ad adTimeToLive]);
    }

    if (message != nil) {
        payload[@"message"] = message;
    }

    return payload;
}

/// Places the ad view inside Unity's root view. iOS has no ad containers: the SDK hands
/// over a sized UIView and the host decides where it goes.
///
/// Auto Layout, not frames: banner and infeed views carry their own constraints, and a frame
/// set here is overwritten on the next layout pass, dropping the ad into the top-left corner.
/// Constraints also keep the ad in place across rotation.
static void DioLayout(NSString *placementId, UIView *adView, int position,
                      CGFloat offsetX, CGFloat offsetY, int safeArea) {
    UIView *parent = DioAdParentView();
    if (parent == nil) {
        return;
    }

    NSArray<NSLayoutConstraint *> *previous = gConstraints()[placementId];
    if (previous != nil) {
        [NSLayoutConstraint deactivateConstraints:previous];
        [gConstraints() removeObjectForKey:placementId];
    }

    if (adView.superview != parent) {
        [adView removeFromSuperview];
        [parent addSubview:adView];
    }
    [parent bringSubviewToFront:adView];

    adView.translatesAutoresizingMaskIntoConstraints = NO;

    NSLayoutXAxisAnchor *leading;
    NSLayoutXAxisAnchor *trailing;
    NSLayoutXAxisAnchor *centerX;
    NSLayoutYAxisAnchor *top;
    NSLayoutYAxisAnchor *bottom;
    NSLayoutYAxisAnchor *centerY;

    UILayoutGuide *guide = parent.safeAreaLayoutGuide;

    if ((safeArea & DioSafeAreaHorizontal) != 0) {
        leading = guide.leadingAnchor;
        trailing = guide.trailingAnchor;
        centerX = guide.centerXAnchor;
    } else {
        leading = parent.leadingAnchor;
        trailing = parent.trailingAnchor;
        centerX = parent.centerXAnchor;
    }

    if ((safeArea & DioSafeAreaVertical) != 0) {
        top = guide.topAnchor;
        bottom = guide.bottomAnchor;
        centerY = guide.centerYAnchor;
    } else {
        top = parent.topAnchor;
        bottom = parent.bottomAnchor;
        centerY = parent.centerYAnchor;
    }

    NSMutableArray<NSLayoutConstraint *> *constraints = [NSMutableArray array];

    switch (position) {
        case DioPositionTopLeft:
        case DioPositionCenterLeft:
        case DioPositionBottomLeft:
            [constraints addObject:[adView.leadingAnchor constraintEqualToAnchor:leading
                                                                       constant:offsetX]];
            break;
        case DioPositionTopRight:
        case DioPositionCenterRight:
        case DioPositionBottomRight:
            [constraints addObject:[adView.trailingAnchor constraintEqualToAnchor:trailing
                                                                        constant:-offsetX]];
            break;
        default:
            [constraints addObject:[adView.centerXAnchor constraintEqualToAnchor:centerX]];
            break;
    }

    switch (position) {
        case DioPositionTopLeft:
        case DioPositionTopCenter:
        case DioPositionTopRight:
            [constraints addObject:[adView.topAnchor constraintEqualToAnchor:top
                                                                   constant:offsetY]];
            break;
        case DioPositionBottomLeft:
        case DioPositionBottomCenter:
        case DioPositionBottomRight:
            [constraints addObject:[adView.bottomAnchor constraintEqualToAnchor:bottom
                                                                      constant:-offsetY]];
            break;
        default:
            [constraints addObject:[adView.centerYAnchor constraintEqualToAnchor:centerY]];
            break;
    }

    // No width/height constraints: DIO ad views size themselves, and pinning a size here
    // stretches the creative's background while its contents keep their own size. This
    // matches the SDK's own sample (StaticViewController insertBanner).

    [NSLayoutConstraint activateConstraints:constraints];
    gConstraints()[placementId] = constraints;
}

extern "C" {

void dioSetEventCallback(DioEventCallback callback) {
    gEventCallback = callback;
}

void dioInitialize(const char *appId) {
    NSString *identifier = DioString(appId);

    [[DIOController sharedInstance] initializeWithAppId:identifier
                                      completionHandler:^{
        DioSend(DioBridgeEventInitialized, nil);
    }
                                           errorHandler:^(NSError *error) {
        DioSend(DioBridgeEventInitializationFailed,
                @{ @"message": error.localizedDescription ?: @"Unknown error" });
    }];
}

bool dioIsInitialized(void) {
    return [DIOController sharedInstance].initialized;
}

const char *dioGetSdkVersion(void) {
    return DioCopyString([[DIOController sharedInstance] getSDKVersion]);
}

// ---------- interstitial ----------

void dioInterstitialLoad(const char *placementIdRaw) {
    NSString *placementId = DioString(placementIdRaw);
    DIOPlacement *placement = [[DIOController sharedInstance] placementWithId:placementId];

    if (placement == nil) {
        DioSend(DioBridgeEventInterstitialLoadFailed,
                DioAdPayload(nil, placementId,
                             [NSString stringWithFormat:@"Placement %@ not found", placementId]));
        return;
    }

    DIOAdRequest *request = [placement newAdRequest];

    [request requestAdWithAdReceivedHandler:^(DIOAd *ad) {
        gInterstitials()[placementId] = ad;
        DioSend(DioBridgeEventInterstitialLoaded, DioAdPayload(ad, placementId, nil));
    }
                                noAdHandler:^(NSError *error) {
        BOOL noFill = error.code == kDIOErrorNoFill
                   || error.code == kDIOErrorNoAds
                   || error.code == kDIOErrorNoAd;

        DioSend(noFill ? DioBridgeEventInterstitialNoFill : DioBridgeEventInterstitialLoadFailed,
                DioAdPayload(nil, placementId, error.localizedDescription ?: @"No ad"));
    }];
}

bool dioInterstitialIsReady(const char *placementIdRaw) {
    DIOAd *ad = gInterstitials()[DioString(placementIdRaw)];
    return ad != nil && ad.loaded;
}

void dioInterstitialShow(const char *placementIdRaw) {
    NSString *placementId = DioString(placementIdRaw);
    DIOAd *ad = gInterstitials()[placementId];

    if (ad == nil) {
        DioSend(DioBridgeEventInterstitialShowFailed,
                DioAdPayload(nil, placementId, @"No interstitial loaded for this placement"));
        return;
    }

    [ad showAdFromViewController:UnityGetGLViewController() eventHandler:^(DIOAdEvent event) {
        switch (event) {
            case DIOAdEventOnShown:
                DioSend(DioBridgeEventInterstitialShown, DioAdPayload(ad, placementId, nil));
                break;
            case DIOAdEventOnFailedToShow:
                DioSend(DioBridgeEventInterstitialShowFailed,
                        DioAdPayload(ad, placementId, @"Failed to show"));
                break;
            case DIOAdEventOnClicked:
                DioSend(DioBridgeEventInterstitialClicked, DioAdPayload(ad, placementId, nil));
                break;
            case DIOAdEventOnClosed:
                [gInterstitials() removeObjectForKey:placementId];
                DioSend(DioBridgeEventInterstitialClosed, DioAdPayload(ad, placementId, nil));
                break;
            case DIOAdEventOnAdCompleted:
                DioSend(DioBridgeEventInterstitialCompleted, DioAdPayload(ad, placementId, nil));
                break;
            default:
                break;
        }
    }];
}

// ---------- inline: banner, infeed, in-game audio ----------

void dioInlineLoad(const char *placementIdRaw, int customSize) {
    NSString *placementId = DioString(placementIdRaw);
    DIOPlacement *placement = [[DIOController sharedInstance] placementWithId:placementId];

    if (placement == nil) {
        DioSend(DioBridgeEventInlineLoadFailed,
                DioAdPayload(nil, placementId,
                             [NSString stringWithFormat:@"Placement %@ not found", placementId]));
        return;
    }

    if (customSize > 0 && [placement isKindOfClass:[DIOInGameAudioPlacement class]]) {
        ((DIOInGameAudioPlacement *)placement).customWidth = customSize;
    }

    DIOAdRequest *request = [placement newAdRequest];

    [request requestAdWithAdReceivedHandler:^(DIOAd *ad) {
        gInlineAds()[placementId] = ad;

        [ad setEventHandler:^(DIOAdEvent event) {
            switch (event) {
                case DIOAdEventOnShown:
                    DioSend(DioBridgeEventInlineShown, DioAdPayload(ad, placementId, nil));
                    break;
                case DIOAdEventOnFailedToShow:
                    DioSend(DioBridgeEventInlineShowFailed,
                            DioAdPayload(ad, placementId, @"Failed to show"));
                    break;
                case DIOAdEventOnClicked:
                    DioSend(DioBridgeEventInlineClicked, DioAdPayload(ad, placementId, nil));
                    break;
                case DIOAdEventOnClosed:
                    DioSend(DioBridgeEventInlineClosed, DioAdPayload(ad, placementId, nil));
                    break;
                case DIOAdEventOnAdCompleted:
                    DioSend(DioBridgeEventInlineCompleted, DioAdPayload(ad, placementId, nil));
                    break;
                default:
                    break;
            }
        }];

        DioSend(DioBridgeEventInlineLoaded, DioAdPayload(ad, placementId, nil));
    }
                                noAdHandler:^(NSError *error) {
        BOOL noFill = error.code == kDIOErrorNoFill
                   || error.code == kDIOErrorNoAds
                   || error.code == kDIOErrorNoAd;

        DioSend(noFill ? DioBridgeEventInlineNoFill : DioBridgeEventInlineLoadFailed,
                DioAdPayload(nil, placementId, error.localizedDescription ?: @"No ad"));
    }];
}

bool dioInlineIsLoaded(const char *placementIdRaw) {
    return gInlineAds()[DioString(placementIdRaw)] != nil;
}

void dioInlineShow(const char *placementIdRaw, int position, float offsetX, float offsetY,
                   int safeArea) {
    NSString *placementId = DioString(placementIdRaw);
    DIOAd *ad = gInlineAds()[placementId];

    if (ad == nil) {
        DioSend(DioBridgeEventInlineShowFailed,
                DioAdPayload(nil, placementId, @"No ad loaded for this placement"));
        return;
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        UIView *adView = [ad view];
        if (adView == nil) {
            DioSend(DioBridgeEventInlineShowFailed,
                    DioAdPayload(ad, placementId, @"Ad has no view"));
            return;
        }

        DioLayout(placementId, adView, position, offsetX, offsetY, safeArea);
    });
}

void dioInlineHide(const char *placementIdRaw) {
    NSString *placementId = DioString(placementIdRaw);
    DIOAd *ad = gInlineAds()[placementId];
    if (ad == nil) {
        return;
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        NSArray<NSLayoutConstraint *> *constraints = gConstraints()[placementId];
        if (constraints != nil) {
            [NSLayoutConstraint deactivateConstraints:constraints];
            [gConstraints() removeObjectForKey:placementId];
        }

        [[ad view] removeFromSuperview];
    });
}

void dioInlineDestroy(const char *placementIdRaw) {
    NSString *placementId = DioString(placementIdRaw);
    DIOAd *ad = gInlineAds()[placementId];
    if (ad == nil) {
        return;
    }

    [gInlineAds() removeObjectForKey:placementId];

    dispatch_async(dispatch_get_main_queue(), ^{
        NSArray<NSLayoutConstraint *> *constraints = gConstraints()[placementId];
        if (constraints != nil) {
            [NSLayoutConstraint deactivateConstraints:constraints];
            [gConstraints() removeObjectForKey:placementId];
        }

        [[ad view] removeFromSuperview];
        [ad finish];
    });
}

}
