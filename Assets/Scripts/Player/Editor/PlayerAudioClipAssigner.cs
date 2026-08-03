#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Player.Editor
{
	/// <summary>
	/// Force-assigns AudioClips onto the selected PlayerAudio components (for scenes that keep
	/// serialized clip refs). Shares the filename map with <see cref="PlayerAudio.EditorAssignClips"/>.
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
