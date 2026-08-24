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
        public AudioClip jump;
        public AudioClip jumpTakeoff;
        public AudioClip landing;
        public AudioClip backFlip;
        public AudioClip backFlipLand;
        public AudioClip hardLanding;
        public AudioClip run1;
        public AudioClip run2;
        public AudioClip backStep;
        public AudioClip slide;
        [SerializeField] private AudioClip dashStepSound;

        [Header("近战")]
        public AudioClip swordSwing;
        public AudioClip melee2;
        public AudioClip melee3;
        public AudioClip meleePlus;
        public AudioClip attack1;
        public AudioClip attack2;
        public AudioClip attack3;
        public AudioClip attack4;
        public AudioClip melee4;
        public AudioClip melee4Afterwind;
        public AudioClip melee4After;
        public AudioClip swordSheathe1;
        public AudioClip swordSheathe;
        public AudioClip melee4Sheathe;
        public AudioClip jumpAttackDown;

        [Header("枪械")]
        public AudioClip aiming;
        public AudioClip gunFire;
        public AudioClip reload;
        public AudioClip removeMagazine;
        public AudioClip setMagazine;
        public AudioClip cocking;

        [Header("魔法")]
        public AudioClip windMagic;

        [Header("受击")]
        [Tooltip("受击音效 — Damage_B（默认受击音效）")]
        public AudioClip damageB;
        [Tooltip("受击音效 — Damage_B_2")]
        public AudioClip damageB2;
        [Tooltip("死亡音效 — Damage_A_Dead")]
        public AudioClip damageADead;

        [Header("受击 - 特殊音效（扩展）")]
        [Tooltip("特殊敌人的标签（如 Boss）")]
        public string specialTag = "";
        [Tooltip("特殊敌人的受击音效（当攻击者标签匹配 specialTag 时播放）")]
        public AudioClip specialDamageSound;

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
            EditorAssignClips(onlyMissing: true);
#endif
        }

        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null || seSource == null) return;
            seSource.PlayOneShot(clip, volume);
        }

        // ============ 移动音效 ============
        public void PlayJump()
        {
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

        // ============ 近战音效 ============
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
            if (meleePlus != null) Play(meleePlus, 0.7f);
        }

        public void PlayMeleeSwing(int comboIndex)
        {
            switch (comboIndex)
            {
                case 1: Play(melee2 != null ? melee2 : swordSwing); break;
                case 2: Play(melee3 != null ? melee3 : swordSwing); break;
                default: Play(swordSwing); break;
            }
            if (meleePlus != null) Play(meleePlus, 0.7f);
        }

        /// <summary>
        /// Melee4 特效音效：Melee4_2 + Meleeplus + Melee4Afterwind + Attack4
        /// </summary>
        public void PlayMelee4()
        {
            Play(melee4 != null ? melee4 : swordSwing);
            if (meleePlus != null) Play(meleePlus, 0.7f);
            Play(melee4Afterwind != null ? melee4Afterwind : melee4After);
            if (attack4 != null) Play(attack4, 0.8f);
        }

        public void PlayMelee4After() => Play(melee4After != null ? melee4After : melee4Afterwind);
        public void PlayJumpAttackDown() => Play(jumpAttackDown != null ? jumpAttackDown : swordSwing);

        // ============ 枪械音效 ============
        public void PlayAiming() => Play(aiming);
        public void PlayGunFire() => Play(gunFire);
        public void PlayMagazineOut() => Play(removeMagazine != null ? removeMagazine : reload);
        public void PlaySetMagazine() => Play(setMagazine != null ? setMagazine : reload);
        public void PlayCocking() => Play(cocking != null ? cocking : reload);
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

        // ============ 魔法音效 ============
        public void PlayWindMagic() => Play(windMagic);

        // ============ 受击音效 ============
        public void PlayDamageB() => Play(damageB);
        public void PlayDamageADead() => Play(damageADead);

        public void PlayDamageByTag(string attackerTag)
        {
            if (!string.IsNullOrEmpty(specialTag) && attackerTag == specialTag && specialDamageSound != null)
            {
                Play(specialDamageSound);
            }
            else
            {
                PlayDamageB();
            }
        }

        // ============ 脚步声 ============
        public void TryPlayFootstep(float interval = 0.28f)
        {
            if (Time.time < _nextFootstep) return;
            _nextFootstep = Time.time + interval;
            _runToggle = 1 - _runToggle;
            Play(_runToggle == 0 ? run1 : run2);
        }

        // ============ 动画事件桥接 ============
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
                    PlayCocking();
                    break;
                case "SE_foldSMG":
                    PlayCocking();
                    break;
                case "Stepped_Forward":
                    Play(dashStepSound);
                    break;
                case "Damage_B":
                    PlayDamageB();
                    break;
                case "Damage_A_Dead":
                    PlayDamageADead();
                    break;
            }
        }

#if UNITY_EDITOR
        public void EditorAssignClips(bool onlyMissing)
        {
            void Set(ref AudioClip field, string fileName)
            {
                if (onlyMissing && field != null) return;
                var clip = FindClipByName(fileName);
                if (clip != null) field = clip;
            }

            // 移动
            Set(ref jump, "STEPS Dirt_ Run 03");
            Set(ref jumpTakeoff, "Jump1");
            Set(ref landing, "footstep_dirt_land_10");
            Set(ref backFlip, "Jump_BackFlip");
            Set(ref backFlipLand, "Jump_BackFlip_Land");
            Set(ref hardLanding, "EventLand");
            Set(ref run1, "Run1");
            Set(ref run2, "Run2");
            Set(ref backStep, "BackStep_0");
            Set(ref slide, "Run_Over");

            // 近战
            Set(ref swordSwing, "SwordSwing");
            Set(ref melee2, "Melee2");
            Set(ref melee3, "Melee3");
            Set(ref meleePlus, "Meleeplus");
            Set(ref attack1, "Attack");
            // ★ 修改：attack2 对应 Attack2（恢复原样）
            Set(ref attack2, "Attack2");
            // ★ 修改：attack3 对应 Attack3（恢复原样）
            Set(ref attack3, "Attack3");
            // ★ 修改：attack4 对应 Attack3-2
            Set(ref attack4, "Attack3-2");
            Set(ref melee4, "Melee4_2");
            Set(ref melee4Afterwind, "Melee4Afterwind");
            Set(ref melee4After, "Melee4After");
            Set(ref swordSheathe1, "SwordSheathe1");
            Set(ref swordSheathe, "SwordSheathe");
            Set(ref melee4Sheathe, "Melee4Sheathe");
            Set(ref jumpAttackDown, "Jump_Attack_Down");

            // 枪械
            Set(ref aiming, "Aiming");
            Set(ref gunFire, "ar15-223cal-surpressed-single-shot-processed-B");
            Set(ref reload, "SetMagazine");
            Set(ref removeMagazine, "RemoveMagazine");
            Set(ref setMagazine, "SetMagazine");
            Set(ref cocking, "Cocking");

            // 魔法
            Set(ref windMagic, "WindMagic");

            // 受击
            Set(ref damageB, "Damage_B");
            Set(ref damageB2, "Damage_B_2");
            Set(ref damageADead, "Damage_A_Dead");
        }

        private static AudioClip FindClipByName(string fileNameWithoutExt)
        {
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets(fileNameWithoutExt + " t:AudioClip"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == fileNameWithoutExt)
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            return null;
        }
#endif
    }
}