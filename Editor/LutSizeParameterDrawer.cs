using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace CustomToneMapping.URP.Editor
{
    [VolumeParameterDrawer(typeof(LutSizeParameter))]
    public sealed class LutSizeParameterDrawer : VolumeParameterDrawer
    {
        public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
        {
            var value = parameter.value;

            if (value.propertyType != SerializedPropertyType.Integer)
                return false;

            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, title, value);

            var labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            EditorGUI.LabelField(labelRect, title);

            var fieldRect = new Rect(
                rect.x + EditorGUIUtility.labelWidth,
                rect.y,
                rect.width - EditorGUIUtility.labelWidth,
                rect.height
            );

            EditorGUI.BeginChangeCheck();
            int newValue = EditorGUI.IntField(fieldRect, value.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                // Clamp the value to the valid range
                newValue = Mathf.Clamp(newValue, LutSizeParameter.MinLutSize, LutSizeParameter.MaxLutSize);
                value.intValue = newValue;
            }

            EditorGUI.EndProperty();

            return true;
        }
    }
}
