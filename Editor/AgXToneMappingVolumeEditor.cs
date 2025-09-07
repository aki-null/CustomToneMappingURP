using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP.Editor
{
    [CustomEditor(typeof(AgXToneMapping))]
    [SupportedOnRenderPipeline(typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset))]
    public sealed class AgXTonemappingVolumeEditor : VolumeComponentEditor
    {
        // HDR Output parameters
        private SerializedDataParameter _detectBrightnessLimits;
        private SerializedDataParameter _maxNits;
        private SerializedDataParameter _sdrPaperWhite;

        // Look parameters
        private SerializedDataParameter _look;
        private SerializedDataParameter _lookIntensity;

        // HDR Settings parameters
        private SerializedDataParameter _hdrPurity;
        private SerializedDataParameter _hdrExtraPowerFactor;

        // Working Space Transform parameters
        private SerializedDataParameter _useP3Limit;

        public override void OnEnable()
        {
            try
            {
                base.OnEnable();

                var o = new PropertyFetcher<AgXToneMapping>(serializedObject);

                _detectBrightnessLimits = Unpack(o.Find(x => x.detectBrightnessLimits));
                _maxNits = Unpack(o.Find(x => x.maxNits));
                _sdrPaperWhite = Unpack(o.Find(x => x.sdrPaperWhite));

                _look = Unpack(o.Find(x => x.look));
                _lookIntensity = Unpack(o.Find(x => x.lookIntensity));

                _hdrPurity = Unpack(o.Find(x => x.hdrPurity));
                _hdrExtraPowerFactor = Unpack(o.Find(x => x.hdrExtraPowerFactor));

                _useP3Limit = Unpack(o.Find(x => x.useP3Limit));
            }
            catch (System.Exception)
            {
                // Silently ignore - this can happen during domain reload when the serialized object isn't ready
            }
        }

        public override void OnInspectorGUI()
        {
            // Skip drawing if initialization failed
            if (_look == null)
                return;

            // HDR Output section - only show if HDR is enabled in PlayerSettings
            if (PlayerSettings.allowHDRDisplaySupport)
            {
                EditorGUILayout.LabelField("HDR Output", EditorStyles.boldLabel);
                PropertyField(_detectBrightnessLimits);

                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(_detectBrightnessLimits.value.boolValue))
                {
                    PropertyField(_maxNits);
                }

                EditorGUI.indentLevel--;

                PropertyField(_sdrPaperWhite);
                PropertyField(_hdrPurity);
                PropertyField(_hdrExtraPowerFactor);

                EditorGUILayout.Space();
            }

            EditorGUILayout.LabelField("Look", EditorStyles.boldLabel);
            PropertyField(_look);
            PropertyField(_lookIntensity);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Miscellaneous", EditorStyles.boldLabel);
            PropertyField(_useP3Limit);
        }
    }
}