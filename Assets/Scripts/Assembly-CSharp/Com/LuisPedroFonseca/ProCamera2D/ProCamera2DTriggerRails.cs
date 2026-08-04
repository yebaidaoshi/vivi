using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	[HelpURL("http://www.procamera2d.com/user-guide/trigger-rails/")]
	public class ProCamera2DTriggerRails : BaseTrigger
	{
		public static string TriggerName = "Rails Trigger";

		[HideInInspector]
		public ProCamera2DRails ProCamera2DRails;

		public TriggerRailsMode Mode;

		public float TransitionDuration = 1f;

		private void Start()
		{
			if (!(base.ProCamera2D == null))
			{
				if (ProCamera2DRails == null)
				{
					ProCamera2DRails = Object.FindObjectOfType<ProCamera2DRails>();
				}
				if (ProCamera2DRails == null)
				{
					Debug.LogWarning("Rails extension couldn't be found on ProCamera2D. Please enable it to use this trigger.");
				}
			}
		}

		protected override void EnteredTrigger()
		{
			base.EnteredTrigger();
			if (Mode == TriggerRailsMode.Enable)
			{
				ProCamera2DRails.EnableTargets(TransitionDuration);
			}
			else
			{
				ProCamera2DRails.DisableTargets(TransitionDuration);
			}
		}
	}
}
