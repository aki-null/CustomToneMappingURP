# Changelog

## 1.3.0 - 2026-10-10

### Added
- ACES 2.0 tone mapping for SDR and HDR through both integration methods, with a dedicated volume inspector
- ACES-aware grading through the updated URP customization ([upgrade steps](README.md#upgrading-the-urp-customization)). The Renderer Feature and older customizations use standard grading.
- URP's Paper White controls in the Custom Tone Mapping inspector

### Changed
- GT Tone Mapping moved to Post-processing, alongside the other modes
- GT and GT7 curve previews show output units
- Failed LUT bakes retry with increasing delays
- The Renderer Feature requires HDR color grading. HDR display output remains supported regardless of the asset's grading mode.
- Cached LUTs survive mode switches and frames without post-processing
- GT/GT7 Target Peak Nits and AgX Max Nits no longer blend between volumes
- GT/GT7 SDR Paper White also controls HDR midtones; default midtones are unchanged

### Breaking
- Removed AgX SDR Paper White. Use URP's Paper White instead.

### Fixed
- GT, GT7 and AgX use the manual peak when detection reports none
- GT, GT7 and AgX HDR output no longer exceeds the target peak. Remove previous workarounds such as lowering the target peak or setting Paper White to 100.
- The Renderer Feature now uses URP's paper white, correcting HDR exposure
- AgX supports HDR displays dimmer than paper white by scaling output to the peak
- Zero paper white disables custom tone mapping with a warning instead of producing invalid output

## 1.2.6 - 2026-09-23

### Fixed
- Unity 6000.6 compile error from obsolete `Object.GetInstanceID()` in custom LUT caching ([#6](https://github.com/aki-null/CustomToneMappingURP/issues/6))

## 1.2.5 - 2026-09-06

### Fixed
- Corrected ARRI LogC3 EI 1000 encoding in custom LUT sampling and CPU baking, fixing shadow mismatches across SDR, HDR, and LDR paths

### Changed
- Optimized Render Graph LUT handoff by removing the copy-back pass
- Added validated LUT caching to avoid unnecessary rebakes, preserve valid LUTs after configuration errors, and expire stale entries
- Improved configuration validation and HDR LUT format safety

## 1.2.4 - 2026-05-09

### Fixed
- Compatibility with VR Single Pass Instanced rendering by [@sambazzano](https://github.com/sambazzano) in [#5](https://github.com/aki-null/CustomToneMappingURP/pull/5)
- Per-frame GC allocations by [@sambazzano](https://github.com/sambazzano) in [#5](https://github.com/aki-null/CustomToneMappingURP/pull/5)

## 1.2.3 - 2026-03-24

### Fixed
- URP modification integration with LDR color grading clearing all UberPost shader keywords, disabling bloom, film grain, dithering, and other post-processing effects

## 1.2.2 - 2026-03-21

### Added
- LRU cache to prevent redundant LUT re-baking when multiple cameras use different tone mapping configurations

### Changed
- Optimized fallback texture format conversion

### Fixed
- `displayName` deprecation warning

## 1.2.1 - 2026-03-19

### Fixed
- Compilation error on URP 17.5+ where legacy rendering callbacks used by the Renderer Feature were removed; the Renderer Feature requires Render Graph on these versions

## 1.2.0 - 2026-02-02

### Added
- All remaining AgX presets used in Blender

## 1.1.0 - 2025-11-15

### Added
- LUT size configuration added to advanced properties in the Custom Tone Mapping volume component

### Fixed
- LUT building for fallback texture formats

## 1.0.0 - 2025-09-09

Initial release
