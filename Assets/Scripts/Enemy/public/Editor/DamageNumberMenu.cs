#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UI;

namespace Enemy.EditorTools
{
    /// <summary>
    /// 伤害数字相关的编辑器菜单。
    /// </summary>
    public static class DamageNumberMenu
    {
        [MenuItem("Tools/Vivi/UI/创建伤害数字配置", false, 60)]
        public static void CreateConfig()
        {
            var existing = Object.FindObjectOfType<DamageNumberConfig>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[DamageNumberMenu] 场景里已有配置：" + existing.gameObject.name, existing);
                return;
            }

            var go = new GameObject("DamageNumberConfig");
            var cfg = go.AddComponent<DamageNumberConfig>();
            Undo.RegisterCreatedObjectUndo(go, "Create DamageNumberConfig");
            Selection.activeGameObject = go;

            Debug.Log("[DamageNumberMenu] 已创建 DamageNumberConfig，在 Inspector 里调 Font Size / Character Size 即可实时生效。", cfg);
        }
    }
}
#endif
