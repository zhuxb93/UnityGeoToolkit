#if UNITY_EDITOR
using GeoToolkit;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class HeatMapData
{
    public List<DateTime> times;
    public Dictionary<DateTime, List<Info>> data;
}

public struct Info
{
    public float[] location;
    public float value;
}

public class BorderTileId
{
    public TileID downLeft;
    public TileID upRight;
}

public class HeatMapGenerator
{
    public static void CreateHeatMap(Vector2d origin, string json, float scale, BorderTileId borderTileId, string savePath)
    {
        Vector2d center = Conversions.LatLonToMeters(origin);
        HeatMapData heatMapData = JsonConvert.DeserializeObject<HeatMapData>(json);
        List<DateTime> times = heatMapData.times;
        Dictionary<DateTime, List<Info>> data = heatMapData.data;
        (Vector2 size, Vector2 min) = CalculateBorderSize(borderTileId, center, scale);
        Gradient rainfallGradient = new Gradient();
        GradientColorKey[] colorsKey = new GradientColorKey[4];
        colorsKey[0] = new GradientColorKey(Color.blue, 0.0f);
        colorsKey[1] = new GradientColorKey(Color.cyan, 0.33f);
        colorsKey[2] = new GradientColorKey(Color.yellow, 0.66f);
        colorsKey[3] = new GradientColorKey(Color.red, 1.0f);
        GradientAlphaKey[] alphaKey = new GradientAlphaKey[2];
        alphaKey[0] = new GradientAlphaKey(0.6f, 0.0f);
        alphaKey[1] = new GradientAlphaKey(0.9f, 1.0f);
        rainfallGradient.colorKeys = colorsKey;
        rainfallGradient.alphaKeys = alphaKey;
        foreach (var item in data)
        {
            List<Info> infos = item.Value;
            List<Vector3> points = new List<Vector3>();
            List<float> vals = new List<float>();
            foreach (var info in infos)
            {
                vals.Add(info.value);
                Vector2d worldPos = Conversions.GeoToWorldPosition(info.location[1], info.location[0], center);
                Vector3 point = new Vector3((float)worldPos.x, info.location[2], (float)worldPos.y);
                point *= scale;
                point += Vector3.up * 50;
                points.Add(point);
            }

            List<Vector2> uvPoints = new List<Vector2>();
            foreach (var point in points)
            {
                Vector2 uv = new Vector2(
                    (point.x - min.x) / size.x,
                    (point.z - min.y) / size.y
                );
                uvPoints.Add(uv);
            }

            int texWidth = 512;
            int texHeight = 512;
            float sigmaUV = 0.05f;
            float[] intensity = new float[texWidth * texHeight];
            for (int m = 0; m < uvPoints.Count; m++)
            {
                Vector2 uv = uvPoints[m];
                int centerX = (int)(uv.x * texWidth);
                int centerY = (int)(uv.y * texHeight);

                int radius = (int)(sigmaUV * texWidth * 3);
                int startX = Mathf.Max(0, centerX - radius);
                int endX = Mathf.Min(texWidth, centerX + radius);
                int startY = Mathf.Max(0, centerY - radius);
                int endY = Mathf.Min(texHeight, centerY + radius);

                for (int y = startY; y < endY; y++)
                {
                    for (int x = startX; x < endX; x++)
                    {
                        float dx = (x - centerX) / (float)texWidth;
                        float dy = (y - centerY) / (float)texHeight;
                        float distanceSqr = dx * dx + dy * dy;
                        float weight = Mathf.Exp(-distanceSqr / (2 * sigmaUV * sigmaUV));
                        int idx = y * texWidth + x;
                        intensity[idx] += vals[m] * weight;
                    }
                }
            }

            float maxIntensity = 0;
            for (int i = 0; i < intensity.Length; i++)
                maxIntensity = Mathf.Max(maxIntensity, intensity[i]);

            Color[] colors = new Color[texWidth * texHeight];

            for (int i = 0; i < colors.Length; i++)
            {
                float normalized = maxIntensity > 0 ? intensity[i] / maxIntensity : 0;
                colors[i] = rainfallGradient.Evaluate(normalized);
            }

            Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            tex.SetPixels(colors);
            tex.Apply();
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
            string texSavePath = $"{savePath}/{item.Key.ToString("yyyy_MM_dd_HH_mm_ss")}.png";
            byte[] pngData = tex.EncodeToPNG();
            File.WriteAllBytes(texSavePath, pngData);
            AssetDatabase.Refresh();
        }
    }

    public static (Vector2, Vector2) CalculateBorderSize(BorderTileId borderTileId, Vector2d center, float scale)
    {
        Vector2dBounds dlBounds = Conversions.TileIdToBounds(borderTileId.downLeft.x, borderTileId.downLeft.y, borderTileId.downLeft.z);
        Vector2d dl = new Vector2d(dlBounds.East, dlBounds.North);
        Vector2dBounds urBounds = Conversions.TileIdToBounds(borderTileId.upRight.x, borderTileId.upRight.y, borderTileId.upRight.z);
        Vector2d ur = new Vector2d(urBounds.West, urBounds.South);
        Vector2d dlPos = Conversions.GeoToWorldPosition(dl.y, dl.x, center) * scale;
        Vector2d urPos = Conversions.GeoToWorldPosition(ur.y, ur.x, center) * scale;
        Vector2d size = urPos - dlPos;
        return (new Vector2((float)size.x, (float)size.y), new Vector2((float)dlPos.x, (float)dlPos.y));
    }

    //[MenuItem("Test/Test3")]
    //public static void Test3()
    //{
    //    string path = Application.dataPath + "/TestRain/rain_heatmap_data.json";
    //    string geojson = File.ReadAllText(path);
    //    GeoPlatformConfig config = ScriptableObject.CreateInstance<GeoPlatformConfig>();
    //    GeoCoordinateUtils.Initialize(config);
    //    double scale = GeoCoordinateUtils.CalculateDistanceRatio((int)TerrainSize.Terrain_512, 14);
    //    Vector2d center = new Vector2d(30.581179257386985f, 103.86474609375f);
    //    BorderTileId borderTileId = new BorderTileId();
    //    borderTileId.downLeft = new TileID(12919, 6752, 14);
    //    borderTileId.upRight = new TileID(12941, 6728, 14);
    //    CreateHeatMap(center, geojson, (float)scale, borderTileId, "Assets/RainHeatMap/Textures");
    //}
}

#endif