using UnityEditor;
using UnityEngine;

public static class Live2DCoreImportFix
{
    const string DllPath =
        "Assets/Live2D/Cubism/Plugins/Windows/x86_64/Live2DCubismCore.dll";

    [MenuItem("Tools/Live2D/Fix Cubism Core Import Settings")]
    public static void Fix()
    {
        var importer = AssetImporter.GetAtPath(DllPath) as PluginImporter;
        if (importer == null)
        {
            Debug.LogError($"PluginImporter not found at {DllPath}");
            return;
        }

        importer.SetCompatibleWithAnyPlatform(false);
        importer.SetCompatibleWithEditor(true);
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);

        importer.SetEditorData("CPU", "x86_64");
        importer.SetEditorData("OS", "Windows");
        importer.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");

        importer.SaveAndReimport();

        Debug.Log($"Cubism Core 导入设置已彻底修复！");
    }
}