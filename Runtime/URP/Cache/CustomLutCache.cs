using UnityEngine;

namespace CustomToneMapping.URP
{
    // Small identity/validation cache for caller-owned custom LUT textures.
    // The cache never destroys these textures. It stores only validation results
    // and sampling parameters, using a four-entry true LRU with monotonic stamps.
    internal static class CustomLutCache
    {
        private const int MaxCacheEntries = 4;

        private struct CacheEntry
        {
            public CustomLutConfig Config;
            public bool HasConfig;
            public string Error;
            public bool HasFailure;
            public bool FailureWasReported;
            public ulong LastUsedStamp;
        }

        private static readonly CacheEntry[] Cache = new CacheEntry[MaxCacheEntries];
        private static ulong _accessStamp;
        private static int _cacheCount;
        private static int _lastUsedSlot = -1;

        internal static bool TryGetOrValidate(Texture2D texture, out Vector3 sample,
            out string error, out bool shouldReportFailure)
        {
            // The last-used slot is the common path. A frame number is not enough
            // here: several accesses can happen in one frame, so every access gets
            // a unique stamp for deterministic LRU selection.
            if (_cacheCount > 0 && _lastUsedSlot >= 0 && _lastUsedSlot < _cacheCount)
            {
                ref var last = ref Cache[_lastUsedSlot];
                if (TryUse(ref last, _lastUsedSlot, texture, out sample, out error,
                        out shouldReportFailure))
                    return !last.HasFailure;
            }

            for (var i = 0; i < _cacheCount; i++)
            {
                if (i == _lastUsedSlot)
                    continue;

                ref var entry = ref Cache[i];
                if (TryUse(ref entry, i, texture, out sample, out error,
                        out shouldReportFailure))
                    return !entry.HasFailure;
            }

            // This texture is not cached. Validate it before committing a new
            // snapshot; failures are cached too so repeated bad inputs do not
            // repeat validation or reporting.
            var slot = SelectSlot(out var isNewSlot);
            var valid = CustomLutConfig.TryCreate(texture, out var config, out error);
            Cache[slot] = new CacheEntry
            {
                Config = config,
                HasConfig = true,
                Error = valid ? null : error,
                HasFailure = !valid,
                FailureWasReported = !valid,
                LastUsedStamp = NextAccessStamp(),
            };

            if (isNewSlot)
                _cacheCount++;

            _lastUsedSlot = slot;
            sample = valid ? config.SampleParams : default;
            shouldReportFailure = !valid;
            return valid;
        }

        internal static void ClearCache()
        {
            for (var i = 0; i < Cache.Length; i++)
                Cache[i] = default;

            _cacheCount = 0;
            _lastUsedSlot = -1;
            _accessStamp = 0;
        }

        private static bool TryUse(ref CacheEntry entry, int slot, Texture2D texture,
            out Vector3 sample, out string error, out bool shouldReportFailure)
        {
            sample = default;
            error = null;
            shouldReportFailure = false;
            if (!entry.HasConfig || !entry.Config.Matches(texture))
                return false;

            entry.LastUsedStamp = NextAccessStamp();
            _lastUsedSlot = slot;
            sample = entry.Config.SampleParams;
            error = entry.Error;
            shouldReportFailure = entry.HasFailure && !entry.FailureWasReported;
            if (shouldReportFailure)
                entry.FailureWasReported = true;
            return true;
        }

        private static int SelectSlot(out bool isNewSlot)
        {
            if (_cacheCount < MaxCacheEntries)
            {
                isNewSlot = true;
                return _cacheCount;
            }

            isNewSlot = false;
            var slot = 0;
            var oldest = Cache[0].LastUsedStamp;
            for (var i = 1; i < MaxCacheEntries; i++)
            {
                if (Cache[i].LastUsedStamp < oldest)
                {
                    oldest = Cache[i].LastUsedStamp;
                    slot = i;
                }
            }

            return slot;
        }

        private static ulong NextAccessStamp() => ++_accessStamp;
    }
}
