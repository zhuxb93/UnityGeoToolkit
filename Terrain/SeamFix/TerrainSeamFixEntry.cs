using UnityEditor;

namespace GeoToolkit.SeamFix
{
    /// <summary>
    /// 地形缝隙修复工具菜单入口
    /// </summary>
    public static class TerrainSeamFixEntry
    {
        //[MenuItem("GeoToolkit/地形工具/地形缝隙修复工具")]
        public static void OpenTerrainSeamFixer()
        {
            TerrainSeamFixWindow.ShowWindow();
        }
    }
}