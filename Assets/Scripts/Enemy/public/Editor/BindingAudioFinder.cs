#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Player;
using UnityEditor;
using UnityEngine;

namespace Enemy.EditorTools
{
    /// <summary>
    /// 自动填充 BindingProfile 的束缚音效（玩家被鬣狗束缚时）。
    /// 选中 Assets 里的 BindingProfile 资产 → Tools/Vivi/敌人/自动填充束缚音效
    /// </summary>
    public static class BindingAudioFinder
    {
        private const string Root = "Assets/Audio/Clips";

        [MenuItem("Tools/Vivi/敌人/自动填充束缚音效（选中 BindingProfile）", false, 42)]
        public static void Fill()
        {
            var profile = Selection.activeObject as BindingProfile;
            if (profile == null)
            {
                Debug.LogWarning("[BindingAudioFinder] 先在 Project 里选中一个 BindingProfile 资产。");
                return;
            }

            var index = BuildIndex();

            // 每次被束缚攻击扣血时随机播放
            var hits = new List<AudioClip>();
            AddIfExists(index, "Damage_BearHug", hits);
            AddIfExists(index, "Damage_BearHug_Loop", hits);
            AddIfExists(index, "Damage_BearHug_Loop_2", hits);
            profile.struggleHits = hits.ToArray();

            if (index.TryGetValue("Damage_BearHug", out string bearHugPath))
                profile.struggleSe = AssetDatabase.LoadAssetAtPath<AudioClip>(bearHugPath);

            if (index.TryGetValue("Damage_BearHug_Escape", out string escapePath))
                profile.escapeSe = AssetDatabase.LoadAssetAtPath<AudioClip>(escapePath);

            EditorUtility.SetDirty(profile);
            Debug.Log("[BindingAudioFinder] 已填充: 受击随机音 " + profile.struggleHits.Length
                + " 个, escape=" + (profile.escapeSe != null ? profile.escapeSe.name : "null"), profile);
        }

        private static void AddIfExists(Dictionary<string, string> index, string name, List<AudioClip> list)
        {
            if (!index.TryGetValue(name, out string path)) return;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) list.Add(clip);
        }

        private static Dictionary<string, string> BuildIndex()
        {
            var map = new Dictionary<string, string>();
            if (!AssetDatabase.IsValidFolder(Root)) return map;

            foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!map.ContainsKey(name)) map[name] = path;
            }
            return map;
        }
    }
}
#endif
