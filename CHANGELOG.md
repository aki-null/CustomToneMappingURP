# Changelog

## Unreleased

### Fixed
- Corrected custom LUT sampling to use standard piecewise ARRI LogC3 EI 1000 encoding, fixing the shadow mismatch with CPU-baked and standard custom LUTs in SDR, HDR, and LDR shader integration paths
- Corrected the CPU LUT baker's LogC3 EI 1000 black offset constant to `0.092814`

### Changed
- Optimized the Renderer Feature's Render Graph integration by handing the tone-mapped LUT directly to downstream post-processing, eliminating the copy-back pass and allowing URP's original grading LUT to remain memoryless when framebuffer fetch pass merging is available
- Improved built-in LUT caching to avoid unnecessary rebakes and preserve valid LUTs when configurations fail validation
- Improved invalid-configuration handling and HDR LUT format safety

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
