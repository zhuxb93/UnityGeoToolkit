#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GeoToolkit
{
    public class GeoImportWindow : EditorWindow
    {
        private string inputText = "";
        private string label = "请输入内容：";
        private Action<string, SelectType> onConfirmCallback;
        private ImportType importType;

        private static readonly string[] CheckPaths =
        {
            GeoToolkitEditor.TerrainSavePath,
            GeoToolkitEditor.GeoJsonSavePath,
            GeoToolkitEditor.AutoModelSavePath,
            GeoToolkitEditor.CustomModelSavePath,
            GeoToolkitEditor.WindySavePath,
        };

        private static readonly char[] InvalidChars =
            Path.GetInvalidFileNameChars();

        public static void ShowWindow(Action<string, SelectType> onConfirm, ImportType importType)
        {
            GeoImportWindow window = ScriptableObject.CreateInstance<GeoImportWindow>();
            window.titleContent = new GUIContent("导入资源中");
            window.label = "给你导入的素材起个名吧，重复就会覆盖啊";
            window.onConfirmCallback = onConfirm;
            window.importType = importType;
            window.inputText = GenerateUniqueCode();
            window.position = new Rect(Screen.width / 2, Screen.height / 2, 320, 110);
            window.ShowUtility();
        }

        void OnGUI()
        {
            GUILayout.Label(label, EditorStyles.boldLabel);
            inputText = EditorGUILayout.TextField("输入内容：", inputText);

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("选择文件"))
            {
                if (!ValidateInput())
                    return;

                onConfirmCallback?.Invoke(inputText.Trim(), SelectType.File);
                Close();
            }

            if (GUILayout.Button("选择文件夹"))
            {
                if (!ValidateInput())
                    return;

                onConfirmCallback?.Invoke(inputText.Trim(), SelectType.Folder);
                Close();
            }

            if (GUILayout.Button("取消"))
            {
                Close();
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 名称合法性 + 重名校验
        /// </summary>
        private bool ValidateInput()
        {
            string name = inputText?.Trim();

            // 1. 非空校验
            if (string.IsNullOrWhiteSpace(name))
            {
                EditorUtility.DisplayDialog(
                    "名称不能为空",
                    "请输入有效的数据名称（不能是空字符串或空格）",
                    "确定"
                );
                return false;
            }

            // 2. 非法字符校验
            if (name.IndexOfAny(InvalidChars) >= 0)
            {
                EditorUtility.DisplayDialog(
                    "名称包含非法字符",
                    $"名称不能包含以下字符：\n{string.Join(" ", InvalidChars)}",
                    "确定"
                );
                return false;
            }

            // 3. 重名文件夹检测（仅检测当前 ImportType 对应目录）
            string checkRoot = importType switch
            {
                ImportType.Terrain => GeoToolkitEditor.TerrainSavePath,
                ImportType.GeoJSON => GeoToolkitEditor.GeoJsonSavePath,
                ImportType.AutoModel => GeoToolkitEditor.AutoModelSavePath,
                ImportType.CustomModel => GeoToolkitEditor.CustomModelSavePath,
                ImportType.Windy => GeoToolkitEditor.WindySavePath,
                _ => null
            };

            if (!string.IsNullOrEmpty(checkRoot))
            {
                string fullPath = Path.Combine(checkRoot, name);

                if (AssetDatabase.IsValidFolder(fullPath))
                {
                    bool continueImport = EditorUtility.DisplayDialog(
                        "发现同名数据",
                        $"在以下路径已存在同名文件夹：\n\n{fullPath}\n\n继续将会覆盖或复用该目录，是否继续？",
                        "继续",
                        "取消"
                    );

                    if (!continueImport)
                        return false;
                }
            }


            return true;
        }

        public static string GenerateUniqueCode(int length = 16)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            System.Random random = new System.Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

    public enum SelectType
    {
        File,
        Folder
    }

    public enum ImportType
    {
        Terrain,
        GeoJSON,
        AutoModel,
        CustomModel,
        Windy
    }
}
#endif
