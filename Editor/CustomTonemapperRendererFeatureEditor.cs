using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using CustomToneMapping.URP.RendererFeatures;

namespace CustomToneMapping.URP.Editor
{
    [CustomEditor(typeof(CustomToneMappingRendererFeature))]
    public class CustomTonemapperRendererFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var feature = target as CustomToneMappingRendererFeature;

            // Load shader
            ResourceReloader.TryReloadAllNullIn(feature, "Packages/net.aki-null.tonemapping");

            DrawDefaultInspector();

            CheckUrpConfiguration();
        }

        private void CheckUrpConfiguration()
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                ShowError("Universal Render Pipeline asset not found. This renderer feature only works with URP.");
                return;
            }

            CheckColorGradingMode(urpAsset);
        }

        private void CheckColorGradingMode(UniversalRenderPipelineAsset urpAsset)
        {
            if (urpAsset.colorGradingMode == ColorGradingMode.HighDynamicRange) return;

            ShowError(
                "Custom Tonemapper Renderer Feature requires HDR Color Grading Mode to function properly. " +
                "Current mode is 'Low Dynamic Range'. 'Grading Mode' can be found in the URP Asset settings under " +
                "'Quality > Post-processing'."
            );

            if (GUILayout.Button("Open URP Asset Settings"))
            {
                Selection.activeObject = urpAsset;
                EditorGUIUtility.PingObject(urpAsset);
            }
        }

        private static void ShowError(string message)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(message, MessageType.Error);
        }
    }
}