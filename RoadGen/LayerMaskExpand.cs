using UnityEditor;
using UnityEngine;

namespace GeoToolkit.Road
{

    public static class LayerMaskExpand
    {
        /// <summary>
        /// Editor ״̬ �ɼ������ ���� Layer����Editor�� ������������Layer
        /// </summary>
        /// <param name="go"></param>
        /// <param name="layerName"></param>
        public static void CheckAddLayer(this GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer == -1)
            {
                layer = AutoAddLayer(layerName);
            }
            go.layer = layer;
        }

        public static int AutoAddLayer(string layer)
        {
#if UNITY_EDITOR
            if (!HasThisLayer(layer))
            {
                SerializedObject tagMagager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Tagmanager.asset"));
                SerializedProperty it = tagMagager.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.name.Equals("layers"))
                    {
                        for (int i = 0; i < it.arraySize; i++)
                        {
                            if (i <= 7)
                            {
                                continue;
                            }
                            SerializedProperty sp = it.GetArrayElementAtIndex(i);
                            if (string.IsNullOrEmpty(sp.stringValue))
                            {
                                sp.stringValue = layer;
                                tagMagager.ApplyModifiedProperties();
                                return LayerMask.NameToLayer(layer);
                            }
                        }
                    }
                }
                return 0;
            }
            else
            {
                return LayerMask.NameToLayer(layer);
            }
#else
        return LayerMask.NameToLayer(layer);
#endif
        }

        static bool HasThisLayer(string layer)
        {
#if UNITY_EDITOR
            for (int i = 0; i < UnityEditorInternal.InternalEditorUtility.layers.Length; i++)
            {
                if (UnityEditorInternal.InternalEditorUtility.layers[i].Equals(layer))
                {
                    return true;
                }
            }
#endif
            return false;
        }

        /// <summary>
        /// Editor ״̬ �ɼ������ Tag ��ǩ����Editor�� ������������Tag
        /// </summary>
        /// <param name="go"></param>
        /// <param name="tagName"></param>
        public static void CheckAddTag(this GameObject go, string tagName)
        {
#if UNITY_EDITOR
            if (!HasThisTag(tagName))
            {
                AddTag(tagName);
            }
#endif
            go.tag = tagName;
        }

        private static bool HasThisTag(string tagName)
        {
#if UNITY_EDITOR
            for (int i = 0; i < UnityEditorInternal.InternalEditorUtility.tags.Length; i++)
            {
                if (UnityEditorInternal.InternalEditorUtility.tags[i].Equals(tagName))
                {
                    return true;
                }
            }
#endif
            return false;
        }

        private static void AddTag(string tagName)
        {
#if UNITY_EDITOR
            bool isEmptyItem = false;
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                SerializedProperty sp = tagsProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    isEmptyItem = true;
                    sp.stringValue = tagName;
                    tagManager.ApplyModifiedProperties();
                    break;
                }
            }
            if (!isEmptyItem)
            {
                int oriSize = tagsProp.arraySize;
                tagsProp.InsertArrayElementAtIndex(oriSize);
                SerializedProperty sp = tagsProp.GetArrayElementAtIndex(oriSize);
                sp.stringValue = tagName;
                tagManager.ApplyModifiedProperties();
            }
#endif
        }
    }
}