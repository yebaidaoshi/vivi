#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vivi.Slash.Editor
{
    public class SlashPreviewWindow : EditorWindow
    {
        private Vector2 _scroll;
        private GameObject[] _prefabs;

        [MenuItem("Vivi/Slash/Open Preview Scene")]
        public static void OpenPreviewScene()
        {
            const string path = "Assets/VFX/Slash/Scenes/SlashPreview.unity";
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path);
            }
        }

        [MenuItem("Vivi/Slash/Preview Window")]
        public static void OpenWindow()
        {
            var win = GetWindow<SlashPreviewWindow>("刀光 Slash");
            win.minSize = new Vector2(280, 360);
            win.Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/VFX/Slash/Prefabs" });
            _prefabs = new GameObject[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                _prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("刀光预制体（拖到场景或 PlayerMelee）", EditorStyles.boldLabel);
            if (GUILayout.Button("打开预览场景"))
            {
                OpenPreviewScene();
            }

            if (GUILayout.Button("刷新列表"))
            {
                Refresh();
            }

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_prefabs != null)
            {
                foreach (GameObject prefab in _prefabs)
                {
                    if (prefab == null)
                    {
                        continue;
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                    if (GUILayout.Button("生成", GUILayout.Width(56)))
                    {
                        Spawn(prefab);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void Spawn(GameObject prefab)
        {
            var cam = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
            Vector3 pos = cam != null
                ? cam.transform.position + cam.transform.forward * 8f
                : Vector3.zero;
            pos.z = 0f;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = pos;
            Selection.activeGameObject = instance;
            Undo.RegisterCreatedObjectUndo(instance, "Spawn Slash");
        }
    }
}
#endif
