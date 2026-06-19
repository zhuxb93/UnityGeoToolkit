using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;

/// <summary>
/// 编译建筑模型数据、材质、贴图为AB包
/// </summary>
public static class TileBundleBuilder
{
    private static string commonOutput = "AssetBundles/Common";   // 公共目录
    private static string tilesOutput = "AssetBundles/Building";  // 瓦片目录

    [MenuItem("Assets/Build AssetBundle (Auto)", false, 2000)]
    private static void BuildAssetBundleAuto()
    {
        string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(selectedPath) || !AssetDatabase.IsValidFolder(selectedPath))
        {
            Debug.LogError("请选择一个有效文件夹（Materials / Textures / 区县建筑目录）。");
            return;
        }

        string folderName = GetFileName(selectedPath);

        // 1. 打 Materials / Textures
        if (folderName == "Materials" || folderName == "Textures")
        {
            string bundleName = folderName.ToLower();
            string outputPath = Path.Combine(Application.dataPath, "../" + commonOutput);
            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

            BuildSingleFolderBundle(selectedPath, bundleName, outputPath);
            Debug.Log($"✅ {folderName} 打包完成 → {outputPath}");
            return;
        }

        // 2. 区县建筑目录
        string prefabsRoot = Path.Combine(selectedPath, "Prefabs");
        if (!Directory.Exists(prefabsRoot))
        {
            Debug.LogError("区县建筑目录内未找到 Prefabs 文件夹。");
            return;
        }

        string districtName = folderName; // 区县建筑名称
        string districtOutputPath = Path.Combine(Application.dataPath, "../" + tilesOutput, districtName);
        if (!Directory.Exists(districtOutputPath)) Directory.CreateDirectory(districtOutputPath);

        var tileIds = new List<string>();
        string[] tileDirs = Directory.GetDirectories(prefabsRoot);

        for (int idx = 0; idx < tileDirs.Length; idx++)
        {
            string tileDir = tileDirs[idx];
            string tileId = GetFileName(tileDir);
            tileIds.Add(tileId);
            string bundleName = tileId.ToLower();

            EditorUtility.DisplayProgressBar("打包瓦片", $"正在打包 {tileId} ({idx + 1}/{tileDirs.Length})", (float)idx / tileDirs.Length);

            string[] guids = AssetDatabase.FindAssets("", new[] { tileDir });
            List<string> assets = new List<string>();
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith(".meta") || Directory.Exists(assetPath)) continue;
                assets.Add(assetPath);
            }

            if (assets.Count > 0)
            {
                AssetBundleBuild build = new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetNames = assets.ToArray()
                };
                BuildPipeline.BuildAssetBundles(districtOutputPath,
                    new AssetBundleBuild[] { build },
                    BuildAssetBundleOptions.ChunkBasedCompression,
                    EditorUserBuildSettings.activeBuildTarget);
            }

            // 释放内存，避免堆积
            EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();

            Debug.Log($"✔ 瓦片 {tileId} 打包完成");
        }

        EditorUtility.ClearProgressBar();

        // 生成索引 JSON
        var index = new AbIndex
        {
            tiles = new TileEntry[tileIds.Count]
        };
        for (int i = 0; i < tileIds.Count; i++)
        {
            index.tiles[i] = new TileEntry { id = tileIds[i], bundle = tileIds[i].ToLower() };
        }

        string jsonPath = Path.Combine(districtOutputPath, "ab_index.json");
        File.WriteAllText(jsonPath, JsonUtility.ToJson(index, true));
        Debug.Log($"索引 JSON 已生成 → {jsonPath}");
    }

    [MenuItem("Assets/Build AssetBundle (Auto)", true)]
    private static bool Validate_BuildAssetBundleAuto()
    {
        var obj = Selection.activeObject;
        if (obj == null) return false;
        string path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return false;

        string name = GetFileName(path);
        if (name == "Materials" || name == "Textures") return true;

        string prefabsDir = Path.Combine(path, "Prefabs");
        return Directory.Exists(prefabsDir);
    }

    private static void BuildSingleFolderBundle(string folderPath, string bundleName, string outputPath)
    {
        string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });
        List<string> assets = new List<string>();
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (assetPath.EndsWith(".meta") || Directory.Exists(assetPath)) continue;
            assets.Add(assetPath);
        }

        if (assets.Count > 0)
        {
            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetNames = assets.ToArray()
            };
            BuildPipeline.BuildAssetBundles(outputPath,
                new AssetBundleBuild[] { build },
                BuildAssetBundleOptions.ChunkBasedCompression,
                EditorUserBuildSettings.activeBuildTarget);
        }
    }

    private static string GetFileName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        return Path.GetFileName(path);
    }

    // 命令行专用：固定从 Assets/ImportRoot/Building 打 AB 包
    public static void BuildAssetBundle_FixedBuilding_CLI()
    {
        try
        {
            Debug.Log("===== [TileBundleBuilder] CLI 打包开始 =====");
            string rootAssetPath = "Assets/ImportRoot/Building";
            if (!AssetDatabase.IsValidFolder(rootAssetPath))
            {
                Debug.LogError($"未找到固定目录：{rootAssetPath}");
                return;
            }

            // 先打公共资源（若存在）
            string commonOutAbs = Path.Combine(Application.dataPath, "../" + commonOutput);
            if (!Directory.Exists(commonOutAbs)) Directory.CreateDirectory(commonOutAbs);

            string materialsPath = $"{rootAssetPath}/Materials";
            if (AssetDatabase.IsValidFolder(materialsPath))
            {
                BuildSingleFolderBundle(materialsPath, "materials", commonOutAbs);
                Debug.Log($"✅ Materials 打包完成 → {commonOutAbs}");
            }

            string texturesPath = $"{rootAssetPath}/Textures";
            if (AssetDatabase.IsValidFolder(texturesPath))
            {
                BuildSingleFolderBundle(texturesPath, "textures", commonOutAbs);
                Debug.Log($"✅ Textures 打包完成 → {commonOutAbs}");
            }

            // 遍历区县目录（排除 Materials / Textures）
            string[] districtDirs = AssetDatabase.GetSubFolders(rootAssetPath);
            foreach (var districtAssetPath in districtDirs)
            {
                string districtName = GetFileName(districtAssetPath);
                if (districtName == "Materials" || districtName == "Textures") continue;

                string prefabsDir = $"{districtAssetPath}/Prefabs";
                if (!AssetDatabase.IsValidFolder(prefabsDir))
                {
                    Debug.LogWarning($"跳过：{districtName}（未找到 Prefabs 文件夹）");
                    continue;
                }

                string districtOutAbs = Path.Combine(Application.dataPath, "../" + tilesOutput, districtName);
                if (!Directory.Exists(districtOutAbs)) Directory.CreateDirectory(districtOutAbs);

                var tileIds = new List<string>();
                string[] tileDirs = AssetDatabase.GetSubFolders(prefabsDir);

                for (int i = 0; i < tileDirs.Length; i++)
                {
                    string tileDir = tileDirs[i];
                    string tileId = GetFileName(tileDir);
                    tileIds.Add(tileId);
                    string bundleName = tileId.ToLower();

                    try { EditorUtility.DisplayProgressBar($"打包区县：{districtName}", $"正在打包 {tileId} ({i + 1}/{tileDirs.Length})", (float)i / Math.Max(1, tileDirs.Length)); } catch { }

                    string[] guids = AssetDatabase.FindAssets("", new[] { tileDir });
                    List<string> assets = new List<string>();
                    foreach (string guid in guids)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (assetPath.EndsWith(".meta") || AssetDatabase.IsValidFolder(assetPath)) continue;
                        assets.Add(assetPath);
                    }

                    if (assets.Count > 0)
                    {
                        AssetBundleBuild build = new AssetBundleBuild
                        {
                            assetBundleName = bundleName,
                            assetNames = assets.ToArray()
                        };
                        BuildPipeline.BuildAssetBundles(
                            districtOutAbs,
                            new AssetBundleBuild[] { build },
                            BuildAssetBundleOptions.ChunkBasedCompression,
                            EditorUserBuildSettings.activeBuildTarget
                        );
                    }

                    EditorUtility.UnloadUnusedAssetsImmediate();
                    GC.Collect();
                    Debug.Log($"✔ 瓦片 {tileId} 打包完成 → {districtOutAbs}");
                }

                try { EditorUtility.ClearProgressBar(); } catch { }

                // 生成索引 JSON
                var index = new AbIndex { tiles = new TileEntry[tileIds.Count] };
                for (int k = 0; k < tileIds.Count; k++)
                {
                    index.tiles[k] = new TileEntry { id = tileIds[k], bundle = tileIds[k].ToLower() };
                }
                string jsonPath = Path.Combine(districtOutAbs, "ab_index.json");
                File.WriteAllText(jsonPath, JsonUtility.ToJson(index, true));
                Debug.Log($"📄 索引 JSON 已生成 → {jsonPath}");
            }

            Debug.Log("===== [TileBundleBuilder] CLI 打包完成 =====");
        }
        catch (Exception ex)
        {
            try { EditorUtility.ClearProgressBar(); } catch { }
            Debug.LogException(ex);
        }
    }


    [System.Serializable]
    private class AbIndex
    {
        public TileEntry[] tiles;
    }

    [System.Serializable]
    private class TileEntry
    {
        public string id;
        public string bundle;
    }
}
