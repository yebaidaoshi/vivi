using System;
using System.Collections;
using UnityEngine;

namespace Com.LuisPedroFonseca.ProCamera2D
{
	public abstract class BaseTrigger : BasePC2D
	{
		public Action OnEnteredTrigger;

		public Action OnExitedTrigger;

		[Tooltip("Every X seconds detect collision. Smaller intervals are more precise but also require more processing.")]
		public float UpdateInterval = 0.1f;

		public TriggerShape TriggerShape;

		[Tooltip("If enabled, use the targets mid point to know when inside/outside the trigger.")]
		public bool UseTargetsMidPoint = true;

		[Tooltip("If UseTargetsMidPoint is disabled, use this transform to know when inside/outside the trigger.")]
		public Transform TriggerTarget;

		protected float _exclusiveInfluencePercentage;

		private Coroutine _testTriggerRoutine;

		protected bool _insideTrigger;

		protected Vector2 _vectorFromPointToCenter;

		protected int _instanceID;

		private bool _triggerEnabled;

		protected override void Awake()
		{
			base.Awake();
			if (!(base.ProCamera2D == null))
			{
				_instanceID = GetInstanceID();
				UpdateInterval += UnityEngine.Random.Range(-0.02f, 0.02f);
				Toggle(value: true);
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			if (!(base.ProCamera2D == null) && _triggerEnabled)
			{
				Toggle(value: true);
			}
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			_testTriggerRoutine = null;
		}

		public void Toggle(bool value)
		{
			if (value)
			{
				if (_testTriggerRoutine == null)
				{
					_testTriggerRoutine = StartCoroutine(TestTriggerRoutine());
				}
				_triggerEnabled = true;
				return;
			}
			if (_testTriggerRoutine != null)
			{
				StopCoroutine(_testTriggerRoutine);
				_testTriggerRoutine = null;
			}
			if (_insideTrigger)
			{
				ExitedTrigger();
			}
			_triggerEnabled = false;
		}

		public void TestTrigger()
		{
			Vector3 arg = base.ProCamera2D.TargetsMidPoint;
			if (!UseTargetsMidPoint && TriggerTarget != null)
			{
				arg = TriggerTarget.position;
			}
			if (TriggerShape == TriggerShape.RECTANGLE && Utils.IsInsideRectangle(Vector3H(_transform.position), Vector3V(_transform.position), Vector3H(_transform.localScale), Vector3V(_transform.localScale), Vector3H(arg), Vector3V(arg)))
			{
				if (!_insideTrigger)
				{
					EnteredTrigger();
				}
			}
			else if (TriggerShape == TriggerShape.CIRCLE && Utils.IsInsideCircle(Vector3H(_transform.position), Vector3V(_transform.position), (Vector3H(_transform.localScale) + Vector3V(_transform.localScale)) * 0.25f, Vector3H(arg), Vector3V(arg)))
			{
				if (!_insideTrigger)
				{
					EnteredTrigger();
				}
			}
			else if (_insideTrigger)
			{
				ExitedTrigger();
			}
		}

		protected virtual void EnteredTrigger()
		{
			_insideTrigger = true;
			if (OnEnteredTrigger != null)
			{
				OnEnteredTrigger();
			}
		}

		protected virtual void ExitedTrigger()
		{
			_insideTrigger = false;
			if (OnExitedTrigger != null)
			{
				OnExitedTrigger();
			}
		}

		private IEnumerator TestTriggerRoutine()
		{
			yield return new WaitForEndOfFrame();
			WaitForSeconds waitForSeconds = new WaitForSeconds(UpdateInterval);
			WaitForSecondsRealtime waitForSecondsRealtime = new WaitForSecondsRealtime(UpdateInterval);
			while (true)
			{
				TestTrigger();
				if (base.ProCamera2D.IgnoreTimeScale)
				{
					yield return waitForSecondsRealtime;
				}
				else
				{
					yield return waitForSeconds;
				}
			}
		}

		protected float GetDistanceToCenterPercentage(Vector2 point)
		{
			_vectorFromPointToCenter = point - new Vector2(Vector3H(_transform.position), Vector3V(_transform.position));
			if (TriggerShape == TriggerShape.RECTANGLE)
			{
				float f = Vector3H(_vectorFromPointToCenter) / (Vector3H(_transform.localScale) * 0.5f);
				return Mathf.Max(b: Mathf.Abs(Vector3V(_vectorFromPointToCenter) / (Vector3V(_transform.localScale) * 0.5f)), a: Mathf.Abs(f)).Remap(_exclusiveInfluencePercentage, 1f, 0f, 1f);
			}
			return (_vectorFromPointToCenter.magnitude / ((Vector3H(_transform.localScale) + Vector3V(_transform.localScale)) * 0.25f)).Remap(_exclusiveInfluencePercentage, 1f, 0f, 1f);
		}
	}
}
