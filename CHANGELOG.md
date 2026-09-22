# Changelog

## Unreleased

### Added
- ACES 2.0 Output Transform (Academy aces-core 069b0bc), SDR and HDR, through both integration methods
- ACES-aware grading for ACES 2.0 through the URP customization, via one optional `_CUSTOM_TONEMAP_ACES2` pragma. Existing customizations work without it. Inspector and console notices explain when it is inactive.

### Changed
- The Renderer Feature no longer tone maps cameras that grade in LDR, in every mode. Cameras outputting to an HDR display are unaffected.
- `UrpBridge.CachedLutTexture` is obsolete. Use `UrpBridge.GetCachedLut(mode)`.
- Baked LUTs are no longer evicted by switching modes, or expired by frames where only cameras without post-processing (such as UI cameras) render
- Peak luminance (GT and GT7 **Target Peak Nits**, AgX **Max Nits**) no longer blends between volumes.

### Fixed
- GT, GT7 and AgX fall back to their manual peak luminance when peak detection is on but the display reports no peak. Tone mapping used to turn off.

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
