using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成"游玩时专用"的 Live2D HUD 预制体（Resources/Live2D/Live2DHud.prefab）。
///
/// 预制体里只有：根物体 + HUD 相机 + Canvas/RawImage 这几个骨架，
/// Vivi 模型和 RenderTexture 都是运行时生成的（RT 存不进 prefab）。
/// 生成后可以在 Inspector 里直接调相机参数 / RectTransform，运行时优先用这个预制体。
/// </summary>
public static class Live2DHudPrefabTools
{
    private const string PrefabPath = "Assets/Resources/Live2D/Live2DHud.prefab";
    private const string ConfigPath = "Live2D/Live2DHudConfig";

    [MenuItem("Tools/Vivi/Live2D/生成游玩时 HUD 预制体")]
    public static void Create()
    {
        var config = Resources.Load<Live2DHudConfig>(ConfigPath);

        if (config == null)
        {
            Debug.LogError($"找不到 Resources/{ConfigPath}，先生成/修好配置资源。");
            return;
        }

        if (File.Exists(PrefabPath))
        {
            var overwrite = EditorUtility.DisplayDialog(
                "Live2D HUD 预制体",
                "已存在 " + PrefabPath + "，要覆盖吗？",
                "覆盖", "取消");

            if (!overwrite)
            {
                return;
            }
        }

        // 临时搭一份，保存成 prefab 后再删掉
        var root = new GameObject("[Live2D HUD]");
        var hud = root.AddComponent<Live2DRealtimeHud>();
        hud.config = config;
        hud.BuildRig();

        var directory = Path.GetDirectoryName(PrefabPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();

        Debug.Log($"[Live2D HUD] 预制体已生成：{PrefabPath}（运行时优先用它，删掉就回退到代码搭建）");

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (asset != null)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
