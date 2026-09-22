using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CustomToneMapping.URP.RendererFeatures;

namespace CustomToneMapping.URP.Editor
{
    /// <summary>
    /// Validates the custom tonemapping setup and provides guidance for configuration issues.
    /// </summary>
    internal static class SetupValidator
    {
        private struct SetupStatus
        {
            public bool HasUrpModification;
            public bool HasRendererFeature;
            public TonemappingMode TonemappingMode;
            public ToneMappingMode CustomToneMappingMode;
        }

        // The inspector describes the profile being edited. VolumeManager's resolved stack reflects whichever
        // profile is active, so reading it would hide every message while an inactive profile is edited, and could
        // show warnings for another profile's mode.
        private static VolumeProfile OwningProfile(VolumeComponent inspected)
        {
            if (inspected == null)
                return null;
            var path = AssetDatabase.GetAssetPath(inspected);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        }

        // Use the edited profile. When it lacks the component, another volume supplies the value, so use the stack. A
        // profile created at runtime has no asset path and also uses the stack.
        private static T Resolve<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile != null && profile.TryGet<T>(out var fromProfile))
                return fromProfile;
            try
            {
                return VolumeManager.instance.stack?.GetComponent<T>();
            }
            catch (System.Exception)
            {
                // The volume stack may not be initialized in edit mode.
                return null;
            }
        }

        private static SetupStatus GetSetupStatus(VolumeProfile profile)
        {
            var status = new SetupStatus
            {
                // Check if URP has been modified by looking for our custom tonemapping integration
                HasUrpModification = HasUrpCustomToneMappingIntegration()
            };

            var tonemapping = Resolve<Tonemapping>(profile);
            if (tonemapping != null && tonemapping.mode != null)
                status.TonemappingMode = tonemapping.mode.value;

            var customTonemapping = Resolve<CustomToneMapping>(profile);
            if (customTonemapping != null && customTonemapping.mode != null)
                status.CustomToneMappingMode = customTonemapping.mode.value;

            // Check if renderer feature is set up
            status.HasRendererFeature = HasCustomTonemapperRendererFeature();

            return status;
        }

        internal static void DrawSetupValidation(VolumeComponent inspected)
        {
            var profile = OwningProfile(inspected);
            var setupStatus = GetSetupStatus(profile);

            if (setupStatus.CustomToneMappingMode == ToneMappingMode.None)
                return;

            if (setupStatus.CustomToneMappingMode == ToneMappingMode.ACES2)
                DrawAces2GradingValidation(profile, setupStatus.HasUrpModification);

            if (setupStatus.HasUrpModification)
            {
                // URP has been modified - check for conflicts and proper setup
                if (setupStatus.HasRendererFeature)
                {
                    EditorGUILayout.HelpBox(
                        "URP has been customized but Custom Tone Mapping Renderer Feature is still active.\n\n" +
                        "Please remove it from your Universal Renderer Data since URP now handles custom tone mapping natively.",
                        MessageType.Warning);
                    EditorGUILayout.Space();
                }
                else if (!IsToneMappingModeCustom(setupStatus.TonemappingMode) &&
                         setupStatus.CustomToneMappingMode != ToneMappingMode.None)
                {
                    EditorGUILayout.HelpBox(
                        "Tone mapping mode is not set to 'Custom'. Custom tone mapping will not function.\n\n" +
                        "Please set it to 'Custom' in your Volume Profile that contains the Tonemapping override.",
                        MessageType.Warning);
                    EditorGUILayout.Space();
                }
            }
            else
            {
                // URP has not been modified - check renderer feature setup
                if (!setupStatus.HasRendererFeature)
                {
                    EditorGUILayout.HelpBox(
                        "URP has not been modified and Custom Tone Mapping Renderer Feature is not set up. Custom tone mapping will not function.\n\n" +
                        "Please add the renderer feature to your Universal Renderer Data.",
                        MessageType.Error);
                    EditorGUILayout.Space();
                }
                else if (setupStatus.TonemappingMode != TonemappingMode.None &&
                         setupStatus.CustomToneMappingMode != ToneMappingMode.None)
                {
                    EditorGUILayout.HelpBox(
                        "Renderer feature is set up but URP tonemapping mode should be 'None' when using renderer feature fallback",
                        MessageType.Warning);
                    EditorGUILayout.Space();
                }
                else if (GradesInLdr())
                {
                    EditorGUILayout.HelpBox(
                        "Custom tone mapping is off: the URP Asset grades in LDR, and the Renderer Feature works only with HDR grading. " +
                        "Cameras that output to an HDR display are still tone mapped, because URP grades them in HDR.\n\n" +
                        "Enable HDR and HDR Color Grading in the URP Asset, or customize URP to support LDR grading (README, Method 2).",
                        MessageType.Error);
                    EditorGUILayout.Space();
                }
            }
        }

        private static void DrawAces2GradingValidation(VolumeProfile profile, bool hasUrpModification)
        {
            var aces = Resolve<Aces2ToneMapping>(profile);
            if (aces == null || !aces.acesAwareGrading.value)
                return;

            if (!hasUrpModification)
            {
                EditorGUILayout.HelpBox(
                    "ACES 2.0 is grading in URP's standard spaces: ACES-aware grading needs the URP customization (README, Method 2). Customize URP, or turn off 'ACES-aware grading' on the ACES 2.0 Tone Mapping component.",
                    MessageType.Warning);
                DrawLink("Open README: Method 2", "https://github.com/aki-null/CustomToneMappingURP#method-2-urp-package-modification-recommended");
            }
            else if (GradesInLdr())
            {
                EditorGUILayout.HelpBox(UrpBridge.Aces2LdrGradingNotice + LdrExceptionNote, MessageType.Warning);
                EditorGUILayout.Space();
            }
            else if (!UrpBridge.DeclaresAces2Grading(Shader.Find("Hidden/Universal Render Pipeline/LutBuilderHdr")))
            {
                EditorGUILayout.HelpBox(UrpBridge.Aces2StandardGradingNotice, MessageType.Warning);
                DrawLink("Open the upgrade steps", UrpBridge.UrpCustomizationUpgradeUrl);
            }
        }

        // What the asset selects. URP still grades a camera in HDR when it outputs to an HDR display
        // (UniversalRenderPipeline.CreatePostProcessingData), which an edit-time check cannot know.
        private const string LdrExceptionNote = " Cameras that output to an HDR display are not affected, because URP grades them in HDR.";

        private static bool GradesInLdr() =>
            GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset &&
            (asset.colorGradingMode != ColorGradingMode.HighDynamicRange || !asset.supportsHDR);

        private static void DrawLink(string label, string url)
        {
            if (EditorGUILayout.LinkButton(label))
                Application.OpenURL(url);
            EditorGUILayout.Space();
        }

        private static bool HasUrpCustomToneMappingIntegration()
        {
            // Check if URP's TonemappingMode enum contains a "Custom" value
            // This indicates that URP has been modified to support custom tonemapping
            try
            {
                var tonemappingModeType = typeof(TonemappingMode);

                var enumValues = System.Enum.GetNames(tonemappingModeType);
                var hasCustom = enumValues.Contains("Custom");

                return hasCustom;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static bool IsToneMappingModeCustom(TonemappingMode mode)
        {
            try
            {
                var tonemappingModeType = typeof(TonemappingMode);

                // Try to get the "Custom" enum value using reflection
                if (System.Enum.TryParse(tonemappingModeType, "Custom", out var customValue))
                {
                    return (int)mode == (int)customValue;
                }

                return false;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static bool HasCustomTonemapperRendererFeature()
        {
            // First check the pipeline asset from current quality settings
            var qualityLevel = QualitySettings.GetQualityLevel();
            var pipeline = QualitySettings.GetRenderPipelineAssetAt(qualityLevel) as UniversalRenderPipelineAsset;

            // Fallback to default pipeline if quality-specific one is not set
            if (pipeline == null)
                pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

            if (pipeline == null) return false;

            // Use reflection to access internal fields (Unity's public API doesn't expose renderer data directly)
            try
            {
                var renderersField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (renderersField == null)
                    return false;

                var renderers = renderersField.GetValue(pipeline) as ScriptableRendererData[];

                if (renderers == null || renderers.Length == 0)
                    return false;

                foreach (var rendererData in renderers)
                {
                    var universalRenderer = rendererData as UniversalRendererData;
                    if (universalRenderer == null) continue;

                    // Check if our renderer feature is present in this renderer
                    if (universalRenderer.rendererFeatures != null &&
                        universalRenderer.rendererFeatures.Any(feature =>
                            feature is CustomToneMappingRendererFeature))
                    {
                        return true; // Found the feature in at least one renderer
                    }
                }

                return false; // Feature not found in any renderer
            }
            catch
            {
                // If reflection fails, assume feature is not present
                return false;
            }
        }
    }
}
