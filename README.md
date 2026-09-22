# Custom Tone Mapping for URP

This package extends URP by adding custom tone mapping algorithms, providing more options for different visual requirements and workflows.

<img width="1920" alt="GT7 Tone Mapping Sample" src="https://github.com/user-attachments/assets/541952bf-26a4-4047-9861-84e66020fd58" />

_GT7 Tone Mapping Sample_

Since adding new tone mapping functions to URP is not officially supported through Unity's API, integration requires either URP modifications or a renderer feature. Please see the [Integration Guide](#integration-guide) for details.

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Usage](#usage)
- [Modes](#modes)
  - [GT Tone Mapping](#gt-tone-mapping)
  - [GT7 Tone Mapping](#gt7-tone-mapping)
  - [AgX](#agx)
  - [ACES 2.0](#aces-20)
  - [Custom LUT](#custom-lut)
- [Integration Guide](#integration-guide)
  - [Method 1: Renderer Feature](#method-1-renderer-feature-no-urp-modification)
  - [Method 2: URP Package Modification](#method-2-urp-package-modification-recommended)
  - [Upgrading the URP customization](#upgrading-the-urp-customization)
- [Technical Details](#technical-details)
- [License](#license)

## Features

* Additional tone mapping algorithms: GT, GT7, AgX and ACES 2.0
* HDR display output compatibility
    * Runtime LUT generation supports variable peak brightness configurations
* Custom LUT support
    * Allows use of external LogC-encoded lookup tables
* Configurable LUT size (32³ to 65³) for quality/memory tradeoff
    * Found in the Additional Properties section of the Custom Tone Mapping component

## Prerequisites

* Unity 6 or later
* Universal Render Pipeline 17.0 or later

## Installation

Install via OpenUPM:

- Package is available on [OpenUPM](https://openupm.com). If you have [openupm-cli](https://github.com/openupm/openupm-cli#openupm-cli) installed, run this in your Unity project root:
    ```
    openupm add net.aki-null.tonemapping
    ```
- To update, specify the version you want:
    ```
    openupm add net.aki-null.tonemapping@1.2.6
    ```

Install via UPM (Git URL):

1. Open **Window > Package Manager**
2. Click the **+** button in the top-left corner
3. Select **Add package from git URL**
4. Enter: `https://github.com/aki-null/CustomToneMappingURP.git`
5. Follow the [Integration Guide](#integration-guide)

Alternatively, add it to your `Packages/manifest.json`:

```json
{
    "dependencies": {
        "net.aki-null.tonemapping": "https://github.com/aki-null/CustomToneMappingURP.git"
    }
}
```

## Usage

1. Add a **Tonemapping** volume component
   - Set to **None** when using the Renderer Feature method
   - Set to **Custom** when using the URP modification method
2. Add a **Custom Tone Mapping** volume component and select a mode
3. Add the mode-specific volume component to configure parameters
   - Each mode requires its own dedicated volume component (e.g., **GT Tone Mapping**, **GT7 Tone Mapping**, **AgX Tone Mapping**, **ACES 2.0 Tone Mapping**)

## Modes

### GT Tone Mapping

Designed by Polyphony Digital for Gran Turismo Sport. Initially presented at [CEDEC 2017](https://www.slideshare.net/nikuque/hdr-theory-and-practicce-jp) and later at [SIGGRAPH Asia 2018](http://cdn2.gran-turismo.com/data/www/pdi_publications/PracticalHDRandWCGinGTS_20181222.pdf).

<img width="510" alt="GT Tone Mapping Inspector" src="https://github.com/user-attachments/assets/41589490-174f-4407-9f16-7510de103c00" />

### GT7 Tone Mapping

Designed by Polyphony Digital for Gran Turismo 7. Presented at [SIGGRAPH 2025](https://blog.selfshadow.com/publications/s2025-shading-course/).

<img width="510" alt="GT7 Tone Mapping Inspector" src="https://github.com/user-attachments/assets/0fbc18b5-260d-4286-a280-54a08db6b0fc" />

### AgX

Designed by [Troy Sobotka](https://github.com/sobotka). The LUT generation is based on [Eary Chow's version](https://github.com/EaryChow/AgX_LUT_Gen).

<img width="510" alt="AgX Tone Mapping Inspector" src="https://github.com/user-attachments/assets/b80e555b-f1e7-4536-9dac-6138ae04e201" />

### ACES 2.0

The Academy [ACES 2.0](https://github.com/aces-aswf/aces-core) Output Transform.

- **ACES-aware grading** (on by default) grades in ACEScc/ACEScg, like URP's own ACES. It needs Method 2 with HDR Color Grading. Otherwise ACES 2.0 grades in URP's standard spaces, which looks different, and the inspector shows a notice.

### Custom LUT

When Custom LUT is selected, assign a 2D Alexa LogC EI 1000 LUT strip in the Custom Tone Mapping component. For an `N`³ LUT, use an `N² × N` strip; the inspector can repair the import settings.

Custom LUT mode uses a static LUT and does not support variable peak brightness like the baked modes.

## Integration Guide

### Method 1: Renderer Feature (No URP Modification)

The Custom Tone Mapping Renderer Feature enables custom tone mapping without modifying URP packages. This approach is suitable for:
- Projects that cannot modify URP source code
- Testing custom tone mapping before committing to URP modifications
- Maintaining easier Unity version upgrades

#### Limitations
- Requires HDR and HDR Color Grading in the URP Asset. Cameras that grade in LDR are not tone mapped. HDR display output always grades in HDR.
- ACES 2.0 always grades in URP's standard spaces.
- Less efficient than native integration due to additional rendering pass overhead

#### Setup
1. Add **Custom Tone Mapping Renderer Feature** to your Universal Renderer Data
2. Follow [Usage](#usage), with URP's Tonemapping mode set to **None**

#### How it works
The renderer feature injects a tone mapping pass into the render pipeline by intercepting the color grading LUT after URP's LUT generation pass and applying custom tone mapping. This approach provides better integration with the pipeline compared to directly tone mapping the framebuffer.

### Method 2: URP Package Modification (Recommended)

Modifying the URP package gives native integration with the best performance.

- Supports both HDR and LDR Color Grading.
- In Compatibility Mode, only HDR Color Grading is supported.
- Customizations made before 1.3: see [Upgrading the URP customization](#upgrading-the-urp-customization).

#### 1. Add Assembly Reference

**File**: `Packages/com.unity.render-pipelines.universal/Runtime/Unity.RenderPipelines.Universal.Runtime.asmdef`

Add the custom tonemapper assembly reference to the `references` array:

```diff
     "references": [
         "GUID:df380645f10b7bc4b97d4f5eb6303d95",
         "GUID:69257879134bba646869b21467b3338d",
         "GUID:ab67fb10353d84448ac887a7367cbda8",
         "GUID:7dbf32976982c98448af054f2512cb79",
         "GUID:d8b63aba1907145bea998dd612889d6b",
         "GUID:2665a8d13d1b3f18800f46e256720795",
         "GUID:4fd6538c1c56b409fb53fdf0183170ec",
         "GUID:86bc95e6fdb13ff43aa04316542905ae",
         "GUID:d04eb9c554ad44ceab303cecf0c0cf82",
         "GUID:214c0945bb158c940aada223f3223ee8",
+        "net.aki-null.ToneMapping.URP",
         "GUID:c49c619b6af2be941af9bcbca2641964"
     ],
```

#### 2. Add Custom Tonemapping Mode

**File**: `Packages/com.unity.render-pipelines.universal/Runtime/Overrides/Tonemapping.cs`

**Around line 27**
```diff
 namespace UnityEngine.Rendering.Universal
 {
     [Serializable]
     public enum TonemappingMode
     {
         /// <summary>
         /// Use this option if you do not want to apply tonemapping
         /// </summary>
         None,

         /// <summary>
         /// Use this option if you only want range-remapping with minimal impact on color hue and saturation.
         /// It is generally a great starting point for extensive color grading.
         /// </summary>
         Neutral, // Neutral tonemapper

         /// <summary>
         /// Use this option to apply a close approximation of the reference ACES tonemapper for a more filmic look.
         /// It is more contrasted than Neutral and has an effect on actual color hue and saturation.
         /// Note that if you use this tonemapper all the grading operations will be done in the ACES color spaces for optimal precision and results.
         /// </summary>
         ACES, // ACES Filmic reference tonemapper (custom approximation)
+        Custom
     }
```

If your URP version has no comma after `ACES`, add one.

#### 3. Update UberPost Shader

**File**: `Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/UberPost.shader`

**Around line 7:**
```diff
 HLSLINCLUDE
     #pragma multi_compile_local_fragment _ _DISTORTION
     #pragma multi_compile_local_fragment _ _CHROMATIC_ABERRATION
     #pragma multi_compile_local_fragment _ _BLOOM_LQ _BLOOM_HQ _BLOOM_LQ_DIRT _BLOOM_HQ_DIRT
-    #pragma multi_compile_local_fragment _ _HDR_GRADING _TONEMAP_ACES _TONEMAP_NEUTRAL
+    #pragma multi_compile_local_fragment _ _HDR_GRADING _TONEMAP_ACES _TONEMAP_NEUTRAL _TONEMAP_CUSTOM
     #pragma multi_compile_local_fragment _ _FILM_GRAIN
     #pragma multi_compile_local_fragment _ _DITHERING
     #pragma multi_compile_local_fragment _ _GAMMA_20 _LINEAR_TO_SRGB_CONVERSION
```

**Around line 33 (include order matters):**
```diff
     #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
+    #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/TonemapParams.hlsl"
     #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
     #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/DebuggingFullscreen.hlsl"
     #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScalingClamping.hlsl"
     #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
```

#### 4. Update LutBuilderHdr Shader

**File**: `Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/LutBuilderHdr.shader`

**Around line 4 (include order matters):**
```diff
     HLSLINCLUDE
-        #pragma multi_compile_local _ _TONEMAP_ACES _TONEMAP_NEUTRAL
+        #pragma multi_compile_local _ _TONEMAP_ACES _TONEMAP_NEUTRAL _TONEMAP_CUSTOM
+        #pragma multi_compile_local_fragment _ _CUSTOM_TONEMAP_ACES2
         #pragma multi_compile_local_fragment _ HDR_COLORSPACE_CONVERSION

         #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
+        #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/TonemapParams.hlsl"
         #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
         #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ACES.hlsl"
         #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
 #if defined(HDR_COLORSPACE_CONVERSION)
         #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/HDROutput.hlsl"
 #endif
```

**Around line 189:**
```diff
         float3 Tonemap(float3 colorLinear)
         {
             #if _TONEMAP_NEUTRAL
             {
                 colorLinear = NeutralTonemap(colorLinear);
             }
             #elif _TONEMAP_ACES
             {
                 // Note: input is actually ACEScg (AP1 w/ linear encoding)
                 float3 aces = ACEScg_to_ACES(colorLinear);
                 colorLinear = AcesTonemap(aces);
             }
+            #elif _TONEMAP_CUSTOM
+            #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/Tonemap.hlsl"
             #endif

             return colorLinear;
         }

         float3 ProcessColorForHDR(float3 colorLinear)
         {
             #ifdef HDR_COLORSPACE_CONVERSION
                 #ifdef _TONEMAP_ACES
                 float3 aces = ACEScg_to_ACES(colorLinear);
                 return HDRMappingACES(aces.rgb, PaperWhite, MinNits, MaxNits, RangeReductionMode, true);
                 #elif _TONEMAP_NEUTRAL
                 return HDRMappingFromRec2020(colorLinear.rgb, PaperWhite, MinNits, MaxNits, RangeReductionMode, HueShift, true);
+                #elif _TONEMAP_CUSTOM
+                #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/TonemapHdr.hlsl"
                 #else
                 // Grading finished in Rec2020, converting to the expected color space and [0, 10k] nits range
                 return RotateRec2020ToOutputSpace(colorLinear) * PaperWhite;
                 #endif
             #endif

             return colorLinear;
         }
```

#### 5. Update Common Shader Include

**File**: `Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl`

**Around line 108 in ApplyTonemap function**:

```diff
 half3 ApplyTonemap(half3 input)
 {
 #if _TONEMAP_ACES
     float3 aces = unity_to_ACES(input);
     input = AcesTonemap(aces);
 #elif _TONEMAP_NEUTRAL
     input = NeutralTonemap(input);
+#elif _TONEMAP_CUSTOM
+#include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/TonemapLdr.hlsl"
 #endif

     return saturate(input);
}
```

#### 6. Add C# Integration Points

**File**: `Packages/com.unity.render-pipelines.universal/Runtime/Passes/PostProcessPassRenderGraph.cs`

**Around line 2580 in RenderUberPost method (the LDR grading path):**
```diff
                         switch (data.toneMappingMode)
                         {
                             case TonemappingMode.Neutral: CoreUtils.SetKeyword(material, ShaderKeywordStrings.TonemapNeutral, true); break;
                             case TonemappingMode.ACES: CoreUtils.SetKeyword(material, ShaderKeywordStrings.TonemapACES, true); break;
+                            case TonemappingMode.Custom: CustomToneMapping.URP.UrpBridge.PrepareMaterial(material, data.cameraData.isHDROutputActive ? data.cameraData.hdrDisplayInformation : null); break;
                             default: break; // None
                         }
```

**File**: `Packages/com.unity.render-pipelines.universal/Runtime/Passes/ColorGradingLutPass.cs`

**Around line 259 in ExecutePass method (the HDR grading path):**
```diff
                     switch (tonemapping.mode.value)
                     {
                         case TonemappingMode.Neutral: material.EnableKeyword(ShaderKeywordStrings.TonemapNeutral); break;
                         case TonemappingMode.ACES: material.EnableKeyword(allowColorGradingACESHDR ? ShaderKeywordStrings.TonemapACES : ShaderKeywordStrings.TonemapNeutral); break;
+                        case TonemappingMode.Custom: CustomToneMapping.URP.UrpBridge.PrepareMaterial(material, passData.cameraData.isHDROutputActive ? passData.cameraData.hdrDisplayInformation : null); break;
                         default: break; // None
                     }
```

### Upgrading the URP customization

Customizations made before 1.3 keep working, and ACES 2.0 grades in standard spaces with them. For ACES-aware grading, add the `_CUSTOM_TONEMAP_ACES2` pragma from [step 4](#4-update-lutbuilderhdr-shader) to `LutBuilderHdr.shader`. No other change is needed.

## Technical Details

GT, GT7, AgX and the ACES 2.0 LDR path are baked into a 3D LUT on the CPU when settings change. The GPU samples it during URP color grading.

The LUT uses a 2D `N² × N` texture layout and ARRI LogC3 EI 1000 input encoding. The LUT size setting controls the quality, baking cost, and memory tradeoff.

Tone mapping stays inside URP's color grading stage, so bloom, grading, film grain, dithering and output conversion run in URP's order.

## License

This project is licensed under the MIT License – see [LICENSE.md](./LICENSE.md).

This project includes third-party code under its own licenses.
See [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md).
