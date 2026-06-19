#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace GeoToolkit
{
    public static class EditorFileUtils
    {
        public static void CopyDirectory(string sourceDir, string destinationDir)
        {
            // 获取源文件夹的名称
            string rootFolderName = Path.GetFileName(sourceDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // 构造包含根文件夹的目标路径
            string newDestinationDir = Path.Combine(destinationDir, rootFolderName);

            // 递归复制
            CopyDirectoryInternal(sourceDir, newDestinationDir);
        }

        private static void CopyDirectoryInternal(string sourceDir, string destinationDir)
        {
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string destDir = Path.Combine(destinationDir, Path.GetFileName(dir));
                CopyDirectoryInternal(dir, destDir);
            }
        }

        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                //AssetDatabase.Refresh();
            }
        }
    }

}
#endif