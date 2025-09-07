using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CustomToneMapping.URP.Editor
{
    /// <summary>
    /// Handles exporting LUT textures to EXR format for debugging and external use.
    /// </summary>
    internal static class LutExporter
    {
        internal static void DrawDebugExportSection(ToneMappingMode mode2, Texture2D lutTexture)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Export", EditorStyles.boldLabel);

            // Get the cached LUT texture
            var cachedLut = GetCurrentLutTexture(mode2, lutTexture);

            // Enable/disable button based on LUT availability
            using (new EditorGUI.DisabledScope(cachedLut == null))
            {
                if (cachedLut == null)
                {
                    EditorGUILayout.HelpBox(
                        "No cached LUT available. The LUT will be generated when the scene is rendered with this volume active.",
                        MessageType.Info);
                }

                if (GUILayout.Button("Export Cached LUT to EXR"))
                {
                    // Capture all necessary data immediately
                    var texture = cachedLut;

                    // Defer the export to avoid layout group errors
                    EditorApplication.delayCall += () =>
                    {
                        if (texture != null)
                        {
                            ExportLutToExr(texture, mode2);
                        }
                        else
                        {
                            Debug.LogError("LUT texture became null before export could complete");
                        }
                    };
                }
            }
        }

        private static Texture2D GetCurrentLutTexture(ToneMappingMode mode, Texture2D customLutTexture)
        {
            // For CustomLUT mode, return the assigned texture
            if (mode == ToneMappingMode.CustomLUT)
            {
                return customLutTexture;
            }

            // For GT7 and AgX modes, return the cached LUT from UrpBridge
            return UrpBridge.CachedLutTexture;
        }

        private static void ExportLutToExr(Texture2D lutTexture, ToneMappingMode mode2)
        {
            if (lutTexture == null)
            {
                Debug.LogError("No LUT texture to export");
                return;
            }

            try
            {
                // Open save file dialog
                var defaultName = $"LUT_{mode2}_{DateTime.Now:yyyyMMdd_HHmmss}.exr";
                var path = EditorUtility.SaveFilePanel(
                    "Save LUT as EXR",
                    Application.dataPath,
                    defaultName,
                    "exr"
                );

                if (string.IsNullOrEmpty(path))
                {
                    // User cancelled
                    return;
                }

                // All LUT textures from LutBaker are guaranteed to be readable
                var width = lutTexture.width;
                var height = lutTexture.height;

                var exportTexture = new Texture2D(width, height, lutTexture.format, false, true);
                Graphics.CopyTexture(lutTexture, exportTexture);

                // Export to EXR with compression
                var bytes = exportTexture.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
                File.WriteAllBytes(path, bytes);

                // Cleanup
                UnityEngine.Object.DestroyImmediate(exportTexture);

                Debug.Log($"LUT exported successfully to: {path}");
                AssetDatabase.Refresh();

                // Ping the file in Project window if it's within the project
                var relativePath = "Assets" + path.Replace(Application.dataPath, "");
                if (File.Exists(relativePath))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to export LUT: {e.Message}");
            }
        }
    }
}