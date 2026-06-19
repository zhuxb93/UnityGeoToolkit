#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using Parabox.CSG;
using static Parabox.CSG.CSG;

namespace GeoToolkit
{
    public class MeshBooleanWindow : EditorWindow
    {
        //private enum BooleanOperation
        //{
        //    Union,
        //    Subtract,
        //    Intersect
        //}

        private Mesh meshA;
        private Mesh meshB;
        private BooleanOp operation = BooleanOp.Union;

        private string resultMeshName = "BooleanResultMesh";

        private const string saveFolderPath = "Assets/ImportRoot/BooleanMeshes";
        private string SavePath => Path.Combine(saveFolderPath, resultMeshName + ".asset");

        [MenuItem("GeoToolkit/辅助工具/Mesh Boolean 运算", false, priority = 202)]
        public static void ShowWindow()
        {
            GetWindow<MeshBooleanWindow>("Mesh Boolean");
        }

        private void OnGUI()
        {
            GUILayout.Label("Mesh Boolean 运算", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawOperationSelector();
            EditorGUILayout.Space();

            DrawMeshInputArea();
            EditorGUILayout.Space();

            DrawResultSettings();
            EditorGUILayout.Space();

            DrawExecuteButton();
        }

        #region UI Sections

        private void DrawOperationSelector()
        {
            EditorGUILayout.LabelField("运算类型", EditorStyles.boldLabel);

            operation = (BooleanOp)GUILayout.Toolbar(
                (int)operation,
                new[] { "Intersection", "Union", "Subtraction" }
            );

            EditorGUILayout.HelpBox(GetOperationDescription(), MessageType.Info);
        }

        private void DrawMeshInputArea()
        {
            EditorGUILayout.LabelField("输入 Mesh", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");

            meshA = DrawMeshField("Mesh A", meshA);
            meshB = DrawMeshField("Mesh B", meshB);

            EditorGUILayout.Space();
            DrawDragDropArea();

            EditorGUILayout.Space();
            DrawMeshInfo(meshA, "A");
            DrawMeshInfo(meshB, "B");

            EditorGUILayout.EndVertical();
        }

        private void DrawResultSettings()
        {
            EditorGUILayout.LabelField("结果设置", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("结果 Mesh 名称", GUILayout.Width(120));
            resultMeshName = EditorGUILayout.TextField(resultMeshName);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawExecuteButton()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = meshA != null && meshB != null;

            if (GUILayout.Button("执行 Boolean 运算", GUILayout.Width(160), GUILayout.Height(36)))
            {
                ExecuteBoolean();
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Helpers

        private Mesh DrawMeshField(string label, Mesh mesh)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(60));
            mesh = (Mesh)EditorGUILayout.ObjectField(mesh, typeof(Mesh), false);
            EditorGUILayout.EndHorizontal();
            return mesh;
        }

        private void DrawDragDropArea()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "拖拽 Mesh / GameObject 到此区域\n（优先填充 A → B）", EditorStyles.helpBox);

            Event evt = Event.current;
            if (!dropArea.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (Object obj in DragAndDrop.objectReferences)
                    {
                        Mesh m = null;

                        if (obj is Mesh mesh)
                            m = mesh;
                        else if (obj is GameObject go)
                        {
                            var mf = go.GetComponent<MeshFilter>();
                            if (mf != null)
                                m = mf.sharedMesh;
                        }

                        if (m == null) continue;

                        if (meshA == null) meshA = m;
                        else if (meshB == null) meshB = m;
                    }

                    Repaint();
                }
                evt.Use();
            }
        }

        private void DrawMeshInfo(Mesh mesh, string label)
        {
            if (mesh == null) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Mesh {label} 信息", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Vertices: {mesh.vertexCount}");
            EditorGUILayout.LabelField($"Triangles: {mesh.triangles.Length / 3}");
            EditorGUILayout.EndVertical();
        }

        private string GetOperationDescription()
        {
            switch (operation)
            {
                case BooleanOp.Union:
                    return "Union：合并 A 和 B，生成一个整体 Mesh";
                case BooleanOp.Subtraction:
                    return "Subtraction：从 A 中减去 B（A - B）";
                case BooleanOp.Intersection:
                    return "Intersection：仅保留 A 与 B 的重叠部分";
                default:
                    return string.Empty;
            }
        }

        #endregion

        #region Core Logic

        private void ExecuteBoolean()
        {
            if (meshA == null || meshB == null)
            {
                EditorUtility.DisplayDialog("错误", "请同时指定 Mesh A 和 Mesh B", "确定");
                return;
            }

            Mesh result = null;

            try
            {
                result = Perform(operation, meshA, meshB).mesh;
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
                EditorUtility.DisplayDialog("Boolean 运算失败", e.Message, "确定");
                return;
            }

            if (result == null)
            {
                EditorUtility.DisplayDialog("失败", "Boolean 运算未生成有效 Mesh", "确定");
                return;
            }

            SaveResultMesh(result);
        }

        private void SaveResultMesh(Mesh mesh)
        {
            if (!AssetDatabase.IsValidFolder(saveFolderPath))
            {
                string parent = Path.GetDirectoryName(saveFolderPath);
                string folder = Path.GetFileName(saveFolderPath);
                if (!AssetDatabase.IsValidFolder(parent))
                    Directory.CreateDirectory(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }

            string finalPath = SavePath;
            int index = 1;
            while (File.Exists(finalPath))
            {
                finalPath = Path.Combine(saveFolderPath, $"{resultMeshName}_{index}.asset");
                index++;
            }

            mesh.name = resultMeshName;
            AssetDatabase.CreateAsset(mesh, finalPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(finalPath);
            EditorGUIUtility.PingObject(Selection.activeObject);

            Debug.Log($"Mesh Boolean 结果已保存: {finalPath}");
        }

        #endregion
    }
}
#endif
