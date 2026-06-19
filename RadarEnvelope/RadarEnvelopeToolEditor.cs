#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using GeoToolkit;
using GeoToolkit.RadarEnvelope;
public class RadarEnvelopeToolEditor : EditorWindow
{
    public static RadarEnvelopeToolEditor window;

    public Vector3 radarPosition = Vector3.zero;

    public float radarRadius = 0;

    private float ratio = 1;

    public const string EditorRootPath = "Packages/com.geoearth.platformsdk/Runtime/RadarRangeTool";

    public GameObject defaultParent;
    public GameObject quipmentParent;
    private int _selectedOptionIndex = 0;
    private string[] _dropdownOptions = new[] { "侦测", "反制", "打击"};
    private string[] names = new string[]{ "Detect", "Counter", "Strike"};

    public AdjunctEdit adjunctEdit;
    [MenuItem("GeoToolkit/行业组件/反无设备包络/控制面板", false, priority = 300)]
    public static void ShowWindow()
    {
        window = GetWindow<RadarEnvelopeToolEditor>("控制面板");
        // window.minSize = new Vector2(260, 200);
        window.position = new Rect(Screen.width / 1.2f, Screen.height / 1.5f, 360, 300);

        string configPath = $"Assets/GeoToolkit/Config/GeoPlatformConfig.asset";
        GeoPlatformConfig platformConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<GeoPlatformConfig>(configPath);
        if (platformConfig != null)
        {
            window.ratio = platformConfig.GetScale();
        }
        if (SceneView.lastActiveSceneView != null)
        {
            Camera sceneCam = SceneView.lastActiveSceneView.camera;
            Vector3 camPos = sceneCam.transform.position;
            Vector3 pivot = SceneView.lastActiveSceneView.pivot;  
            Vector3 forward = sceneCam.transform.forward;
            Ray ray = new Ray(camPos, forward);
            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, LayerMask.GetMask(RadarEnvelopeTool.GetRayLayer())))
            {
                window.radarPosition = hit.point;
            }
            else
            {
                window.radarPosition = pivot;
            }
        }
        window.radarRadius = 500;
        EnvelopeTransparentController controller = GameObject.FindFirstObjectByType<EnvelopeTransparentController>();
        if (controller == null)
        {
            GameObject go = new GameObject("EnvelopeTransparentController");
            controller = go.AddComponent<EnvelopeTransparentController>(); 
        }
        window.defaultParent = controller.gameObject;
        AdjunctEdit adjunct = GameObject.FindFirstObjectByType<AdjunctEdit>();
        if(adjunct == null) 
            adjunct =controller.gameObject.AddComponent<AdjunctEdit>();
        adjunct.enabled = true;
        window.adjunctEdit = adjunct;
        window.adjunctEdit.center = window.radarPosition;
        window.adjunctEdit.showRadius = window.radarRadius * window.ratio;

    }
    
    public void OnDisable()
    {
        // Debug.Log("OnDisable");
        if(window.adjunctEdit != null) 
            window.adjunctEdit.enabled = false;
    }

    public void OnGUI()
    {
        #region 1. 基础自定义下拉框（字符串选项）
        GUILayout.Label("类型", EditorStyles.boldLabel);
        // 核心：Popup(标签, 选中索引, 选项列表) → 返回新的选中索引
        _selectedOptionIndex = EditorGUILayout.Popup(
            label: "选择选项：",
            selectedIndex: _selectedOptionIndex,
            displayedOptions: _dropdownOptions
        );
        // 显示选中结果
        GUILayout.Label($"当前选中：{_dropdownOptions[_selectedOptionIndex]}", EditorStyles.miniLabel);
        GUILayout.Space(15);
        #endregion

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("设备位置", EditorStyles.boldLabel);
        radarPosition = EditorGUILayout.Vector3Field("位置", radarPosition);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("作用范围", EditorStyles.boldLabel);
        radarRadius = EditorGUILayout.FloatField("半径", radarRadius);
        EditorGUILayout.Space();

        if (GUILayout.Button("从场景选择雷达位置"))
        {
            if (Selection.activeTransform != null)
            {
                radarPosition = Selection.activeTransform.position;
                quipmentParent = Selection.activeGameObject;
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "请先在场景中选择一个对象作为雷达位置", "确定");
            }
        }
        if(adjunctEdit != null)
        {
            adjunctEdit.center = radarPosition;
            adjunctEdit.showRadius = radarRadius * ratio;
        }
        EditorGUILayout.Space(20);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("生产"))
        {
            if (_selectedOptionIndex == 0)
            {
                Material heimisMat = AssetDatabase.LoadAssetAtPath<Material>($"{EditorRootPath}/Materials/Hemisphere.mat");
                Material heimisScanMat = AssetDatabase.LoadAssetAtPath<Material>($"{EditorRootPath}/Materials/HemisphereScan.mat");
                GameObject decalParfab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EditorRootPath}/Prefabs/DecalRangle.prefab");
                RadarEnvelopeTool.CreateHemisphere((int)RadarEnvelopeTool.GetTimestampID(), names[_selectedOptionIndex], radarPosition, radarRadius, ratio, heimisMat, heimisScanMat, quipmentParent == null ? window.defaultParent : quipmentParent, decalParfab);
            }
            else if(_selectedOptionIndex == 1)
            {
                Material counterMat = AssetDatabase.LoadAssetAtPath<Material>($"{EditorRootPath}/Materials/Donut.mat");
                RadarEnvelopeTool.CreateDonut((int)RadarEnvelopeTool.GetTimestampID(), names[_selectedOptionIndex], radarPosition, radarRadius, ratio, counterMat, quipmentParent == null ? window.defaultParent : quipmentParent);
            }
            else if (_selectedOptionIndex == 2)
            {
                Material laserMat = AssetDatabase.LoadAssetAtPath<Material>($"{EditorRootPath}/Materials/Laser.mat");
                RadarEnvelopeTool.CreateEllipsoid((int)RadarEnvelopeTool.GetTimestampID(), names[_selectedOptionIndex], radarPosition, radarRadius, ratio, laserMat, quipmentParent == null ? window.defaultParent : quipmentParent);
            }
        }
        if (GUILayout.Button("关闭"))
        {
            this.Close();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }
}
#endif