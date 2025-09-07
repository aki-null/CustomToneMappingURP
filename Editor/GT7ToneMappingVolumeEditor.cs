using CustomToneMapping.Baker.GT7;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP.Editor
{
    [CustomEditor(typeof(GT7ToneMapping))]
    [SupportedOnRenderPipeline(typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset))]
    public sealed class GT7ToneMappingVolumeEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _detectPeakNits;
        private SerializedDataParameter _targetPeakNits;
        private SerializedDataParameter _sdrPaperWhite;
        private SerializedDataParameter _referenceLuminance;
        private SerializedDataParameter _curveAlpha;
        private SerializedDataParameter _curveMidPoint;
        private SerializedDataParameter _curveLinearSection;
        private SerializedDataParameter _curveToeStrength;
        private SerializedDataParameter _ucs;
        private SerializedDataParameter _jzazbzExponentScale;
        private SerializedDataParameter _blendRatio;
        private SerializedDataParameter _fadeStart;
        private SerializedDataParameter _fadeEnd;

        private bool _previewHdr;
        private float _previewPeakNits = 1000f;
        private const float CurvePreviewHeight = 200f;

        public override void OnEnable()
        {
            try
            {
                base.OnEnable();

                var o = new PropertyFetcher<GT7ToneMapping>(serializedObject);

                _detectPeakNits = Unpack(o.Find(x => x.detectPeakNits));
                _targetPeakNits = Unpack(o.Find(x => x.targetPeakNits));
                _sdrPaperWhite = Unpack(o.Find(x => x.sdrPaperWhite));
                _referenceLuminance = Unpack(o.Find(x => x.referenceLuminance));

                _curveAlpha = Unpack(o.Find(x => x.curveAlpha));
                _curveMidPoint = Unpack(o.Find(x => x.curveMidPoint));
                _curveLinearSection = Unpack(o.Find(x => x.curveLinearSection));
                _curveToeStrength = Unpack(o.Find(x => x.curveToeStrength));

                _ucs = Unpack(o.Find(x => x.ucs));
                _jzazbzExponentScale = Unpack(o.Find(x => x.jzazbzExponentScale));
                _blendRatio = Unpack(o.Find(x => x.blendRatio));
                _fadeStart = Unpack(o.Find(x => x.fadeStart));
                _fadeEnd = Unpack(o.Find(x => x.fadeEnd));
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

            DrawCurveParameters();
            EditorGUILayout.Space();

            DrawPreviewControls();
            EditorGUILayout.Space(2);
            DrawCurvePreview();
            EditorGUILayout.Space();

            DrawUcsAndBlendingSettings();
        }

        private void DrawCurveParameters()
        {
            EditorGUILayout.LabelField("Curve Parameters", EditorStyles.boldLabel);
            PropertyField(_curveAlpha);
            PropertyField(_curveMidPoint);
            PropertyField(_curveLinearSection);
            PropertyField(_curveToeStrength);
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

        private void DrawUcsAndBlendingSettings()
        {
            EditorGUILayout.LabelField("UCS & Blending", EditorStyles.boldLabel);
            PropertyField(_ucs);

            if (_ucs.value.intValue == (int)UcsMode.JzAzBz)
            {
                PropertyField(_jzazbzExponentScale);
            }

            PropertyField(_blendRatio);
            PropertyField(_fadeStart);
            PropertyField(_fadeEnd);
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

            GT7CurvePreview.DrawCurvePreview(rect, config, previewPeak, _previewHdr);
        }

        private GT7Config BuildPreviewConfig()
        {
            return new GT7Config
            {
                ReferenceLuminance = GetParameterValue(_referenceLuminance, GT7ToneMapping.DefaultReferenceLuminance),
                SdrPaperWhite = GetParameterValue(_sdrPaperWhite, GT7ToneMapping.DefaultSdrPaperWhite),
                Ucs = _ucs.overrideState.boolValue ? (UcsMode)_ucs.value.intValue : GT7ToneMapping.DefaultUcs,
                JzazbzExponentScaleFactor =
                    GetParameterValue(_jzazbzExponentScale, GT7ToneMapping.DefaultJzazbzExponentScale),
                CurveAlpha = GetParameterValue(_curveAlpha, GT7ToneMapping.DefaultCurveAlpha),
                CurveMidPoint = GetParameterValue(_curveMidPoint, GT7ToneMapping.DefaultCurveMidPoint),
                CurveLinearSection = GetParameterValue(_curveLinearSection, GT7ToneMapping.DefaultCurveLinearSection),
                CurveToeStrength = GetParameterValue(_curveToeStrength, GT7ToneMapping.DefaultCurveToeStrength),
                BlendRatio = GetParameterValue(_blendRatio, GT7ToneMapping.DefaultBlendRatio),
                FadeStart = GetParameterValue(_fadeStart, GT7ToneMapping.DefaultFadeStart),
                FadeEnd = GetParameterValue(_fadeEnd, GT7ToneMapping.DefaultFadeEnd),
            };
        }

        private static float GetParameterValue(SerializedDataParameter param, float defaultValue)
        {
            return param.overrideState.boolValue ? param.value.floatValue : defaultValue;
        }
    }
}