using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GeoToolkit.Smooth
{
    public enum ComputeMode
    {
        CPU = 0,
        GPU = 1
    }

    public class TerrainSmoothWindow : EditorWindow
    {
        // =========================
        // ⭐ 核心修改：支持多对象
        // =========================
        public List<GameObject> targetTerrainObjects = new List<GameObject>();

        private Vector2 scrollPosition;
        private bool showDetailedInfo = false;

        private int maxBatchSize = 512;

        private int crossSmoothRadius = 16;
        private int crossSmoothIterations = 1;
        private float crossSmoothStrength = 1.0f;

        private bool enableMinHeightSkip = false;
        private float minHeightThreshold = 0f;
        private bool enableMaxHeightSkip = false;
        private float maxHeightThreshold = 1000f;

        private ComputeMode computeMode = ComputeMode.GPU;

        private TerrainSmoothProcessor processor;
        private TerrainSmoothGPUProcessor gpuProcessor;

        public static void ShowWindow()
        {
            var window = GetWindow<TerrainSmoothWindow>("地形平滑工具");
            window.minSize = new Vector2(450, 500);

            // 支持从 Selection 直接多选带入
            window.targetTerrainObjects.Clear();
            //foreach (var obj in Selection.gameObjects)
            //{
            //    if (!window.targetTerrainObjects.Contains(obj))
            //        window.targetTerrainObjects.Add(obj);
            //}

            window.Show();
        }

        private void OnEnable()
        {
            processor = new TerrainSmoothProcessor(
                crossSmoothRadius, crossSmoothIterations, crossSmoothStrength,
                maxBatchSize, enableMinHeightSkip, minHeightThreshold,
                enableMaxHeightSkip, maxHeightThreshold);

            gpuProcessor = new TerrainSmoothGPUProcessor(
                crossSmoothRadius, crossSmoothIterations, crossSmoothStrength,
                maxBatchSize, enableMinHeightSkip, minHeightThreshold,
                enableMaxHeightSkip, maxHeightThreshold);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("地形平滑工具", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // =========================
            // ⭐ 多对象拖拽区域
            // =========================
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("拖拽一个或多个地形 Root GameObject：", EditorStyles.boldLabel);

            var dropRect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "将 GameObject 拖到这里（支持多选）", EditorStyles.helpBox);

            HandleDragAndDrop(dropRect);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // =========================
            // ⭐ 已选择对象列表
            // =========================
            if (targetTerrainObjects.Count > 0)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"已选择对象（{targetTerrainObjects.Count}）：", EditorStyles.boldLabel);

                for (int i = 0; i < targetTerrainObjects.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(targetTerrainObjects[i], typeof(GameObject), true);

                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        targetTerrainObjects.RemoveAt(i);
                        GUIUtility.ExitGUI();
                    }

                    EditorGUILayout.EndHorizontal();
                }

                showDetailedInfo = EditorGUILayout.Foldout(showDetailedInfo, "显示地形详细信息");
                if (showDetailedInfo)
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
                    foreach (var go in targetTerrainObjects)
                    {
                        if (go != null)
                            DisplayTerrainInfo(go.transform, 0);
                    }
                    EditorGUILayout.EndScrollView();
                }

                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("请拖入至少一个 GameObject", MessageType.Info);
            }

            EditorGUILayout.Space(10);
            DrawCrossTerrainSmoothParameters();
            EditorGUILayout.Space(10);
            DrawFunctionButtons();

            if (GUILayout.Button("清除选择", GUILayout.Height(30)))
            {
                targetTerrainObjects.Clear();
            }
        }

        // =========================
        // ⭐ 拖拽处理（核心）
        // =========================
        private void HandleDragAndDrop(Rect rect)
        {
            Event evt = Event.current;

            if (!rect.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    // ⭐ 关键：每次拖拽先清空
                    targetTerrainObjects.Clear();

                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is GameObject go)
                        {
                            targetTerrainObjects.Add(go);
                        }
                    }

                    // 防止同一帧多次执行
                    GUI.changed = true;
                }

                evt.Use();
            }
        }


        private void DrawCrossTerrainSmoothParameters()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("地形平滑参数设置", EditorStyles.boldLabel);

            computeMode = (ComputeMode)EditorGUILayout.EnumPopup("计算模式:", computeMode);

            crossSmoothRadius = EditorGUILayout.IntSlider("地形平滑半径:", crossSmoothRadius, 1, 256);
            crossSmoothIterations = EditorGUILayout.IntSlider("地形平滑迭代次数:", crossSmoothIterations, 1, 5);
            crossSmoothStrength = EditorGUILayout.Slider("地形平滑强度:", crossSmoothStrength, 0.1f, 1.0f);
            maxBatchSize = EditorGUILayout.IntSlider("批处理大小:", maxBatchSize, 4, 1024);

            enableMinHeightSkip = EditorGUILayout.Toggle("启用最小高度跳过:", enableMinHeightSkip);
            if (enableMinHeightSkip)
                minHeightThreshold = EditorGUILayout.FloatField("最小高度阈值:", minHeightThreshold);

            enableMaxHeightSkip = EditorGUILayout.Toggle("启用最大高度跳过:", enableMaxHeightSkip);
            if (enableMaxHeightSkip)
                maxHeightThreshold = EditorGUILayout.FloatField("最大高度阈值:", maxHeightThreshold);

            EditorGUILayout.EndVertical();
        }

        private void DrawFunctionButtons()
        {
            EditorGUI.BeginDisabledGroup(targetTerrainObjects.Count == 0);

            if (GUILayout.Button("平滑所有地形", GUILayout.Height(40)))
            {
                CrossSmoothAllTerrains();
            }

            EditorGUI.EndDisabledGroup();
        }

        private void CollectTerrainsRecursively(Transform parent, List<Terrain> terrains)
        {
            if (parent == null) return;

            var terrain = parent.GetComponent<Terrain>();
            if (terrain != null && terrain.terrainData != null)
                terrains.Add(terrain);

            for (int i = 0; i < parent.childCount; i++)
                CollectTerrainsRecursively(parent.GetChild(i), terrains);
        }

        private void DisplayTerrainInfo(Transform parent, int depth)
        {
            EditorGUILayout.LabelField(new string(' ', depth * 4) + parent.name);

            for (int i = 0; i < parent.childCount; i++)
                DisplayTerrainInfo(parent.GetChild(i), depth + 1);
        }

        private void CrossSmoothAllTerrains()
        {
            EditorApplication.delayCall += () => CrossSmoothAllTerrainsAsync();
        }

        private async void CrossSmoothAllTerrainsAsync()
        {
            List<Terrain> terrainsToProcess = new List<Terrain>();

            foreach (var root in targetTerrainObjects)
            {
                if (root != null)
                    CollectTerrainsRecursively(root.transform, terrainsToProcess);
            }

            //if (terrainsToProcess.Count < 2)
            //{
            //    EditorUtility.DisplayDialog("提示", "地形数量不足，至少需要 2 个 Terrain", "确定");
            //    return;
            //}

            int processedCount = computeMode == ComputeMode.GPU
                ? await gpuProcessor.SmoothTerrainsAsync(terrainsToProcess)
                : await processor.SmoothTerrainsAsync(terrainsToProcess);

            EditorUtility.DisplayDialog("完成", $"地形平滑完成，共处理 {processedCount} 个地形", "确定");
        }
    }

    public static class TerrainSmoothManager
    {
        [MenuItem("GeoToolkit/地形工具/地形平滑工具", false, 102)]
        public static void OpenTerrainSmoothWindow()
        {
            TerrainSmoothWindow.ShowWindow();
        }
    }
}
