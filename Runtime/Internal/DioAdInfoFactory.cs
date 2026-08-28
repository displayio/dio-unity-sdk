using System;
using System.Collections.Generic;
using UnityEngine;

namespace DisplayIO.Ads.Internal
{
    /// <summary>
    /// Builds a DioAdInfo from a native Ad. Must be called while still on the JNI callback
    /// thread: the native object is only guaranteed valid for the duration of that call.
    /// Every getter is read defensively — a single missing field must not lose the event.
    /// </summary>
    internal static class DioAdInfoFactory
    {
        internal static DioAdInfo From(AndroidJavaObject ad, string placementId, DioAdUnitType type)
        {
            if (ad == null)
            {
                return new DioAdInfo(placementId, null, type);
            }

            return new DioAdInfo(
                placementId,
                String(ad, "getRequestId"),
                type,
                String(ad, "getAdvertiserName"),
                Domains(ad),
                String(ad, "getCampaignId"),
                String(ad, "getCreativeId"),
                String(ad, "getAuctionId"),
                Double(ad, "getEcpm"),
                Long(ad, "getAdTimeToLive"));
        }

        internal static DioAdInfo Minimal(string placementId, DioAdUnitType type) =>
            new DioAdInfo(placementId, null, type);

        private static string String(AndroidJavaObject ad, string method)
        {
            try
            {
                return ad.Call<string>(method);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static double Double(AndroidJavaObject ad, string method)
        {
            try
            {
                return ad.Call<double>(method);
            }
            catch (Exception)
            {
                return 0d;
            }
        }

        private static long Long(AndroidJavaObject ad, string method)
        {
            try
            {
                return ad.Call<long>(method);
            }
            catch (Exception)
            {
                return 0L;
            }
        }

        private static IReadOnlyList<string> Domains(AndroidJavaObject ad)
        {
            try
            {
                using (AndroidJavaObject list = ad.Call<AndroidJavaObject>("getAdvertiserDomain"))
                {
                    if (list == null)
                    {
                        return null;
                    }

                    int count = list.Call<int>("size");
                    var domains = new List<string>(count);

                    for (int i = 0; i < count; i++)
                    {
                        domains.Add(list.Call<string>("get", i));
                    }

                    return domains;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
