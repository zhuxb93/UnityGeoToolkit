using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GeoToolkit.SeamFix
{
    /// <summary>
    /// 地形缝隙修复工具窗口
    /// </summary>
    public class TerrainSeamFixWindow : EditorWindow
    {
        // 公共字段
        public GameObject targetTerrainObject;

        // 私有UI控制字段
        private Vector2 scrollPosition;
        private bool showDetailedInfo = false;
        private bool showSeamInfo = false;
        private int detectedSeamCount = 0;
        private List<TerrainSeamInfo> seamInfoList = new List<TerrainSeamInfo>();

        // 边缘检测设置字段
        private float seamThreshold = 0.01f; // 高度差阈值，超过此值认为有缝隙（降低到1cm以避免浮点精度问题）
        private bool fixVerticalSeams = true; // 修复垂直边缘
        private bool fixHorizontalSeams = true; // 修复水平边缘
        private bool fixCornerPoints = true; // 修复4个瓦片共用的角点
        private int blendWidth = 3; // 边缘融合宽度（像素数）
        private AnimationCurve blendCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 融合曲线
        


        // 算法组件
        private TerrainSeamDetector detector;
        private TerrainSeamFixer fixer;
        private TerrainCornerFixer cornerFixer;

        /// <summary>
        /// 显示窗口的静态方法
        /// </summary>
        public static void ShowWindow()
        {
            TerrainSeamFixWindow window = EditorWindow.GetWindow<TerrainSeamFixWindow>("地形缝隙修复工具");
            window.targetTerrainObject = Selection.activeGameObject;
            window.minSize = new Vector2(450, 400);
            window.Show();
        }

        /// <summary>
        /// 主要的GUI绘制方法
        /// </summary>
        void OnGUI()
        {
            EditorGUILayout.Space(10);

            // 标题
            EditorGUILayout.LabelField("地形缝隙修复工具", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // GameObject拖拽区域
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("选择要处理的地形根对象:", EditorStyles.boldLabel);

            GameObject newTarget = (GameObject)EditorGUILayout.ObjectField("地形对象", targetTerrainObject, typeof(GameObject), true);

            // 如果拖入了新的GameObject，更新引用
            if (newTarget != targetTerrainObject)
            {
                targetTerrainObject = newTarget;
                seamInfoList.Clear(); // 清空之前的检测结果
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 缝隙检测设置
            DrawSeamDetectionSettings();

            EditorGUILayout.Space(10);

            // 显示当前选中的GameObject信息
            if (targetTerrainObject != null)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("当前选中对象信息:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"名称: {targetTerrainObject.name}");
                EditorGUILayout.LabelField($"子对象数量: {targetTerrainObject.transform.childCount}");

                showDetailedInfo = EditorGUILayout.Foldout(showDetailedInfo, "显示地形详细信息");
                if (showDetailedInfo)
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
                    DisplayTerrainInfo(targetTerrainObject.transform, 0);
                    EditorGUILayout.EndScrollView();
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // 显示缝隙信息
                if (seamInfoList.Count > 0)
                {
                    EditorGUILayout.BeginVertical("box");
                    showSeamInfo = EditorGUILayout.Foldout(showSeamInfo, $"检测到的缝隙信息 (共{seamInfoList.Count}处)");
                    if (showSeamInfo)
                    {
                        foreach (var seam in seamInfoList)
                        {
                            EditorGUILayout.LabelField($"- {seam.terrain1.name} <-> {seam.terrain2.name}: 最大高度差 {seam.maxHeightDifference:F3}m");
                        }
                    }
                    EditorGUILayout.EndVertical();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("请拖入一个包含地形瓦片的根GameObject", MessageType.Info);
            }

            EditorGUILayout.Space(10);

            // 功能按钮区域
            DrawFunctionButtons();

            EditorGUILayout.Space(5);

            // 清除按钮
            if (GUILayout.Button("清除选择", GUILayout.Height(30)))
            {
                targetTerrainObject = null;
                seamInfoList.Clear();
            }
        }

        /// <summary>
        /// 绘制缝隙检测设置
        /// </summary>
        private void DrawSeamDetectionSettings()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("缝隙检测与修复设置:", EditorStyles.boldLabel);

            seamThreshold = EditorGUILayout.FloatField("缝隙阈值(米):", seamThreshold);
            EditorGUILayout.HelpBox("超过此高度差将被认为是缝隙", MessageType.Info);

            EditorGUILayout.Space(5);

            // 动态计算最大融合宽度
            int maxBlendWidth = GetMaxBlendWidth();
            blendWidth = EditorGUILayout.IntSlider("边缘融合宽度:", blendWidth, 1, maxBlendWidth);

            if (maxBlendWidth > 10)
            {
                EditorGUILayout.HelpBox($"在边缘{blendWidth}个像素范围内进行渐变融合 (最大: {maxBlendWidth}像素)", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"在边缘{blendWidth}个像素范围内进行渐变融合", MessageType.Info);
            }

            EditorGUILayout.Space(5);
            fixVerticalSeams = EditorGUILayout.Toggle("修复垂直边缘:", fixVerticalSeams);
            fixHorizontalSeams = EditorGUILayout.Toggle("修复水平边缘:", fixHorizontalSeams);
            fixCornerPoints = EditorGUILayout.Toggle("修复角点(4瓦片共用):", fixCornerPoints);

            EditorGUILayout.Space(5);



            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 获取最大融合宽度（基于当前地形的高度图分辨率）
        /// </summary>
        private int GetMaxBlendWidth()
        {
            if (targetTerrainObject == null)
            {
                return 10; // 默认最大值
            }

            // 收集所有地形
            List<Terrain> terrains = new List<Terrain>();
            TerrainUtils.CollectTerrainsRecursively(targetTerrainObject.transform, terrains);

            if (terrains.Count == 0)
            {
                return 10; // 默认最大值
            }

            // 找到最小的高度图分辨率
            int minResolution = int.MaxValue;
            foreach (var terrain in terrains)
            {
                if (terrain?.terrainData != null)
                {
                    int resolution = terrain.terrainData.heightmapResolution;
                    minResolution = Mathf.Min(minResolution, resolution);
                }
            }

            if (minResolution == int.MaxValue)
            {
                return 10; // 默认最大值
            }

            // 最大融合宽度为最小分辨率的一半，但至少是10
            int maxWidth = minResolution / 2;
            return Mathf.Max(10, maxWidth);
        }


        /// <summary>
        /// 绘制功能按钮
        /// </summary>
        private void DrawFunctionButtons()
        {
            EditorGUI.BeginDisabledGroup(targetTerrainObject == null);

            // 检测缝隙按钮
            if (GUILayout.Button("检测地形缝隙", GUILayout.Height(40)))
            {
                EditorApplication.delayCall += () => DetectTerrainSeamsAsync();
            }

            EditorGUI.BeginDisabledGroup(seamInfoList.Count == 0);

            // 修复缝隙按钮
            if (GUILayout.Button("修复检测到的缝隙", GUILayout.Height(40)))
            {
                EditorApplication.delayCall += () => FixTerrainSeamsAsync();
            }

            EditorGUI.EndDisabledGroup();

            // 一键检测并修复按钮
            if (GUILayout.Button("一键检测并修复缝隙", GUILayout.Height(40)))
            {
                EditorApplication.delayCall += () => DetectAndFixTerrainSeamsAsync();
            }

            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// 显示地形信息的递归方法
        /// </summary>
        private void DisplayTerrainInfo(Transform parent, int depth)
        {
            const int indentScale = 4;
            string indent = new string(' ', depth * indentScale);

            // 处理当前对象
            string currentInfo = $"{indent}{parent.name}";
            EditorGUILayout.LabelField(currentInfo);

            // 检查当前对象是否有Terrain组件
            Terrain terrain = parent.GetComponent<Terrain>();
            if (terrain != null && terrain.terrainData != null)
            {
                string terrainIndent = new string(' ', (depth + 1) * indentScale);
                Vector3 size = terrain.terrainData.size;
                int res = terrain.terrainData.heightmapResolution;
                EditorGUILayout.LabelField($"{terrainIndent}地形大小: {size.x:F1} x {size.z:F1} x {size.y:F1}");
                EditorGUILayout.LabelField($"{terrainIndent}高度图分辨率: {res} x {res}");
            }

            // 递归处理所有子对象
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                DisplayTerrainInfo(child, depth + 1);
            }
        }


        /// <summary>
        /// 异步检测地形缝隙
        /// </summary>
        private async void DetectTerrainSeamsAsync()
        {
            if (targetTerrainObject == null) return;

            try
            {
                EditorUtility.DisplayProgressBar("地形缝隙检测", "初始化检测...", 0.0f);

                // 收集所有地形
                List<Terrain> terrains = new List<Terrain>();
                TerrainUtils.CollectTerrainsRecursively(targetTerrainObject.transform, terrains);


                if (terrains.Count == 0)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("提示", "未找到任何Terrain组件", "确定");
                    return;
                }

                seamInfoList.Clear();

                // 创建检测器并执行检测
                detector = new TerrainSeamDetector(seamThreshold);
                var detectedSeams = await detector.DetectSeamsAsync(terrains);
                seamInfoList.AddRange(detectedSeams);

                EditorUtility.ClearProgressBar();

                EditorUtility.DisplayDialog("检测完成",
                    $"地形缝隙检测完成，共发现 {seamInfoList.Count} 处缝隙", "确定");


                // 重绘窗口以更新UI
                Repaint();
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"地形缝隙检测失败: {e.Message}");
                EditorUtility.DisplayDialog("错误", $"地形缝隙检测失败: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 异步修复地形缝隙
        /// </summary>
        private async void FixTerrainSeamsAsync()
        {
            if (targetTerrainObject == null || seamInfoList.Count == 0) return;

            try
            {
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"地形缝隙修复 - {targetTerrainObject.name}");

                // 统一记录所有相关地形的Undo状态
                var affectedTerrains = new HashSet<Terrain>();
                foreach (var seamInfo in seamInfoList)
                {
                    if (seamInfo.terrain1 != null) affectedTerrains.Add(seamInfo.terrain1);
                    if (seamInfo.terrain2 != null) affectedTerrains.Add(seamInfo.terrain2);
                }

                TerrainSeamFixer.RecordTerrainsUndo(affectedTerrains, $"地形缝隙修复 ({seamInfoList.Count}处缝隙)");

                // 创建修复器并执行修复
                fixer = new TerrainSeamFixer(seamThreshold, fixVerticalSeams, fixHorizontalSeams,
                    fixCornerPoints, blendWidth, blendCurve);

                int fixedCount = await fixer.FixSeamsAsync(seamInfoList, affectedTerrains);

                Undo.CollapseUndoOperations(undoGroup);
                EditorUtility.ClearProgressBar();

                EditorUtility.DisplayDialog("修复完成",
                    $"地形缝隙修复完成，共修复了 {fixedCount} 处缝隙", "确定");


                // 清空缝隙列表，因为已经修复了
                seamInfoList.Clear();
                Repaint();
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"地形缝隙修复失败: {e.Message}");
                EditorUtility.DisplayDialog("错误", $"地形缝隙修复失败: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 一键检测并修复地形缝隙
        /// </summary>
        private async void DetectAndFixTerrainSeamsAsync()
        {
            if (targetTerrainObject == null) return;

            try
            {
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"检测并修复地形缝隙 - {targetTerrainObject.name}");

                EditorUtility.DisplayProgressBar("地形缝隙检测和修复", "收集地形信息...", 0.0f);

                // 收集所有地形
                List<Terrain> terrains = new List<Terrain>();
                TerrainUtils.CollectTerrainsRecursively(targetTerrainObject.transform, terrains);


                if (terrains.Count == 0)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("提示", "未找到任何Terrain组件", "确定");
                    return;
                }

                seamInfoList.Clear();

                // 阶段1: 检测缝隙
                EditorUtility.DisplayProgressBar("地形缝隙检测和修复", "检测地形缝隙...", 0.1f);

                detector = new TerrainSeamDetector(seamThreshold);
                var detectedSeams = await detector.DetectSeamsAsync(terrains);
                seamInfoList.AddRange(detectedSeams);


                // 在开始修复前统一记录所有相关地形的Undo状态
                var affectedTerrains = new HashSet<Terrain>();
                if (seamInfoList.Count > 0)
                {
                    foreach (var seamInfo in seamInfoList)
                    {
                        if (seamInfo.terrain1 != null) affectedTerrains.Add(seamInfo.terrain1);
                        if (seamInfo.terrain2 != null) affectedTerrains.Add(seamInfo.terrain2);
                    }

                        TerrainSeamFixer.RecordTerrainsUndo(affectedTerrains, $"地形缝隙修复 ({seamInfoList.Count}处缝隙)");
                }

                // 阶段2: 修复边缘缝隙
                int fixedCount = 0;
                if (seamInfoList.Count > 0)
                {
                    fixer = new TerrainSeamFixer(seamThreshold, fixVerticalSeams, fixHorizontalSeams,
                        fixCornerPoints, blendWidth, blendCurve);

                    fixedCount = await fixer.FixSeamsAsync(seamInfoList, affectedTerrains);
                }

                // 阶段3: 修复角点
                if (fixCornerPoints)
                {
                    EditorUtility.DisplayProgressBar("地形缝隙检测和修复", "修复4瓦片共用角点...", 0.8f);

                    // 为角点修复记录Undo状态（如果还没记录过）
                    if (seamInfoList.Count == 0) // 如果没有缝隙需要修复，但需要修复角点
                    {
                        TerrainSeamFixer.RecordTerrainsUndo(terrains, "4瓦片共用角点修复");
                    }

                    cornerFixer = new TerrainCornerFixer(blendWidth, blendCurve);
                    int cornerFixed = await cornerFixer.FixAllCornerPointsAsync(terrains);
                    fixedCount += cornerFixed;
                }

                Undo.CollapseUndoOperations(undoGroup);
                EditorUtility.ClearProgressBar();

                EditorUtility.DisplayDialog("修复完成",
                    $"地形缝隙检测和修复完成，共修复了 {fixedCount} 处缝隙", "确定");

                seamInfoList.Clear();
                Repaint();
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"地形缝隙检测和修复失败: {e.Message}");
                EditorUtility.DisplayDialog("错误", $"地形缝隙检测和修复失败: {e.Message}", "确定");
            }
        }

    }
}