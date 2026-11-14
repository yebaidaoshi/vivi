using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 硬直 / 护甲 / 破甲（原版 Flinching FSM 的等效实现）。
    ///
    /// 规则：
    ///   - 有护甲点数时，普通命中只扣护甲、不打断动作（原版 GoArmor / SuperArmor）
    ///   - 护甲扣光 → 破甲：播 Break 动画 + 发 BREAK 事件（进入 AI 的 Break 状态）
    ///   - 没护甲 → 按受击方向播 Hit_Up / Hit_Down，并发 FLINCH 给 AI
    ///   - 弱点（critical）无视护甲，直接硬直
    ///   - 一段时间没受击，护甲恢复
    /// </summary>
    public class EnemyFlinching : MonoBehaviour
    {
        [Header("护甲（霸体）")]
        [Tooltip("能挡几次普通命中")]
        [SerializeField] private int armorHits = 3;
        [Tooltip("最后一次受击后多久恢复护甲")]
        [SerializeField] private float armorRegenDelay = 6f;
        [Tooltip("护甲生效时的动画/表现（可留空）")]
        [SerializeField] private string armorOnAnim = "SuperArmor";
        [Tooltip("破甲动画")]
        [SerializeField] private string breakAnim = "Break";

        [Header("硬直")]
        [SerializeField] private string hitUpAnim = "Hit_Up1";
        [SerializeField] private string hitDownAnim = "Hit_Down1";
        [Tooltip("硬直持续时间（秒）")]
        [SerializeField] private float flinchTime = 0.35f;
        [SerializeField] private float heavyHitThreshold = 25f;

        private EnemyMotor _motor;
        private EnemyAnimDriver _anim;
        private EnemyBrainBase _brain;

        private int _armor;
        private float _regenTimer;

        public bool HasArmor => _armor > 0;
        public int Armor => _armor;

        public void Init(EnemyMotor motor, EnemyAnimDriver anim, EnemyBrainBase brain)
        {
            _motor = motor;
            _anim = anim;
            _brain = brain;
            _armor = armorHits;
            _regenTimer = 0f;
        }

        /// <summary>返回 true 表示被护甲吸收（调用方不要再触发普通硬直）。</summary>
        public bool OnHit(int damage, GameObject attacker, bool critical)
        {
            _regenTimer = armorRegenDelay;

            // 弱点/重击直接破甲并硬直
            if (critical)
            {
                if (_armor > 0) BreakArmor();
                PlayFlinch(attacker, heavy: true);
                return false;
            }

            if (_armor > 0)
            {
                _armor--;
                if (_armor <= 0) BreakArmor();
                return true;
            }

            PlayFlinch(attacker, damage >= heavyHitThreshold);
            return false;
        }

        private void BreakArmor()
        {
            _armor = 0;
            if (_anim != null && !string.IsNullOrEmpty(breakAnim)) _anim.PlayState(breakAnim);
            if (_brain != null) _brain.OnAnimEvent("BREAK");
        }

        private void PlayFlinch(GameObject attacker, bool heavy)
        {
            if (_anim == null) return;

            bool fromFront = true;
            if (attacker != null && _motor != null)
            {
                float dx = attacker.transform.position.x - transform.position.x;
                fromFront = (dx >= 0f) == (_motor.Facing > 0);
            }

            string clip = fromFront ? hitUpAnim : hitDownAnim;
            if (heavy && !string.IsNullOrEmpty(hitDownAnim)) clip = hitDownAnim;

            if (!string.IsNullOrEmpty(clip)) _anim.PlayState(clip);
            if (_brain != null) _brain.OnAnimEvent("FLINCH");
        }

        private void Update()
        {
            if (_armor >= armorHits) return;

            if (_regenTimer > 0f)
            {
                _regenTimer -= Time.deltaTime;
                return;
            }

            _armor = Mathf.Min(armorHits, _armor + 1);
            _regenTimer = armorRegenDelay;
        }
    }
}
