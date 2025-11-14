using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP.Editor
{
    [CustomEditor(typeof(CustomToneMapping))]
    [SupportedOnRenderPipeline(typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset))]
    public sealed class CustomToneMappingVolumeEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _mode;
        private SerializedDataParameter _lutSize;
        private SerializedDataParameter _lutTexture;

        // Override to enable Additional Properties feature
        public override bool hasAdditionalProperties => true;

        public override void OnEnable()
        {
            try
            {
                base.OnEnable();

                var o = new PropertyFetcher<CustomToneMapping>(serializedObject);
                _mode = Unpack(o.Find(x => x.mode));
                _lutSize = Unpack(o.Find(x => x.lutSize));
                _lutTexture = Unpack(o.Find(x => x.lutTexture));
            }
            catch (System.Exception)
            {
                // Silently ignore - this can happen during domain reload when the serialized object isn't ready
            }
        }

        public override void OnInspectorGUI()
        {
            // Skip drawing if initialization failed
            if (_mode == null)
                return;

            SetupValidator.DrawSetupValidation();

            DrawModeSelection();

            // Draw additional properties section (Debug Export)
            if (showAdditionalProperties)
            {
                DrawDebugExportSection();
            }
        }

        private void DrawModeSelection()
        {
            PropertyField(_mode);

            var currentMode = (ToneMappingMode)_mode.value.intValue;

            // Only show LUT size for baked tone mapping modes
            // CustomLUT uses the actual texture dimensions
            if (currentMode != ToneMappingMode.None && currentMode != ToneMappingMode.CustomLUT)
            {
                PropertyField(_lutSize);
            }

            if (currentMode == ToneMappingMode.CustomLUT)
            {
                EditorGUILayout.Space();
                PropertyField(_lutTexture, new GUIContent("Custom LUT Texture"));

                var lutTexture = _lutTexture.value.objectReferenceValue;
                var isOverrideEnabled = _lutTexture.overrideState.boolValue;

                if (lutTexture != null && !isOverrideEnabled)
                {
                    _lutTexture.overrideState.boolValue = true;
                    _lutTexture.value.serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                var lutTexture = _lutTexture.value.objectReferenceValue;
                var isOverrideEnabled = _lutTexture.overrideState.boolValue;

                if (lutTexture != null && isOverrideEnabled)
                {
                    _lutTexture.overrideState.boolValue = false;
                    _lutTexture.value.serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private void DrawDebugExportSection()
        {
            var mode = (ToneMappingMode)_mode.value.intValue;
            var lutTexture = _lutTexture.value.objectReferenceValue as Texture2D;
            LutExporter.DrawDebugExportSection(mode, lutTexture);
        }
    }
}