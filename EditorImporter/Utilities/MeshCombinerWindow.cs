#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace GeoToolkit
{
    public class MeshCombinerWindow : EditorWindow
    {
        private List<Mesh> meshesToCombine = new List<Mesh>();
        private Vector2 scrollPos;
        private Vector2 meshListScrollPos; // 新增：Mesh列表滚动位置
        private string combinedMeshName = "CombinedMesh"; // 合并后的 Mesh 名称

        private const string saveFolderPath = "Assets/ImportRoot/CombinedMeshes"; // 保存文件夹
        private string savePath => Path.Combine(saveFolderPath, combinedMeshName + ".asset");

        [MenuItem("GeoToolkit/辅助工具/合并保存Mesh", false, priority = 202)]
        public static void ShowWindow()
        {
            GetWindow<MeshCombinerWindow>("Mesh Combiner");
        }

        private void OnGUI()
        {
            GUILayout.Label("Mesh Combiner", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 输入合并后 Mesh 名称
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Combined Mesh Name:", GUILayout.Width(150));
            combinedMeshName = EditorGUILayout.TextField(combinedMeshName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 新增：Mesh集合拖拽区域
            DrawMeshCollection();

            EditorGUILayout.Space();

            // 合并按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Combine Meshes", GUILayout.Width(120), GUILayout.Height(30)))
            {
                CombineMeshes();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制Mesh集合界面
        /// </summary>
        private void DrawMeshCollection()
        {
            EditorGUILayout.LabelField("Mesh集合:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("可以拖拽项目中的多个Mesh资源到此集合中，或者使用下方的添加按钮", MessageType.Info);

            // 拖拽区域
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "拖拽Mesh资源到这里", EditorStyles.helpBox);

            // 处理拖拽事件
            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        break;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is Mesh mesh)
                            {
                                if (!meshesToCombine.Contains(mesh))
                                {
                                    meshesToCombine.Add(mesh);
                                }
                            }
                            // 同时支持拖拽包含Mesh的GameObject
                            else if (draggedObject is GameObject gameObj)
                            {
                                MeshFilter meshFilter = gameObj.GetComponent<MeshFilter>();
                                if (meshFilter != null && meshFilter.sharedMesh != null)
                                {
                                    if (!meshesToCombine.Contains(meshFilter.sharedMesh))
                                    {
                                        meshesToCombine.Add(meshFilter.sharedMesh);
                                    }
                                }
                            }
                        }
                        Repaint();
                    }
                    Event.current.Use();
                    break;
            }

            // 显示集合中的Mesh列表
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Mesh列表 ({meshesToCombine.Count}):");

            if (meshesToCombine.Count == 0)
            {
                EditorGUILayout.HelpBox("集合为空，请拖拽Mesh资源到上方区域或使用添加按钮", MessageType.Info);
            }
            else
            {
                meshListScrollPos = EditorGUILayout.BeginScrollView(meshListScrollPos,
                    GUILayout.Height(Mathf.Min(meshesToCombine.Count * 20 + 10, 150)));

                for (int i = 0; i < meshesToCombine.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    // 显示Mesh引用和名称
                    meshesToCombine[i] = (Mesh)EditorGUILayout.ObjectField(
                        meshesToCombine[i], typeof(Mesh), false);

                    // 显示Mesh信息
                    if (meshesToCombine[i] != null)
                    {
                        GUILayout.Label($"Verts: {meshesToCombine[i].vertexCount}", GUILayout.Width(80));
                    }

                    // 删除按钮
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        meshesToCombine.RemoveAt(i);
                        Repaint();
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();

                // 操作按钮
                EditorGUILayout.BeginHorizontal();

                // 添加单个Mesh按钮
                if (GUILayout.Button("添加Mesh"))
                {
                    meshesToCombine.Add(null);
                }

                GUILayout.FlexibleSpace();

                // 清空所有按钮
                if (GUILayout.Button("清空列表", GUILayout.Width(80)))
                {
                    if (EditorUtility.DisplayDialog("确认清空", "确定要清空整个Mesh列表吗？", "确定", "取消"))
                    {
                        meshesToCombine.Clear();
                        Repaint();
                    }
                }

                EditorGUILayout.EndHorizontal();

                // 显示统计信息
                EditorGUILayout.Space();
                DisplayMeshStatistics();
            }
        }

        /// <summary>
        /// 显示Mesh统计信息
        /// </summary>
        private void DisplayMeshStatistics()
        {
            int totalVertices = 0;
            int totalMeshes = 0;
            int nullMeshes = 0;

            foreach (var mesh in meshesToCombine)
            {
                if (mesh == null)
                {
                    nullMeshes++;
                    continue;
                }
                totalVertices += mesh.vertexCount;
                totalMeshes++;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("统计信息:", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"有效Mesh数量: {totalMeshes}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"空引用Mesh: {nullMeshes}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"总顶点数: {totalVertices}", EditorStyles.miniLabel);

            if (nullMeshes > 0)
            {
                EditorGUILayout.HelpBox($"有 {nullMeshes} 个空引用Mesh，请检查或移除", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void CombineMeshes()
        {
            // 过滤掉空引用
            List<Mesh> validMeshes = new List<Mesh>();
            foreach (var mesh in meshesToCombine)
            {
                if (mesh != null)
                {
                    validMeshes.Add(mesh);
                }
            }

            if (validMeshes.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "没有有效的Mesh可以合并！请添加至少一个有效的Mesh。", "确定");
                return;
            }

            if (validMeshes.Count < meshesToCombine.Count)
            {
                if (!EditorUtility.DisplayDialog("警告",
                    $"检测到 {meshesToCombine.Count - validMeshes.Count} 个空引用Mesh，是否只合并有效的Mesh？",
                    "继续合并", "取消"))
                {
                    return;
                }
            }

            List<CombineInstance> combineInstances = new List<CombineInstance>();
            foreach (var mesh in validMeshes)
            {
                CombineInstance ci = new CombineInstance
                {
                    mesh = mesh,
                    transform = Matrix4x4.identity
                };
                combineInstances.Add(ci);
            }

            Mesh combinedMesh = new Mesh
            {
                name = combinedMeshName
            };
            combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

            // 确保文件夹存在
            if (!AssetDatabase.IsValidFolder(saveFolderPath))
            {
                string parentFolder = Path.GetDirectoryName(saveFolderPath);
                string folderName = Path.GetFileName(saveFolderPath);

                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    Directory.CreateDirectory(parentFolder);
                }

                AssetDatabase.CreateFolder(parentFolder, folderName);
            }

            // 确保文件名唯一
            string finalSavePath = savePath;
            int counter = 1;
            while (File.Exists(finalSavePath))
            {
                finalSavePath = Path.Combine(saveFolderPath, $"{combinedMeshName}_{counter}.asset");
                counter++;
            }

            AssetDatabase.CreateAsset(combinedMesh, finalSavePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 显示成功对话框
            if (EditorUtility.DisplayDialog("合并成功",
                $"Mesh合并完成并保存到:\n{finalSavePath}\n\n是否在Project窗口中显示该文件？", "显示", "关闭"))
            {
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(finalSavePath);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }

            Debug.Log($"Mesh combined and saved at: {finalSavePath}");
        }
    }
}
#endif