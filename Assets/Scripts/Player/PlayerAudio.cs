using UnityEngine;

namespace Player
{
    /// <summary>
    /// 一次性音效播放器。可在 Inspector 中指定片段；缺失的片段会被跳过。
    /// </summary>
    public class PlayerAudio : MonoBehaviour
    {
        [SerializeField]
        private AudioSource seSource;

        [Header("移动")]
        [Tooltip("起跳时的蹬地脚步声（Jump FSM AudioPlay = STEPS Dirt_ Run 03）。")]
        public AudioClip jump;
        [Tooltip("Jump.ogg — 起跳呼啸，叠在脚步声之上。")]
        public AudioClip jumpTakeoff;
        public AudioClip landing;
        [Tooltip("Jump_BackFlip — State 7 起跳。")]
        public AudioClip backFlip;
        [Tooltip("Jump_BackFlip_Land — 后空翻落地。")]
        public AudioClip backFlipLand;
        [Tooltip("EventLand / Shirimochi_Land — 硬落地（过高）。")]
        public AudioClip hardLanding;
        public AudioClip run1;
        public AudioClip run2;
        public AudioClip backStep;
        public AudioClip slide;
        [SerializeField] private AudioClip dashStepSound;//冲刺
        [Header("近战")]
        public AudioClip swordSwing;
        public AudioClip melee2;
        public AudioClip melee3;
        public AudioClip meleePlus;
        [Tooltip("E_Katana1 挥砍（Attack）— 连招 1。")]
        public AudioClip attack1;
        [Tooltip("E_Katana2 挥砍（Attack2）— 连招 2 主刀。")]
        public AudioClip attack2;
        [Tooltip("E_Katana3 挥砍（Attack3）— 连招 2 副刀。")]
        public AudioClip attack3;
        [Tooltip("Melee4_2 — E_Katana4（Attack4 / Melee4 挥砍）。")]
        public AudioClip melee4;
        [Tooltip("Melee4Afterwind — E_Katana4 收招风声 SE（与挥砍同时播放）。")]
        public AudioClip melee4Afterwind;
        [Tooltip("Melee4After — 来自 _Melee4AfterSlash FSM 的延迟余斩 SE（此处亦作备用播放）。")]
        public AudioClip melee4After;
        [Tooltip("SwordSheathe1 — SE_Noutou 动画事件。")]
        public AudioClip swordSheathe1;
        [Tooltip("SwordSheathe — SE_Noutou2 动画事件。")]
        public AudioClip swordSheathe;
        [Tooltip("Melee4Sheathe — SE_NoutouFast 动画事件（Attack4 / Melee4）。")]
        public AudioClip melee4Sheathe;
        public AudioClip jumpAttackDown;

        [Header("枪械")]
        public AudioClip aiming;
        public AudioClip gunFire;
        [Tooltip("弹匣序列未指定时的备用单段换弹片段。")]
        public AudioClip reload;
        [Tooltip("RemoveMagazine — 换弹开始时的 SE_OffMagazine。")]
        public AudioClip removeMagazine;
        [Tooltip("SetMagazine — 换弹完成时的 SE_SetMagazine。")]
        public AudioClip setMagazine;
        [Tooltip("Cocking — 装上弹匣后的 SE_Cocking。")]
        public AudioClip cocking;

        [Header("魔法")]
        [Tooltip("WindMagic.ogg — Magic2 FrontCast / WindMagic 预制体 AudioPlay。")]
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
			// PlayerController 在运行时组合模块（场景中无序列化片段引用），
			// 因此在编辑器中播放时按名称从工程拉取 SE 片段。
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
            // 脚步（STEPS Dirt_ Run 03）+ Jump.ogg 呼啸，一起播放。
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
        /// 片段内烘焙的 SendEvent(...) 动画事件接收器（经 PlayerController 桥接）。
        /// 枪械换弹 / 收枪 SE 必须在此处理 — Effects FSM 过去在这些事件上播放它们。
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
                    // 换弹拉栓 / 退膛·上膛 — Aim_*_SMG_Reload @ 0.8667
                    PlayCocking();
                    break;
                case "SE_foldSMG":
                    // 收枪折叠在 floor Effects State 11 使用同一段 Cocking.ogg。
                    PlayCocking();
                    break;
                case "Stepped_Forward":
                    Play(dashStepSound); 
                    break;
            }
        }
        public void PlayAiming() => Play(aiming);
        public void PlayGunFire() => Play(gunFire);
        /// <summary>换弹开始时的 SE_OffMagazine。</summary>
        public void PlayMagazineOut() => Play(removeMagazine != null ? removeMagazine : reload);
        /// <summary>换弹中途的 SE_SetMagazine。</summary>
        public void PlaySetMagazine() => Play(setMagazine != null ? setMagazine : reload);
        /// <summary>SE_Cocking / SE_foldSMG — Cocking.ogg。</summary>
        public void PlayCocking() => Play(cocking != null ? cocking : reload);
        /// <summary>
        /// 动画事件未触发时的回退：SetMagazine + Cocking。
        /// Animator 桥接可用时，优先使用换弹片段中定时的 SendEvent(SE_*)。
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
        /// 地面连招挥砍 SE，按片段的 E_Katana 索引。保留基础挥砍
        ///（SwordSwing / Melee2 / Melee3），并同时播放参考工程叠在上面的
        /// 各攻击覆盖音（Attack / Attack2 / Attack3），外加 Meleeplus。
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

        /// <summary>非连招挥砍（滑铲 / 跳攻向上）：SwordSwing + Meleeplus。</summary>
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

        /// <summary>E_Katana4（Effects State 15）：Melee4_2 + Meleeplus + Melee4Afterwind。</summary>
        public void PlayMelee4()
        {
            Play(melee4 != null ? melee4 : swordSwing);
            if (meleePlus != null)
            {
                Play(meleePlus, 0.7f);
            }

            // floor 在 E_Katana4 上的第三个 AudioPlay — 立即播放收招呼啸。
            Play(melee4Afterwind != null ? melee4Afterwind : melee4After);
        }

        /// <summary>
        /// 延迟余斩 SE（_Melee4AfterSlash State 6 AudioPlay → Melee4After.ogg）。
        /// 由 PlayerMelee 驱动，因为预制体的 ChronosWait 在没有 Chronos Clock 时永不结束，
        /// 且其 AudioPlay 可能指向空的全局 AudioMaster。
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
		/// 按 floor.unity 文件名从 Assets/AudioClip 绑定 SE 片段。供 Awake 自动赋值
		///（onlyMissing）与 Tools 菜单赋值器（force）共用。仅编辑器。
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
