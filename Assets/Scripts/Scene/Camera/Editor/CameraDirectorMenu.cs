#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SceneCamera.EditorTools
{
    /// <summary>
    /// 相机系统的一键初始化，避免手动挂组件时漏配。
    /// 菜单：Tools/Vivi/摄像机/...
    /// </summary>
    public static class CameraDirectorMenu
    {
        [MenuItem("Tools/Vivi/摄像机/初始化 Main Camera（接管 ProCamera2D）", false, 10)]
        public static void SetupMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[CameraDirector] 场景里没有 tag = MainCamera 的相机。");
                return;
            }

            GameObject go = cam.gameObject;

            var director = go.GetComponent<CameraDirector>();
            if (director == null)
            {
                director = Undo.AddComponent<CameraDirector>(go);
                Debug.Log("[CameraDirector] 已添加 CameraDirector。", director);
            }

            // 关掉会抢 transform 的旧相机脚本
            string[] conflicting = { "ProCamera2D", "ProCamera2DShake", "ScoutCameraFinal" };
            var behaviours = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null || mb == director || !mb.enabled) continue;
                if (System.Array.IndexOf(conflicting, mb.GetType().Name) < 0) continue;

                Undo.RecordObject(mb, "Disable conflicting camera script");
                mb.enabled = false;
                Debug.Log("[CameraDirector] 已禁用冲突脚本：" + mb.GetType().Name, mb);
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        [MenuItem("Tools/Vivi/摄像机/创建相机边界区域", false, 11)]
        public static void CreateBounds()
        {
            Camera cam = Camera.main;

            var go = new GameObject("CameraBounds");
            Undo.RegisterCreatedObjectUndo(go, "Create CameraBounds");

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;

            if (cam != null)
            {
                float h = cam.orthographic ? cam.orthographicSize : 5f;
                float w = cam.orthographic ? h * cam.aspect : 8f;
                go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
                box.size = new Vector2(w * 3f, h * 3f);
            }
            else
            {
                box.size = new Vector2(40f, 20f);
            }

            if (cam != null)
            {
                var bounds = go.AddComponent<CameraBounds2D>();
                var director = cam.GetComponent<CameraDirector>();
                if (director != null)
                {
                    var so = new SerializedObject(director);
                    var prop = so.FindProperty("bounds");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = bounds;
                        so.ApplyModifiedProperties();
                    }
                }
            }

            Selection.activeGameObject = go;
        }
    }
}
#endif
