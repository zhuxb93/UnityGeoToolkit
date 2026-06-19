#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GeoToolkit.GeoTiffTools
{
    public static class HeatmapDecalMaterial
    {
        private const string SHADER_NAME = "Shader Graphs/HeatmapDecal";
        private const string DEFAULT_COLOR_RAMP_PATH = "Packages/GeoToolkit/Editor/GeoTiffTools/Textures/DefaultHeatmapRamp.png";

        /// <summary>
        /// 创建热力图贴花材质
        /// </summary>
        /// <param name="dataTexture">数据纹理 (RFloat格式)</param>
        /// <param name="minValue">数据最小值</param>
        /// <param name="maxValue">数据最大值</param>
        /// <param name="colorRamp">可选的自定义色带纹理</param>
        /// <returns>配置好的热力图材质</returns>
        public static Material CreateHeatmapMaterial(Texture2D dataTexture, float minValue, float maxValue, Texture2D colorRamp = null)
        {
            var shader = Shader.Find(SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"[HeatmapDecal] 找不到热力图shader: {SHADER_NAME}");
                // 回退到标准HDRP Decal shader
                shader = Shader.Find("HDRP/Decal");
            }

            var material = new Material(shader);
            material.name = $"HeatmapDecal_{dataTexture?.name ?? "Material"}";

            // 设置数据纹理
            if (dataTexture != null)
            {
                material.SetTexture("_DataTexture", dataTexture);
            }

            // 设置数据范围
            material.SetFloat("_MinValue", minValue);
            material.SetFloat("_MaxValue", maxValue);

            // 设置色带纹理
            Texture2D rampTexture = colorRamp;
            if (rampTexture == null)
            {
                rampTexture = GetOrCreateDefaultColorRamp();
            }

            if (rampTexture != null)
            {
                material.SetTexture("_ColorRamp", rampTexture);
            }

            // 设置默认属性
            material.SetFloat("_Alpha", 0.8f);

            // 设置HDRP Decal属性
            if (material.HasProperty("_AffectAlbedo"))
                material.SetFloat("_AffectAlbedo", 1.0f);
            if (material.HasProperty("_AffectNormal"))
                material.SetFloat("_AffectNormal", 1.0f);

            // 设置 Decal Color Mask 属性
            if (material.HasProperty("_DecalColorMask0"))
                material.SetFloat("_DecalColorMask0", 15.0f); // RGBA = 1111 = 15
            if (material.HasProperty("_DecalColorMask1"))
                material.SetFloat("_DecalColorMask1", 15.0f);
            if (material.HasProperty("_DecalColorMask2"))
                material.SetFloat("_DecalColorMask2", 0.0f);
            if (material.HasProperty("_DecalColorMask3"))
                material.SetFloat("_DecalColorMask3", 0.0f);

            Debug.Log($"[HeatmapDecal] 已创建热力图材质，数据范围: [{minValue:F2}, {maxValue:F2}]");
            return material;
        }

        /// <summary>
        /// 获取或创建默认热力图色带
        /// </summary>
        private static Texture2D GetOrCreateDefaultColorRamp()
        {
            // 尝试加载现有的色带纹理
            var existingRamp = AssetDatabase.LoadAssetAtPath<Texture2D>(DEFAULT_COLOR_RAMP_PATH);
            if (existingRamp != null)
            {
                return existingRamp;
            }

            // 创建新的色带纹理
            var colorRamp = CreateDefaultHeatmapRamp();

            // 确保目录存在
            string directory = System.IO.Path.GetDirectoryName(DEFAULT_COLOR_RAMP_PATH);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            // 保存为PNG
            byte[] pngData = colorRamp.EncodeToPNG();
            System.IO.File.WriteAllBytes(DEFAULT_COLOR_RAMP_PATH, pngData);

            AssetDatabase.Refresh();

            // 重新加载并设置导入设置
            var importer = AssetImporter.GetAtPath(DEFAULT_COLOR_RAMP_PATH) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            Debug.Log($"[HeatmapDecal] 已创建默认色带纹理: {DEFAULT_COLOR_RAMP_PATH}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(DEFAULT_COLOR_RAMP_PATH);
        }

        /// <summary>
        /// 创建默认的热力图色带纹理 (蓝->青->绿->黄->红)
        /// </summary>
        private static Texture2D CreateDefaultHeatmapRamp()
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

        /// <summary>
        /// 获取热力图颜色 (基于标准的彩虹色带)
        /// </summary>
        /// <param name="value">归一化值 [0,1]</param>
        /// <returns>对应的颜色</returns>
        private static Color GetHeatmapColor(float value)
        {
            value = Mathf.Clamp01(value);

            // 5段式彩虹色带: 蓝->青->绿->黄->红
            if (value < 0.25f)
            {
                // 蓝到青 (0-0.25)
                float t = value / 0.25f;
                return Color.Lerp(Color.blue, Color.cyan, t);
            }
            else if (value < 0.5f)
            {
                // 青到绿 (0.25-0.5)
                float t = (value - 0.25f) / 0.25f;
                return Color.Lerp(Color.cyan, Color.green, t);
            }
            else if (value < 0.75f)
            {
                // 绿到黄 (0.5-0.75)
                float t = (value - 0.5f) / 0.25f;
                return Color.Lerp(Color.green, Color.yellow, t);
            }
            else
            {
                // 黄到红 (0.75-1)
                float t = (value - 0.75f) / 0.25f;
                return Color.Lerp(Color.yellow, Color.red, t);
            }
        }

        /// <summary>
        /// 更新现有材质的数据范围
        /// </summary>
        public static void UpdateMaterialDataRange(Material material, float minValue, float maxValue)
        {
            if (material == null) return;

            material.SetFloat("_MinValue", minValue);
            material.SetFloat("_MaxValue", maxValue);

            Debug.Log($"[HeatmapDecal] 已更新材质数据范围: [{minValue:F2}, {maxValue:F2}]");
        }


        /// <summary>
        /// 更新材质的透明度
        /// </summary>
        public static void UpdateMaterialAlpha(Material material, float alpha)
        {
            if (material == null) return;

            material.SetFloat("_Alpha", Mathf.Clamp01(alpha));
            Debug.Log($"[HeatmapDecal] 已更新材质透明度: {alpha:F2}");
        }

        /// <summary>
        /// 检查材质是否是热力图贴花材质
        /// </summary>
        public static bool IsHeatmapDecalMaterial(Material material)
        {
            return material != null && material.shader != null &&
                   (material.shader.name == SHADER_NAME ||
                    material.shader.name == "GeoToolkit/HeatmapDecal"); // 兼容旧着色器名称
        }
    }
}
#endif