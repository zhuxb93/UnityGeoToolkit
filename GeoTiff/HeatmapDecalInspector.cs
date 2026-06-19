#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;

namespace GeoToolkit.EditorTools.GeoTiff
{
    [CustomEditor(typeof(DecalProjector))]
    [CanEditMultipleObjects]
    public class HeatmapDecalInspector : Editor
    {
        private bool showHeatmapSettings = false;

        public override void OnInspectorGUI()
        {
            // 绘制默认的DecalProjector Inspector
            DrawDefaultInspector();

            var decalProjector = target as DecalProjector;
            if (decalProjector?.material != null)
            {
                // 检查是否是热力图材质
                if (IsHeatmapMaterial(decalProjector.material))
                {
                    EditorGUILayout.Space(10);
                    showHeatmapSettings = EditorGUILayout.Foldout(showHeatmapSettings, "热力图设置", true);

                    if (showHeatmapSettings)
                    {
                        EditorGUILayout.BeginVertical("box");
                        DrawHeatmapControls(decalProjector.material);
                        EditorGUILayout.EndVertical();
                    }
                }
            }
        }

        private bool IsHeatmapMaterial(Material material)
        {
            return material.shader != null &&
                   (material.shader.name == "Shader Graphs/HeatmapDecal" ||
                    material.shader.name == "GeoToolkit/HeatmapDecal");
        }

        private void DrawHeatmapControls(Material material)
        {
            EditorGUI.BeginChangeCheck();

            // 数据范围设置
            EditorGUILayout.LabelField("数据范围", EditorStyles.boldLabel);

            float minValue = material.GetFloat("_MinValue");
            float maxValue = material.GetFloat("_MaxValue");

            minValue = EditorGUILayout.FloatField("最小值", minValue);
            maxValue = EditorGUILayout.FloatField("最大值", maxValue);

            if (maxValue <= minValue)
            {
                EditorGUILayout.HelpBox("最大值必须大于最小值", MessageType.Warning);
            }

            EditorGUILayout.Space();

            // 显示控制
            EditorGUILayout.LabelField("显示控制", EditorStyles.boldLabel);

            float alpha = material.GetFloat("_Alpha");
            alpha = EditorGUILayout.Slider("透明度", alpha, 0f, 1f);

            EditorGUILayout.Space();

            // 纹理设置
            EditorGUILayout.LabelField("纹理设置", EditorStyles.boldLabel);

            Texture2D dataTexture = material.GetTexture("_DataTexture") as Texture2D;
            Texture2D colorRamp = material.GetTexture("_ColorRamp") as Texture2D;

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("数据纹理", dataTexture, typeof(Texture2D), false);
            EditorGUI.EndDisabledGroup();

            colorRamp = EditorGUILayout.ObjectField("色带纹理", colorRamp, typeof(Texture2D), false) as Texture2D;

            if (EditorGUI.EndChangeCheck())
            {
                // 应用更改
                Undo.RecordObject(material, "修改热力图设置");

                material.SetFloat("_MinValue", minValue);
                material.SetFloat("_MaxValue", maxValue);
                material.SetFloat("_Alpha", alpha);

                if (colorRamp != null)
                {
                    material.SetTexture("_ColorRamp", colorRamp);
                }

                EditorUtility.SetDirty(material);
            }

            EditorGUILayout.Space();

            // 工具按钮
            EditorGUILayout.LabelField("工具", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重置数据范围"))
                {
                    ResetDataRange(material);
                }

                if (GUILayout.Button("创建默认色带"))
                {
                    CreateDefaultColorRamp(material);
                }
            }

            // 显示当前数据信息
            if (dataTexture != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("纹理信息", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"尺寸: {dataTexture.width} x {dataTexture.height}");
                EditorGUILayout.LabelField($"格式: {dataTexture.format}");

                string assetPath = AssetDatabase.GetAssetPath(dataTexture);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    EditorGUILayout.LabelField($"路径: {assetPath}");
                }
            }
        }

        private void ResetDataRange(Material material)
        {
            Undo.RecordObject(material, "重置数据范围");
            material.SetFloat("_MinValue", 0f);
            material.SetFloat("_MaxValue", 1f);
            EditorUtility.SetDirty(material);
        }

        private void CreateDefaultColorRamp(Material material)
        {
            try
            {
                // 使用HeatmapDecalMaterial工具创建默认色带
                var colorRamp = CreateDefaultHeatmapRamp();

                if (colorRamp != null)
                {
                    Undo.RecordObject(material, "设置默认色带");
                    material.SetTexture("_ColorRamp", colorRamp);
                    EditorUtility.SetDirty(material);

                    Debug.Log("[HeatmapDecal] 已设置默认色带纹理");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HeatmapDecal] 创建默认色带失败: {ex.Message}");
            }
        }

        private Texture2D CreateDefaultHeatmapRamp()
        {
            int width = 256;
            int height = 1;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            Color[] colors = new Color[width];

            for (int i = 0; i < width; i++)
            {
                float t = (float)i / (width - 1);
                colors[i] = GetHeatmapColor(t);
            }

            texture.SetPixels(colors);
            texture.Apply();
            texture.name = "DefaultHeatmapRamp";

            return texture;
        }

        private Color GetHeatmapColor(float value)
        {
            value = Mathf.Clamp01(value);

            // 5段式彩虹色带: 蓝->青->绿->黄->红
            if (value < 0.25f)
            {
                float t = value / 0.25f;
                return Color.Lerp(Color.blue, Color.cyan, t);
            }
            else if (value < 0.5f)
            {
                float t = (value - 0.25f) / 0.25f;
                return Color.Lerp(Color.cyan, Color.green, t);
            }
            else if (value < 0.75f)
            {
                float t = (value - 0.5f) / 0.25f;
                return Color.Lerp(Color.green, Color.yellow, t);
            }
            else
            {
                float t = (value - 0.75f) / 0.25f;
                return Color.Lerp(Color.yellow, Color.red, t);
            }
        }
    }
}
#endif