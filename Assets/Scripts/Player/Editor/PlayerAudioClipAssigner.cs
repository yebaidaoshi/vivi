#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Player.Editor
{
	/// <summary>
	/// 强制把 AudioClip 赋给选中的 PlayerAudio 组件（用于保留序列化片段引用的场景）。
	/// 与 <see cref="PlayerAudio.EditorAssignClips"/> 共享文件名映射。
	/// </summary>
	public static class PlayerAudioClipAssigner
	{
		[MenuItem("Tools/Player/Assign Audio Clips On Selection")]
		private static void AssignOnSelection()
		{
			foreach (var go in Selection.gameObjects)
			{
				var audio = go.GetComponent<PlayerAudio>()
					?? go.GetComponentInChildren<PlayerAudio>(true);
				if (audio == null)
				{
					continue;
				}

				Undo.RecordObject(audio, "Assign Player Audio Clips");
				audio.EditorAssignClips(onlyMissing: false);
				EditorUtility.SetDirty(audio);
			}
		}
	}
}
#endif
