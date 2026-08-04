using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[ExecuteInEditMode]
	public class BasicBlit : MonoBehaviour
	{
		public Material CurrentMaterial;

		private void OnRenderImage(RenderTexture src, RenderTexture dst)
		{
			if (CurrentMaterial != null)
			{
				Graphics.Blit(src, dst, CurrentMaterial);
			}
		}
	}
}
