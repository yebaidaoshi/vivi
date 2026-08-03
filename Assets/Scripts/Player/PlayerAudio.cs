using UnityEngine;

namespace Player
{
    /// <summary>
    /// One-shot SE player. Clips can be assigned in the inspector; missing clips are skipped.
    /// </summary>
    public class PlayerAudio : MonoBehaviour
    {
        [SerializeField]
        private AudioSource seSource;

        [Header("Locomotion")]
        [Tooltip("Ground-push step on takeoff (Jump FSM AudioPlay = STEPS Dirt_ Run 03).")]
        public AudioClip jump;
        [Tooltip("Jump.ogg — takeoff whoosh, layered on top of the step.")]
        public AudioClip jumpTakeoff;
        public AudioClip landing;
        [Tooltip("Jump_BackFlip — State 7 takeoff.")]
        public AudioClip backFlip;
        [Tooltip("Jump_BackFlip_Land — backflip touchdown.")]
        public AudioClip backFlipLand;
        [Tooltip("EventLand / Shirimochi_Land — hard (too-high) landing.")]
        public AudioClip hardLanding;
        public AudioClip run1;
        public AudioClip run2;
        public AudioClip backStep;
        public AudioClip slide;

        [Header("Melee")]
        public AudioClip swordSwing;
        public AudioClip melee2;
        public AudioClip melee3;
        public AudioClip meleePlus;
        [Tooltip("E_Katana1 swing (Attack) — combo 1.")]
        public AudioClip attack1;
        [Tooltip("E_Katana2 swing (Attack2) — combo 2 primary.")]
        public AudioClip attack2;
        [Tooltip("E_Katana3 swing (Attack3) — combo 2 secondary.")]
        public AudioClip attack3;
        [Tooltip("Melee4_2 — E_Katana4 (Attack4 / Melee4 swing).")]
        public AudioClip melee4;
        [Tooltip("Melee4Afterwind — E_Katana4 afterwind SE (plays with the swing).")]
        public AudioClip melee4Afterwind;
        [Tooltip("Melee4After — delayed afterslash SE from _Melee4AfterSlash FSM (also played here as backup).")]
        public AudioClip melee4After;
        [Tooltip("SwordSheathe1 — SE_Noutou animation event.")]
        public AudioClip swordSheathe1;
        [Tooltip("SwordSheathe — SE_Noutou2 animation event.")]
        public AudioClip swordSheathe;
        [Tooltip("Melee4Sheathe — SE_NoutouFast animation event (Attack4 / Melee4).")]
        public AudioClip melee4Sheathe;
        public AudioClip jumpAttackDown;

        [Header("Gun")]
        public AudioClip aiming;
        public AudioClip gunFire;
        [Tooltip("Fallback single reload clip if the magazine sequence is unassigned.")]
        public AudioClip reload;
        [Tooltip("RemoveMagazine — SE_OffMagazine at reload start.")]
        public AudioClip removeMagazine;
        [Tooltip("SetMagazine — SE_SetMagazine at reload finish.")]
        public AudioClip setMagazine;
        [Tooltip("Cocking — SE_Cocking after the magazine is set.")]
        public AudioClip cocking;

        [Header("Magic")]
        [Tooltip("WindMagic.ogg — Magic2 FrontCast / WindMagic prefab AudioPlay.")]
        public AudioClip windMagic;

        private float _nextFootstep;
        private int _runToggle;

        private void Awake()
        {
            if (seSource == null)
            {
                seSource = GetComponent<AudioSource>();
            }

            if (seSource == null)
            {
                seSource = gameObject.AddComponent<AudioSource>();
                seSource.playOnAwake = false;
                seSource.spatialBlend = 0f;
            }

#if UNITY_EDITOR
			// PlayerController composes modules at runtime (no serialized clip refs in the scene),
			// so pull the SE clips from the project by name when playing in the editor.
			EditorAssignClips(onlyMissing: true);
#endif
        }

        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null || seSource == null)
            {
                return;
            }

            seSource.PlayOneShot(clip, volume);
        }

        public void PlayJump()
        {
            // Step (STEPS Dirt_ Run 03) + Jump.ogg whoosh, played together.
            Play(jump);
            Play(jumpTakeoff);
        }
        public void PlayBackFlip() => Play(backFlip != null ? backFlip : jump);
        public void PlayLanding() => Play(landing != null ? landing : jump);
        public void PlayBackFlipLand() =>
            Play(backFlipLand != null ? backFlipLand : (landing != null ? landing : jump));
        public void PlayHardLanding() =>
            Play(hardLanding != null ? hardLanding : (landing != null ? landing : jump));
        public void PlayBackStep() => Play(backStep);
        public void PlaySlide() => Play(slide != null ? slide : backStep);

        /// <summary>
        /// Animation-event receiver for SendEvent(...) baked into clips (via PlayerController bridge).
        /// Gun reload / fold SEs must be handled here — Effects FSM used to play them on these events.
        /// </summary>
        public void SendEvent(string eventName)
        {
            switch (eventName)
            {
                case "SE_Noutou":
                    Play(swordSheathe1 != null ? swordSheathe1 : swordSheathe);
                    break;
                case "SE_Noutou2":
                    Play(swordSheathe != null ? swordSheathe : swordSheathe1);
                    break;
                case "SE_NoutouFast":
                    Play(melee4Sheathe != null ? melee4Sheathe : swordSheathe);
                    break;
                case "SE_OffMagazine":
                    PlayMagazineOut();
                    break;
                case "SE_SetMagazine":
                    PlaySetMagazine();
                    break;
                case "SE_Cocking":
                    // Reload bolt / 退膛·上膛 — Aim_*_SMG_Reload @ 0.8667
                    PlayCocking();
                    break;
                case "SE_foldSMG":
                    // Holster fold uses the same Cocking.ogg in floor Effects State 11.
                    PlayCocking();
                    break;
            }
        }
        public void PlayAiming() => Play(aiming);
        public void PlayGunFire() => Play(gunFire);
        /// <summary>SE_OffMagazine at reload start.</summary>
        public void PlayMagazineOut() => Play(removeMagazine != null ? removeMagazine : reload);
        /// <summary>SE_SetMagazine mid-reload.</summary>
        public void PlaySetMagazine() => Play(setMagazine != null ? setMagazine : reload);
        /// <summary>SE_Cocking / SE_foldSMG — Cocking.ogg.</summary>
        public void PlayCocking() => Play(cocking != null ? cocking : reload);
        /// <summary>
        /// Fallback when anim events did not fire: SetMagazine + Cocking.
        /// Prefer timed SendEvent(SE_*) from the reload clip when the Animator bridge is live.
        /// </summary>
        public void PlayReload()
        {
            if (setMagazine == null && cocking == null)
            {
                Play(reload);
                return;
            }

            Play(setMagazine);
            Play(cocking, 0.9f);
        }
        public void PlayJumpAttackDown() => Play(jumpAttackDown != null ? jumpAttackDown : swordSwing);
        public void PlayWindMagic() => Play(windMagic);

        /// <summary>
        /// Ground-combo swing SEs keyed by the clip's E_Katana index. The base swing
        /// (SwordSwing / Melee2 / Melee3) is kept and the per-attack overlay the reference
        /// layers on top (Attack / Attack2 / Attack3) is played simultaneously, + Meleeplus.
        /// </summary>
        public void PlayKatana(int katanaIndex)
        {
            switch (katanaIndex)
            {
                case 2:
                    Play(melee2 != null ? melee2 : swordSwing);
                    Play(attack2);
                    break;
                case 3:
                    Play(melee3 != null ? melee3 : swordSwing);
                    Play(attack3);
                    break;
                default:
                    Play(swordSwing);
                    PlayCocking();
                    Play(attack1);
                    break;
            }

            if (meleePlus != null)
            {
                Play(meleePlus, 0.7f);
            }
        }

        /// <summary>Non-combo swing (slide / jump-attack-up): SwordSwing + Meleeplus.</summary>
        public void PlayMeleeSwing(int comboIndex)
        {
            switch (comboIndex)
            {
                case 1:
                    Play(melee2 != null ? melee2 : swordSwing);
                    break;
                case 2:
                    Play(melee3 != null ? melee3 : swordSwing);
                    break;
                default:
                    Play(swordSwing);
                    break;
            }

            if (meleePlus != null)
            {
                Play(meleePlus, 0.7f);
            }
        }

        /// <summary>E_Katana4 (Effects State 15): Melee4_2 + Meleeplus + Melee4Afterwind.</summary>
        public void PlayMelee4()
        {
            Play(melee4 != null ? melee4 : swordSwing);
            if (meleePlus != null)
            {
                Play(meleePlus, 0.7f);
            }

            // floor third AudioPlay on E_Katana4 — immediate afterwind whoosh.
            Play(melee4Afterwind != null ? melee4Afterwind : melee4After);
        }

        /// <summary>
        /// Delayed afterslash SE (_Melee4AfterSlash State 6 AudioPlay → Melee4After.ogg).
        /// Driven from PlayerMelee because the prefab's ChronosWait never finishes without a
        /// Chronos Clock, and its AudioPlay targets a possibly-null global AudioMaster.
        /// </summary>
        public void PlayMelee4After()
        {
            Play(melee4After != null ? melee4After : melee4Afterwind);
        }

        public void TryPlayFootstep(float interval = 0.28f)
        {
            if (Time.time < _nextFootstep)
            {
                return;
            }

            _nextFootstep = Time.time + interval;
            _runToggle = 1 - _runToggle;
            Play(_runToggle == 0 ? run1 : run2);
        }

#if UNITY_EDITOR
		/// <summary>
		/// Wire SE clips from Assets/AudioClip by floor.unity filename. Shared by the auto-assign
		/// on Awake (onlyMissing) and the Tools menu assigner (force). Editor-only.
		/// </summary>
		public void EditorAssignClips(bool onlyMissing)
		{
			void Set(ref AudioClip field, string fileName)
			{
				if (onlyMissing && field != null)
				{
					return;
				}

				var clip = FindClipByName(fileName);
				if (clip != null)
				{
					field = clip;
				}
			}

			Set(ref jump, "STEPS Dirt_ Run 03");
			Set(ref jumpTakeoff, "Jump");
			Set(ref landing, "footstep_dirt_land_10");
			Set(ref backFlip, "Jump_BackFlip");
			Set(ref backFlipLand, "Jump_BackFlip_Land");
			Set(ref hardLanding, "EventLand");
			Set(ref run1, "Run1");
			Set(ref run2, "Run2");
			Set(ref backStep, "BackStep_0");
			Set(ref slide, "Run_Over");
			Set(ref swordSwing, "SwordSwing");
			Set(ref melee2, "Melee2");
			Set(ref melee3, "Melee3");
			Set(ref meleePlus, "Meleeplus");
			Set(ref attack1, "Attack");
			Set(ref attack2, "Attack2");
			Set(ref attack3, "Attack3");
			Set(ref melee4, "Melee4_2");
			Set(ref melee4Afterwind, "Melee4Afterwind");
			Set(ref melee4After, "Melee4After");
			Set(ref swordSheathe1, "SwordSheathe1");
			Set(ref swordSheathe, "SwordSheathe");
			Set(ref melee4Sheathe, "Melee4Sheathe");
			Set(ref jumpAttackDown, "Jump_Attack_Down");
			Set(ref aiming, "Aiming");
			Set(ref gunFire, "ar15-223cal-surpressed-single-shot-processed-B");
			Set(ref reload, "SetMagazine");
			Set(ref removeMagazine, "RemoveMagazine");
			Set(ref setMagazine, "SetMagazine");
			Set(ref cocking, "Cocking");
			Set(ref windMagic, "WindMagic");
		}

		private static AudioClip FindClipByName(string fileNameWithoutExt)
		{
			foreach (string guid in UnityEditor.AssetDatabase.FindAssets(fileNameWithoutExt + " t:AudioClip"))
			{
				string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
				if (System.IO.Path.GetFileNameWithoutExtension(path) == fileNameWithoutExt)
				{
					return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
				}
			}

			return null;
		}
#endif
    }
}
