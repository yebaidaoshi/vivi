#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Enemy.EditorTools
{
    /// <summary>
    /// 从 Assets/Audio/Clips 里按名字匹配敌人音效。
    /// 菜单：Tools/Vivi/敌人/...
    /// </summary>
    public static class EnemyAudioFinder
    {
        private const string Root = "Assets/Audio/Clips";

        private static readonly Dictionary<string, string> HyenaMap = new Dictionary<string, string>
        {
            { "Hyena_VClaw",       "attackWindup" },
            { "Hyena_Clawhit",     "attackHit" },
            { "Hyena_Vdeath",      "death" },
            { "Hyena_VJumpAttack1","jumpAttack" },
            { "Hyena_Slide",       "flee" },
            { "ArmorDamaged",      "hurt" },
            { "Break_New",         "stun" },
        };

        [MenuItem("Tools/Vivi/敌人/自动匹配鬣狗音效（精确）", false, 40)]
        public static void AssignHyena()
        {
            var go = Selection.activeGameObject;
            if (go == null) { Debug.LogWarning("[EnemyAudioFinder] 先选中敌人。"); return; }

            var audio = go.GetComponent<EnemyAudio>();
            if (audio == null) audio = go.AddComponent<EnemyAudio>();

            var index = BuildIndex();
            int assigned = 0;
            foreach (var kv in HyenaMap)
            {
                if (!index.TryGetValue(kv.Key, out string path)) continue;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                switch (kv.Value)
                {
                    case "attackWindup": audio.attackWindup = clip; break;
                    case "attackHit": audio.attackHit = clip; break;
                    case "death": audio.death = clip; break;
                    case "jumpAttack": audio.jumpAttack = clip; break;
                    case "flee": audio.flee = clip; break;
                    case "hurt": audio.hurt = clip; break;
                    case "stun": audio.stun = clip; break;
                }
                assigned++;
            }

            EditorUtility.SetDirty(audio);
            Debug.Log("[EnemyAudioFinder] 鬣狗音效已匹配 " + assigned + " / " + HyenaMap.Count + " 项。", audio);
        }

        [MenuItem("Tools/Vivi/敌人/列出鬣狗候选音效", false, 41)]
        public static void ListHyenaClips()
        {
            var all = FindByKeyword("hyena");
            Debug.Log("[EnemyAudioFinder] Assets/Audio/Clips 中名字含 hyena 的音频：" + all.Count + " 个");
            foreach (var p in all) Debug.Log("  " + p);
        }

        private static Dictionary<string, string> BuildIndex()
        {
            var map = new Dictionary<string, string>();
            if (!AssetDatabase.IsValidFolder(Root)) return map;

            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { Root });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!map.ContainsKey(name)) map[name] = path;
            }
            return map;
        }

        private static List<string> FindByKeyword(string keyword)
        {
            var result = new List<string>();
            if (!AssetDatabase.IsValidFolder(Root)) return result;

            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { Root });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Contains(keyword))
                    result.Add(path);
            }
            return result;
        }
    }
}
#endif
