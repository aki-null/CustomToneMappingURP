# Changelog

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
- Compilation error on URP 17.5+ where legacy rendering pipeline methods were removed from ScriptableRenderPass

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
