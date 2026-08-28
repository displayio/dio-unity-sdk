using System.Collections.Generic;

namespace DisplayIO.Ads
{
    /// <summary>Immutable snapshot of an ad, taken when the event was raised.</summary>
    public class DioAdInfo
    {
        private static readonly string[] NoDomains = new string[0];

        public string PlacementId { get; }
        public string RequestId { get; }
        public DioAdUnitType AdUnitType { get; }

        /// <summary>Advertiser name, when the ad carries one.</summary>
        public string AdvertiserName { get; }

        /// <summary>Advertiser domains. Never null; empty when unknown.</summary>
        public IReadOnlyList<string> AdvertiserDomains { get; }

        public string CampaignId { get; }
        public string CreativeId { get; }
        public string AuctionId { get; }

        /// <summary>Effective CPM reported by the auction.</summary>
        public double Ecpm { get; }

        /// <summary>How long the ad stays valid, in seconds.</summary>
        public long TimeToLiveSeconds { get; }

        internal DioAdInfo(
            string placementId,
            string requestId,
            DioAdUnitType adUnitType,
            string advertiserName = null,
            IReadOnlyList<string> advertiserDomains = null,
            string campaignId = null,
            string creativeId = null,
            string auctionId = null,
            double ecpm = 0d,
            long timeToLiveSeconds = 0L)
        {
            PlacementId = placementId;
            RequestId = requestId;
            AdUnitType = adUnitType;
            AdvertiserName = advertiserName;
            AdvertiserDomains = advertiserDomains ?? NoDomains;
            CampaignId = campaignId;
            CreativeId = creativeId;
            AuctionId = auctionId;
            Ecpm = ecpm;
            TimeToLiveSeconds = timeToLiveSeconds;
        }

        public override string ToString() =>
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0} placement={1} request={2} advertiser={3} ecpm={4} ttl={5}s",
                AdUnitType, PlacementId, RequestId,
                AdvertiserName ?? "<null>", Ecpm, TimeToLiveSeconds);
    }
}
