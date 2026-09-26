using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP.Editor
{
    [CustomEditor(typeof(Aces2ToneMapping))]
    [SupportedOnRenderPipeline(typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset))]
    public sealed class Aces2ToneMappingVolumeEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _detectPeakNits;
        private SerializedDataParameter _targetPeakNits;
        private SerializedDataParameter _acesAwareGrading;

        public override void OnEnable()
        {
            try
            {
                base.OnEnable();

                var o = new PropertyFetcher<Aces2ToneMapping>(serializedObject);

                _detectPeakNits = Unpack(o.Find(x => x.detectPeakNits));
                _targetPeakNits = Unpack(o.Find(x => x.targetPeakNits));
                _acesAwareGrading = Unpack(o.Find(x => x.acesAwareGrading));
            }
            catch (System.Exception)
            {
                // Silently ignore - this can happen during domain reload when the serialized object isn't ready
            }
        }

        public override void OnInspectorGUI()
        {
            if (_detectPeakNits == null)
                return;

            SetupValidator.DrawAces2GradingValidation(target as VolumeComponent);

            // Like the other modes: HDR settings only when the project can output to an HDR display, and the manual
            // peak only while detection is off.
            if (PlayerSettings.allowHDRDisplaySupport)
            {
                EditorGUILayout.LabelField("HDR Output", EditorStyles.boldLabel);
                PropertyField(_detectPeakNits);

                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(_detectPeakNits.value.boolValue))
                {
                    PropertyField(_targetPeakNits);
                }

                EditorGUI.indentLevel--;

                EditorGUILayout.Space();
            }

            PropertyField(_acesAwareGrading);
        }
    }
}
