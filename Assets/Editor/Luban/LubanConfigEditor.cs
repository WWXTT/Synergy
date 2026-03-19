using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;

namespace CardCore.Editor
{
    /// <summary>
    /// Luban 配置刷新工具
    /// </summary>
    public static class LubanConfigEditor
    {
        private const string TOOLS_PATH = "Config/Tools";
        private const string GEN_BAT_NAME = "gen.bat";

        [MenuItem("Tools/一键刷新配置")]
        public static void RefreshConfig()
        {
            string projectRoot = GetProjectRoot();
            string toolsPath = Path.Combine(projectRoot, TOOLS_PATH);
            string genBatPath = Path.Combine(toolsPath, GEN_BAT_NAME);

            if (!File.Exists(genBatPath))
            {
                EditorUtility.DisplayDialog("错误", $"找不到配置生成脚本: {genBatPath}", "确定");
                return;
            }

            EditorUtility.DisplayProgressBar("刷新配置", "正在执行 Luban 配置生成...", 0.5f);

            try
            {
                var process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = $"/c {genBatPath}";
                process.StartInfo.WorkingDirectory = toolsPath;
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;

                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    EditorUtility.DisplayDialog("成功", "配置刷新完成！", "确定");
                    AssetDatabase.Refresh();
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", $"配置生成失败，退出码: {process.ExitCode}", "确定");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("异常", $"执行失败: {e.Message}", "确定");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static string GetProjectRoot()
        {
            // 从 Assets 目录向上查找项目根目录
            string currentDir = Application.dataPath;
            while (!string.IsNullOrEmpty(currentDir))
            {
                if (Directory.Exists(Path.Combine(currentDir, "Config")))
                {
                    return currentDir;
                }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }
            return Path.GetDirectoryName(Application.dataPath);
        }
    }
}
