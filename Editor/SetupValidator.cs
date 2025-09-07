using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

        private static SetupStatus GetSetupStatus()
        {
            var status = new SetupStatus
            {
                // Check if URP has been modified by looking for our custom tonemapping integration
                HasUrpModification = HasUrpCustomToneMappingIntegration()
            };

            try
            {
                var stack = VolumeManager.instance.stack;
                if (stack != null)
                {
                    var tonemapping = stack.GetComponent<Tonemapping>();
                    if (tonemapping != null && tonemapping.mode != null)
                    {
                        status.TonemappingMode = tonemapping.mode.value;
                    }

                    var customTonemapping = stack.GetComponent<CustomToneMapping>();
                    if (customTonemapping != null && customTonemapping.mode != null)
                    {
                        status.CustomToneMappingMode = customTonemapping.mode.value;
                    }
                }
            }
            catch (System.Exception)
            {
                // Ignore exceptions during volume stack access in edit mode
                // Volume stack might not be properly initialized
            }

            // Check if renderer feature is set up
            status.HasRendererFeature = HasCustomTonemapperRendererFeature();

            return status;
        }

        internal static void DrawSetupValidation()
        {
            var setupStatus = GetSetupStatus();

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
            }
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
                            feature != null && feature.GetType().Name.Contains("CustomToneMapping")))
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