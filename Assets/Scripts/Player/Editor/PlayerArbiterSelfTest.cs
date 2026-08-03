#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Player.Editor
{
	/// <summary>
	/// Lightweight assert suite for <see cref="PlayerArbiter"/> (menu: Player/Run Arbiter Self-Test).
	/// </summary>
	public static class PlayerArbiterSelfTest
	{
		[MenuItem("Player/Run Arbiter Self-Test")]
		private static void Run()
		{
			int failed = 0;
			failed += Expect("idle loco", Idle(), PlayerAnimOwner.Locomotion, PlayerVelocityOwner.LocomotionRamp, true);
			failed += Expect("melee lock", MeleeLock(), PlayerAnimOwner.Melee, PlayerVelocityOwner.ImmediateOverride, false);
			failed += Expect("slide", Slide(), PlayerAnimOwner.Crouch, PlayerVelocityOwner.ImmediateOverride, false);
			failed += Expect("ads face", Ads(), PlayerFacingOwner.Gun);
			failed += Expect("air locked face", Air(), PlayerFacingOwner.Locked);
			failed += Expect("crouch no flip", CrouchBusy(), false);
			failed += ExpectCrouchAds();

			if (failed == 0)
			{
				Debug.Log("[PlayerArbiterSelfTest] All checks passed.");
			}
			else
			{
				Debug.LogError($"[PlayerArbiterSelfTest] {failed} check(s) failed.");
			}
		}

		private static PlayerLayerSnapshot Idle()
		{
			return new PlayerLayerSnapshot { Grounded = true };
		}

		private static PlayerLayerSnapshot MeleeLock()
		{
			return new PlayerLayerSnapshot
			{
				Grounded = true,
				MeleeLocksMovement = true,
				MeleeIsAttacking = true
			};
		}

		private static PlayerLayerSnapshot Slide()
		{
			return new PlayerLayerSnapshot
			{
				Grounded = true,
				CrouchState = PlayerCrouchState.Sliding
			};
		}

		private static PlayerLayerSnapshot Ads()
		{
			return new PlayerLayerSnapshot
			{
				Grounded = true,
				GunIsAds = true,
				GunIsBusy = true
			};
		}

		private static PlayerLayerSnapshot Air()
		{
			return new PlayerLayerSnapshot { JumpOnAir = true };
		}

		private static PlayerLayerSnapshot CrouchBusy()
		{
			return new PlayerLayerSnapshot
			{
				Grounded = true,
				CrouchState = PlayerCrouchState.Crouching
			};
		}

		private static int ExpectCrouchAds()
		{
			var snap = new PlayerLayerSnapshot
			{
				Grounded = true,
				CrouchState = PlayerCrouchState.Crouching,
				GunIsAds = true,
				GunIsBusy = true
			};
			var c = PlayerArbiter.Resolve(snap);
			int failed = 0;
			if (!c.CanAds)
			{
				Debug.LogError("[PlayerArbiterSelfTest] FAIL crouch+ads: CanAds should be true");
				failed++;
			}

			if (c.FacingOwner != PlayerFacingOwner.Gun)
			{
				Debug.LogError(
					$"[PlayerArbiterSelfTest] FAIL crouch+ads: FacingOwner={c.FacingOwner} (want Gun)");
				failed++;
			}

			if (c.AnimOwner != PlayerAnimOwner.Gun)
			{
				Debug.LogError(
					$"[PlayerArbiterSelfTest] FAIL crouch+ads: AnimOwner={c.AnimOwner} (want Gun)");
				failed++;
			}

			return failed;
		}

		private static int Expect(string name, PlayerLayerSnapshot snap,
			PlayerAnimOwner anim, PlayerVelocityOwner vel, bool canFlip)
		{
			var c = PlayerArbiter.Resolve(snap);
			if (c.AnimOwner != anim || c.VelocityOwner != vel || c.CanFlip != canFlip)
			{
				Debug.LogError(
					$"[PlayerArbiterSelfTest] FAIL {name}: anim={c.AnimOwner} (want {anim}), " +
					$"vel={c.VelocityOwner} (want {vel}), CanFlip={c.CanFlip} (want {canFlip})");
				return 1;
			}

			return 0;
		}

		private static int Expect(string name, PlayerLayerSnapshot snap, PlayerFacingOwner face)
		{
			var c = PlayerArbiter.Resolve(snap);
			if (c.FacingOwner != face)
			{
				Debug.LogError(
					$"[PlayerArbiterSelfTest] FAIL {name}: FacingOwner={c.FacingOwner} (want {face})");
				return 1;
			}

			return 0;
		}

		private static int Expect(string name, PlayerLayerSnapshot snap, bool canFlip)
		{
			var c = PlayerArbiter.Resolve(snap);
			if (c.CanFlip != canFlip)
			{
				Debug.LogError(
					$"[PlayerArbiterSelfTest] FAIL {name}: CanFlip={c.CanFlip} (want {canFlip})");
				return 1;
			}

			return 0;
		}
	}
}
#endif
