using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

        public override void OnDisable()
        {
            ReleaseTonemapping();
            base.OnDisable();
        }

        private void ReleaseTonemapping()
        {
            _tonemapping?.Dispose();
            _tonemapping = null;
        }

        public override void OnInspectorGUI()
        {
            // Skip drawing if initialization failed
            if (_mode == null)
                return;

            SetupValidator.DrawSetupValidation(target as VolumeComponent);

            DrawModeSelection();
            DrawPaperWhite();

            // Draw additional properties section (LUT Size, Debug Export)
            if (showAdditionalProperties)
            {
                DrawAdditionalPropertiesSection();
            }
        }

        private void DrawModeSelection()
        {
            PropertyField(_mode);

            var currentMode = (ToneMappingMode)_mode.value.intValue;

            if (currentMode == ToneMappingMode.CustomLUT)
            {
                EditorGUILayout.Space();
                PropertyField(_lutTexture, new GUIContent("Custom LUT Texture"));

                var lutTexture = _lutTexture.value.objectReferenceValue;
                var isOverrideEnabled = _lutTexture.overrideState.boolValue;

                DrawCustomLutValidation(lutTexture as Texture2D);

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

        // Paper white lives on URP's Tonemapping component, whose inspector hides it in the None and Custom modes. So it
        // is drawn here, for this profile's override only, like any volume inspector.
        private void DrawPaperWhite()
        {
            var mode = (ToneMappingMode)_mode.value.intValue;
            if (!PlayerSettings.allowHDRDisplaySupport || mode == ToneMappingMode.None)
                return;

            var profile = SetupValidator.OwningProfile(target as VolumeComponent);
            if (profile == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("HDR Output", EditorStyles.boldLabel);

            if (!profile.TryGet<Tonemapping>(out var tonemapping))
            {
                ReleaseTonemapping();
                DrawPaperWhiteInEffect();
                if (GUILayout.Button("Add Tonemapping Override"))
                {
                    AddTonemappingOverride(profile);
                    GUIUtility.ExitGUI();
                }
                return;
            }

            if (!tonemapping.active)
            {
                ReleaseTonemapping();
                EditorGUILayout.HelpBox("This profile's Tonemapping override is disabled, so Paper White has no effect.",
                    MessageType.Info);
                return;
            }

            if (_tonemapping == null || _tonemapping.targetObject != tonemapping)
            {
                ReleaseTonemapping();
                _tonemapping = new SerializedObject(tonemapping);
                var o = new PropertyFetcher<Tonemapping>(_tonemapping);
                _detectPaperWhite = Unpack(o.Find(x => x.detectPaperWhite));
                _paperWhite = Unpack(o.Find(x => x.paperWhite));
            }

            _tonemapping.Update();
            PropertyField(_detectPaperWhite, PaperWhiteDetectLabel);
            EditorGUI.indentLevel++;
            using (new EditorGUI.DisabledScope(_detectPaperWhite.value.boolValue))
                PropertyField(_paperWhite, PaperWhiteLabel);
            EditorGUI.indentLevel--;
            _tonemapping.ApplyModifiedProperties();
        }

        // Read-only: the paper white in effect.
        private static void DrawPaperWhiteInEffect()
        {
            var applied = SetupValidator.StackTonemapping();
            if (applied == null)
                return;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField(PaperWhiteLabel, applied.detectPaperWhite.value
                    ? "Detected from the display"
                    : $"{applied.paperWhite.value} nits (in effect)");
        }

        // Like the profile's Add Override, seeded with the values in effect. Mode stays unticked but shows the mode in
        // effect, not None. Paper white starts ticked at its current value, so nothing changes until it is edited.
        private static void AddTonemappingOverride(VolumeProfile profile)
        {
            var applied = SetupValidator.StackTonemapping();
            Undo.RecordObject(profile, "Add Tonemapping Override");
            var tonemapping = profile.Add<Tonemapping>();
            if (applied != null)
            {
                tonemapping.mode.value = applied.mode.value;
                tonemapping.detectPaperWhite.Override(applied.detectPaperWhite.value);
                tonemapping.paperWhite.Override(applied.paperWhite.value);
            }
            Undo.RegisterCreatedObjectUndo(tonemapping, "Add Tonemapping Override");
            AssetDatabase.AddObjectToAsset(tonemapping, profile); // OwningProfile only returns assets
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        private static readonly GUIContent PaperWhiteDetectLabel = new("Detect Paper White",
            "Use the paper white the display reports. A URP Tonemapping setting.");
        private static readonly GUIContent PaperWhiteLabel = new("Paper White",
            "Brightness of SDR white and UI on HDR displays, in nits. Highlights still stop at the peak. A URP Tonemapping setting.");

        private SerializedObject _tonemapping;
        private SerializedDataParameter _detectPaperWhite;
        private SerializedDataParameter _paperWhite;

        private static void DrawCustomLutValidation(Texture2D lutTexture)
        {
            if (!UrpBridge.TryValidateCustomLut(lutTexture, out var error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(lutTexture);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            var hasInvalidSettings = importer.sRGBTexture || importer.mipmapEnabled ||
                                     importer.npotScale != TextureImporterNPOTScale.None ||
                                     importer.wrapMode != TextureWrapMode.Clamp ||
                                     importer.filterMode != FilterMode.Bilinear ||
                                     importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!hasInvalidSettings)
                return;

            EditorGUILayout.HelpBox(
                "Custom LUT import settings should be linear, uncompressed, clamp-wrapped, bilinear-filtered, and mipmaps disabled.",
                MessageType.Warning);
            if (GUILayout.Button("Fix LUT Import Settings"))
            {
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private void DrawAdditionalPropertiesSection()
        {
            var currentMode = (ToneMappingMode)_mode.value.intValue;

            // Only show LUT size for baked tone mapping modes
            // CustomLUT uses the actual texture dimensions
            if (currentMode != ToneMappingMode.None && currentMode != ToneMappingMode.CustomLUT)
            {
                PropertyField(_lutSize);
            }

            // Debug export section
            var lutTexture = _lutTexture.value.objectReferenceValue as Texture2D;
            LutExporter.DrawDebugExportSection(currentMode, lutTexture);
        }
    }
}
