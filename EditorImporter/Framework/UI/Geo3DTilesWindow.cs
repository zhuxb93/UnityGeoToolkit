#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GeoToolkit
{
    public class Geo3DTilesWindow : EditorWindow
    {
        private string inputText = "";
        private string label = "请输入3DTiles地址：";
        private Action<string> onConfirmCallback;

        public static void ShowWindow(Action<string> onConfirm)
        {
            Geo3DTilesWindow window = ScriptableObject.CreateInstance<Geo3DTilesWindow>();
            window.titleContent = new GUIContent("导入资源中");
            window.label = "";
            window.onConfirmCallback = onConfirm;
            window.inputText = string.Empty;
            window.position = new Rect(Screen.width / 2, Screen.height / 2, 200, 100);
            window.ShowUtility();
        }

        void OnGUI()
        {
            GUILayout.Label(label, EditorStyles.boldLabel);
            inputText = EditorGUILayout.TextField("请输入3DTiles地址：", inputText);

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("确定"))
            {
                onConfirmCallback?.Invoke(inputText);
                this.Close();
            }

            if (GUILayout.Button("取消"))
            {
                this.Close();
            }

            GUILayout.EndHorizontal();
        }

       
    }
}

#endif