using System;
using System.Collections.Generic;
using System.Text;
using Spine.Unity;
using UnityEngine;

// Diagnoses and repairs the SkeletonMecanim runtime initialization on the heroine.
//
// The bundled spine-unity 3.8 runtime can leave MecanimTranslator.animator == null
// (or the skeleton invalid), which makes MecanimTranslator.Apply throw a
// NullReferenceException every frame from SkeletonMecanim.Update.
//
// On Start (after every Awake) this logs the actual runtime state and forces a clean
// rebind so the translator points at the Animator on this GameObject.
[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(10000)]
public class HeroineSpineMecanimFix : MonoBehaviour
{
	private void Start()
	{
		var mecanim = GetComponent<SkeletonMecanim>();
		var animator = GetComponent<Animator>();

		if (mecanim == null)
		{
			Debug.LogError("[HeroineSpineMecanimFix] SkeletonMecanim component not found on " + name);
			return;
		}

		bool translatorNull = mecanim.Translator == null;
		bool translatorAnimatorNull = translatorNull || mecanim.Translator.Animator == null;
		Debug.Log(string.Format(
			"[HeroineSpineMecanimFix] before: valid={0}, skeleton={1}, animator={2}, controller={3}, translator={4}, translator.Animator={5}",
			mecanim.valid,
			mecanim.skeleton != null,
			animator != null,
			animator != null && animator.runtimeAnimatorController != null,
			!translatorNull,
			!translatorAnimatorNull));

		try
		{
			Spine.SkeletonData data = mecanim.skeletonDataAsset != null
				? mecanim.skeletonDataAsset.GetSkeletonData(true)
				: null;
			
		}
		catch (Exception e)
		{
			
		}

		try
		{
			if (animator != null && animator.runtimeAnimatorController != null)
			{
				animator.Rebind();
				animator.Update(0f);
			}

			mecanim.Initialize(true);

			if (mecanim.valid && mecanim.Translator != null && mecanim.Translator.Animator == null && animator != null)
			{
				mecanim.Translator.Initialize(animator, mecanim.skeletonDataAsset);
			}
		}
		catch (Exception e)
		{
			
		}

		bool afterTranslatorNull = mecanim.Translator == null;
		bool afterAnimatorNull = afterTranslatorNull || mecanim.Translator.Animator == null;
		

		AuditClipNames(mecanim, animator);
	}

	// Logs whether the runtime AnimationClip names actually match the Spine animation
	// names. If clip.name comes from the file name (underscore) instead of m_Name
	// (slash), every folder-based animation (Jump/*, Aim/*, Crouch/*) will be reported
	// as MISSING here, which explains why those animations don't play.
	private static void AuditClipNames(SkeletonMecanim mecanim, Animator animator)
	{
		try
		{
			var data = mecanim.skeletonDataAsset != null
				? mecanim.skeletonDataAsset.GetSkeletonData(true)
				: null;
			if (data == null || animator == null || animator.runtimeAnimatorController == null)
			{
				
				return;
			}

			var spineNames = new HashSet<string>();
			foreach (var anim in data.Animations)
			{
				spineNames.Add(anim.Name);
			}

			var clips = animator.runtimeAnimatorController.animationClips;
			int matched = 0;
			var missing = new List<string>();
			foreach (var clip in clips)
			{
				if (clip == null)
				{
					continue;
				}
				if (spineNames.Contains(clip.name))
				{
					matched++;
				}
				else
				{
					missing.Add(clip.name);
				}
			}

			
				

			if (missing.Count > 0)
			{
				var sb = new StringBuilder("[HeroineSpineMecanimFix] clips NOT found in skeleton (runtime clip.name):");
				foreach (var m in missing)
				{
					sb.Append("\n    '").Append(m).Append('\'');
				}
				
			}

			foreach (var probe in new[] {
				"Jump_Jump", "Jump_Jump_Attack_Up", "Jump_Jump_Attack_Down",
				"Aim_Aim_SMG", "Crouch_Crouching" })
			{
				
			}
		}
		catch (Exception e)
		{
			
		}
	}
}
