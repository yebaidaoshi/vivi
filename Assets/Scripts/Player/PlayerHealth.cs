using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace Player
{
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerController _controller;
        private PlayerMotorSettings _settings;
        private PlayerAudio _audio;

        [Header("生命值")]
        [SerializeField] private int _maxHealth = 114;
        [SerializeField] private float hitStunDuration = 0.667f;
        [SerializeField] private float invincibleDuration = 1.5f;

        [Header("受击特效")]
        [Tooltip("受击时显示的 UI 图片（自动查找，无需手动拖拽）")]
        [SerializeField] private Image hitEffectImage;
        [Tooltip("特效显示时长（默认与硬直一致）")]
        [SerializeField] private float effectDuration = 0.667f;

        private int _currentHealth;
        private float _invincibleTimer;
        private bool _isDead;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;
        public bool IsDead => _isDead;
        public bool IsInvincible => _invincibleTimer > 0f;

        public System.Action<int, int> OnHealthChanged;
        public System.Action OnDeath;

        public void Init(PlayerContext context)
        {
            context.Bind(out _motor, out _anim, out _audio, out _settings);
            if (_settings != null && _settings.maxHealth > 0)
                _maxHealth = _settings.maxHealth;

            _currentHealth = _maxHealth;
            _isDead = false;
            _invincibleTimer = 0f;

            // ★ 自动查找受击特效 Image（如果未手动赋值）
            if (hitEffectImage == null)
            {
                // 查找场景中挂载了 UIFullscreenFit 的 Image 组件
                UIFullscreenFit fit = FindObjectOfType<UIFullscreenFit>();
                if (fit != null)
                {
                    hitEffectImage = fit.GetComponent<Image>();
                    // 确保初始不可见（Alpha = 0）
                    if (hitEffectImage != null)
                    {
                        Color c = hitEffectImage.color;
                        c.a = 0f;
                        hitEffectImage.color = c;
                    }
                }
                else
                {
                    Debug.LogWarning("[PlayerHealth] 未找到受击特效 Image，请确保场景中存在挂载 UIFullscreenFit 的 Image。");
                }
            }
        }

        public void SetController(PlayerController controller)
        {
            _controller = controller;
        }

        public void Tick()
        {
            if (_invincibleTimer > 0f)
                _invincibleTimer -= Time.deltaTime;
        }

        public void TakeDamage(int damage, Vector2 knockback, GameObject attacker)
        {
            if (_isDead || IsInvincible) return;

            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            _invincibleTimer = invincibleDuration;

            if (_motor != null)
                _motor.SetVelocity(Vector2.zero);

            if (_controller != null)
            {
                _controller.Locked = true;
                StartCoroutine(UnlockAfterHitStun());
                _controller.ResetCombo();
            }

            if (_anim != null && !_isDead)
                _anim.ForcePlay("Damage_A");

            // ★ 受击特效（通过 Alpha 显示）
            if (hitEffectImage != null)
            {
                Color c = hitEffectImage.color;
                c.a = 1f;                      // 完全可见
                hitEffectImage.color = c;
                StopCoroutine(nameof(HideEffect));
                StartCoroutine(HideEffect());
            }

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0)
                Die();
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;
            OnDeath?.Invoke();
            if (_controller != null)
                _controller.Locked = true;
            if (_anim != null)
                _anim.ForcePlay("Shirimochi_Dead");
        }

        public void Heal(int amount)
        {
            if (_isDead) return;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        private IEnumerator UnlockAfterHitStun()
        {
            yield return new WaitForSeconds(hitStunDuration);
            if (_controller != null && !_isDead)
                _controller.Locked = false;
        }

        // ★ 特效隐藏协程（通过 Alpha 淡出）
        private IEnumerator HideEffect()
        {
            yield return new WaitForSeconds(effectDuration);
            if (hitEffectImage != null)
            {
                Color c = hitEffectImage.color;
                c.a = 0f;
                hitEffectImage.color = c;
            }
        }
    }
}