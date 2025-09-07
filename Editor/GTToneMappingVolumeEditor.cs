using CustomToneMapping.Baker.GT;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using GTToneMappingVolume = CustomToneMapping.URP.GT.GTToneMapping;

namespace CustomToneMapping.URP.Editor
{
    [CustomEditor(typeof(GTToneMappingVolume))]
    [SupportedOnRenderPipeline(typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset))]
    public sealed class GTToneMappingVolumeEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _detectPeakNits;
        private SerializedDataParameter _targetPeakNits;
        private SerializedDataParameter _sdrPaperWhite;
        private SerializedDataParameter _referenceLuminance;
        private SerializedDataParameter _contrast;
        private SerializedDataParameter _linearSectionStart;
        private SerializedDataParameter _linearSectionLength;
        private SerializedDataParameter _blackTightness;
        private SerializedDataParameter _blackOffset;

        private bool _previewHdr;
        private float _previewPeakNits = 1000f;
        private const float CurvePreviewHeight = 200f;

        public override void OnEnable()
        {
            try
            {
                base.OnEnable();

                var o = new PropertyFetcher<GTToneMappingVolume>(serializedObject);

                _detectPeakNits = Unpack(o.Find(x => x.detectPeakNits));
                _targetPeakNits = Unpack(o.Find(x => x.targetPeakNits));
                _sdrPaperWhite = Unpack(o.Find(x => x.sdrPaperWhite));
                _referenceLuminance = Unpack(o.Find(x => x.referenceLuminance));

                _contrast = Unpack(o.Find(x => x.contrast));
                _linearSectionStart = Unpack(o.Find(x => x.linearSectionStart));
                _linearSectionLength = Unpack(o.Find(x => x.linearSectionLength));
                _blackTightness = Unpack(o.Find(x => x.blackTightness));
                _blackOffset = Unpack(o.Find(x => x.blackOffset));
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

            EditorGUILayout.LabelField("HDR Output", EditorStyles.boldLabel);
            if (PlayerSettings.allowHDRDisplaySupport)
            {
                PropertyField(_detectPeakNits);

                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(_detectPeakNits.value.boolValue))
                {
                    PropertyField(_targetPeakNits);
                }

                EditorGUI.indentLevel--;
            }

            PropertyField(_sdrPaperWhite);
            PropertyField(_referenceLuminance);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Curve Parameters", EditorStyles.boldLabel);
            PropertyField(_contrast);
            PropertyField(_linearSectionStart);
            PropertyField(_linearSectionLength);
            PropertyField(_blackTightness);
            PropertyField(_blackOffset);
            EditorGUILayout.Space();

            DrawPreviewControls();
            EditorGUILayout.Space(2);
            DrawCurvePreview();
        }

        private void DrawPreviewControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (_previewHdr)
                {
                    EditorGUILayout.LabelField("Peak Nits", GUILayout.Width(60));
                    _previewPeakNits = EditorGUILayout.FloatField(_previewPeakNits, GUILayout.Width(60));
                    _previewPeakNits = Mathf.Clamp(_previewPeakNits, 100f, 10000f);
                    GUILayout.Space(5);
                }

                using (new EditorGUILayout.HorizontalScope(GUILayout.Width(100)))
                {
                    if (GUILayout.Toggle(!_previewHdr, "SDR", "ButtonLeft", GUILayout.Width(50)))
                    {
                        _previewHdr = false;
                    }

                    if (GUILayout.Toggle(_previewHdr, "HDR", "ButtonRight", GUILayout.Width(50)))
                    {
                        _previewHdr = true;
                    }
                }

                GUILayout.Space(6);
            }
        }

        private void DrawCurvePreview()
        {
            var rect = GUILayoutUtility.GetRect(
                EditorGUIUtility.currentViewWidth - 35,
                CurvePreviewHeight,
                GUILayout.ExpandWidth(true)
            );

            if (Event.current.type != EventType.Repaint)
                return;

            var config = BuildPreviewConfig();
            var previewPeak = _previewHdr ? _previewPeakNits : config.SdrPaperWhite;
            config.TargetPeakNits = previewPeak;
            config.IsHdrOutput = _previewHdr;

            GTCurvePreview.DrawCurvePreview(rect, config, previewPeak, _previewHdr);
        }

        private GTConfig BuildPreviewConfig()
        {
            return new GTConfig
            {
                ReferenceLuminance =
                    GetParameterValue(_referenceLuminance, GTToneMappingVolume.DefaultReferenceLuminance),
                SdrPaperWhite = GetParameterValue(_sdrPaperWhite, GTToneMappingVolume.DefaultSdrPaperWhite),
                Contrast = GetParameterValue(_contrast, GTToneMappingVolume.DefaultContrast),
                LinearSectionStart =
                    GetParameterValue(_linearSectionStart, GTToneMappingVolume.DefaultLinearSectionStart),
                LinearSectionLength =
                    GetParameterValue(_linearSectionLength, GTToneMappingVolume.DefaultLinearSectionLength),
                BlackTightness = GetParameterValue(_blackTightness, GTToneMappingVolume.DefaultBlackTightness),
                BlackOffset = GetParameterValue(_blackOffset, GTToneMappingVolume.DefaultBlackOffset),
            };
        }

        private static float GetParameterValue(SerializedDataParameter param, float defaultValue)
        {
            return param.overrideState.boolValue ? param.value.floatValue : defaultValue;
        }
    }
}