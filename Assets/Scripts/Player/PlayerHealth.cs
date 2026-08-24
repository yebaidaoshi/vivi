using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Player
{
    [System.Serializable]
    public class DamageTypeMapping
    {
        public string type = "Enemy_Small";
        public string animationName = "Damage_A";
        public List<AudioClip> audioClips = new List<AudioClip>();
    }

    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        private PlayerMotor _motor;
        private PlayerAnimDriver _anim;
        private PlayerController _controller;
        private PlayerMotorSettings _settings;
        private PlayerAudio _audio;

        [Header("生命值")]
        [SerializeField] private int _maxHealth = 150;
        [SerializeField] private float invincibleDuration = 1.5f;
        [SerializeField] private float hitStunDuration = 0.667f;

        [Header("受击类型映射")]
        [SerializeField]
        private List<DamageTypeMapping> typeMappings = new List<DamageTypeMapping>()
        {
            new DamageTypeMapping
            {
                type = "Enemy_Small",
                animationName = PlayerAnimDriver.States.Damage_A,
                audioClips = new List<AudioClip>()
            }
        };

        [Header("受击特效")]
        [SerializeField] private Image hitEffectImage;
        [SerializeField] private float effectDuration = 0.667f;

        [Header("半血 Idle_Damage_A 循环")]
        [SerializeField] private string idleDamageAnim = PlayerAnimDriver.States.Idle_Damage_A;
        [SerializeField] private float idleEnterThreshold = 1f;

        [Header("死亡 UI")]
        [SerializeField] private GameObject deathUIPanel;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private Dictionary<string, string> _attackerTypeMap = new Dictionary<string, string>();

        private int _currentHealth;
        private float _invincibleTimer;
        private bool _isDead;
        private bool _isInIdleDamage = false;
        private bool _isInHitStun = false;
        private bool _isHitAnimationPlaying = false;
        private Coroutine _hitStunCoroutine;
        private Coroutine _hitAnimationCoroutine;
        private string _currentHitAnimation = "";
        private float _idleTimer = 0f;

        // ★ 新增：防止重复触发返回菜单
        private bool _returningToMenu = false;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;
        public bool IsDead => _isDead;
        public bool IsInvincible => _invincibleTimer > 0f;
        public bool IsInHitStun => _isInHitStun;
        public bool IsInIdleDamage => _isInIdleDamage;
        public bool IsHitAnimationPlaying => _isHitAnimationPlaying;

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
            _isInIdleDamage = false;
            _isInHitStun = false;
            _isHitAnimationPlaying = false;
            _idleTimer = 0f;
            _returningToMenu = false;

            if (_audio != null)
            {
                foreach (var mapping in typeMappings)
                {
                    if (mapping.audioClips == null || mapping.audioClips.Count < 2)
                    {
                        AudioClip clip1 = _audio.damageB;
                        AudioClip clip2 = _audio.damageB2 != null ? _audio.damageB2 : _audio.damageB;
                        mapping.audioClips = new List<AudioClip> { clip1, clip2 };
                    }
                }
            }

            if (hitEffectImage == null)
            {
                UIFullscreenFit fit = FindObjectOfType<UIFullscreenFit>();
                if (fit != null)
                {
                    hitEffectImage = fit.GetComponent<Image>();
                    if (hitEffectImage != null)
                    {
                        Color c = hitEffectImage.color;
                        c.a = 0f;
                        hitEffectImage.color = c;
                    }
                }
            }

            if (deathUIPanel != null)
                deathUIPanel.SetActive(false);
        }

        public void SetController(PlayerController controller)
        {
            _controller = controller;
        }

        public void RegisterAttacker(GameObject attacker, string type)
        {
            if (attacker == null) return;
            string key = attacker.name;
            if (!_attackerTypeMap.ContainsKey(key))
                _attackerTypeMap[key] = type;
        }

        private string GetAttackerType(GameObject attacker)
        {
            if (attacker == null) return "Enemy_Small";
            string key = attacker.name;
            if (_attackerTypeMap.TryGetValue(key, out string type))
                return type;
            return "Enemy_Small";
        }

        private DamageTypeMapping GetMappingForType(string type)
        {
            foreach (var mapping in typeMappings)
                if (mapping.type == type) return mapping;
            return typeMappings.Count > 0 ? typeMappings[0] : null;
        }

        private AudioClip GetRandomAudio(DamageTypeMapping mapping)
        {
            if (mapping == null || mapping.audioClips == null || mapping.audioClips.Count == 0)
                return null;
            int index = Random.Range(0, mapping.audioClips.Count);
            return mapping.audioClips[index];
        }

        private bool HasPlayerInput()
        {
            if (_controller == null) return false;
            var intent = _controller.Intent;
            return Mathf.Abs(intent.Move) > 0.1f
                || intent.Jump
                || intent.Slash
                || intent.WantsAds
                || intent.Evade
                || intent.Reload
                || intent.Skill;
        }

        private bool IsHalfHealth() => _currentHealth <= _maxHealth / 2;

        public void Tick()
        {
            if (_invincibleTimer > 0f)
                _invincibleTimer -= Time.deltaTime;

            // 受击动画打断
            if (_isHitAnimationPlaying && HasPlayerInput())
            {
                InterruptHitAnimation();
            }

            if (_isInIdleDamage && HasPlayerInput())
            {
                ExitIdleDamage();
                _idleTimer = 0f;
                return;
            }

            if (_isInHitStun || _isHitAnimationPlaying)
                return;

            bool isIdle = _anim != null && _anim.IsPlaying(PlayerAnimDriver.States.Idle);
            bool hasInput = HasPlayerInput();

            if (isIdle && !hasInput)
                _idleTimer += Time.deltaTime;
            else
                _idleTimer = 0f;

            if (IsHalfHealth() && !_isInHitStun && !_isInIdleDamage && isIdle && _idleTimer >= idleEnterThreshold)
            {
                _anim?.CrossFade(idleDamageAnim, 0.1f);
                _isInIdleDamage = true;
                _idleTimer = 0f;
            }

            // ★★★ 死亡后按任意键返回主菜单 ★★★
            if (_isDead && deathUIPanel != null && deathUIPanel.activeSelf && !_returningToMenu)
            {
                if (Input.anyKeyDown)
                {
                    _returningToMenu = true;
                    ReturnToMainMenu();
                }
            }
        }

        private void InterruptHitAnimation()
        {
            if (!_isHitAnimationPlaying) return;
            _isHitAnimationPlaying = false;
            if (_hitAnimationCoroutine != null)
            {
                StopCoroutine(_hitAnimationCoroutine);
                _hitAnimationCoroutine = null;
            }
        }

        private void ExitIdleDamage()
        {
            if (!_isInIdleDamage) return;
            _isInIdleDamage = false;
            _idleTimer = 0f;
        }

        public void TakeDamage(int damage, Vector2 knockback, GameObject attacker)
        {
            if (_isDead || IsInvincible) return;

            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            _invincibleTimer = invincibleDuration;

            if (_isInIdleDamage)
                ExitIdleDamage();

            _idleTimer = 0f;

            if (_motor != null)
                _motor.SetVelocity(Vector2.zero);

            if (_controller != null)
            {
                _controller.Locked = true;
                _isInHitStun = true;
                _isHitAnimationPlaying = true;
                if (_hitStunCoroutine != null)
                    StopCoroutine(_hitStunCoroutine);
                _hitStunCoroutine = StartCoroutine(UnlockAfterHitStun());
                _controller.ResetCombo();
            }

            string type = GetAttackerType(attacker);
            var mapping = GetMappingForType(type);
            string animName = mapping != null ? mapping.animationName : PlayerAnimDriver.States.Damage_A;
            _currentHitAnimation = animName;

            if (_anim != null && !_isDead)
                _anim.ForcePlay(animName);

            if (_audio != null && mapping != null)
            {
                AudioClip clip = GetRandomAudio(mapping);
                if (clip != null)
                    _audio.Play(clip);
                else
                    _audio.PlayDamageB();
            }

            if (hitEffectImage != null)
            {
                Color c = hitEffectImage.color;
                c.a = 1f;
                hitEffectImage.color = c;
                StopCoroutine(nameof(HideEffect));
                StartCoroutine(HideEffect());
            }

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0)
                Die();
        }

        private IEnumerator UnlockAfterHitStun()
        {
            yield return new WaitForSeconds(hitStunDuration);
            if (_controller != null && !_isDead)
                _controller.Locked = false;
            _isInHitStun = false;
            _hitStunCoroutine = null;

            if (_anim != null && !_isDead && !string.IsNullOrEmpty(_currentHitAnimation))
            {
                _hitAnimationCoroutine = StartCoroutine(WaitForHitAnimationEnd());
            }
        }

        private IEnumerator WaitForHitAnimationEnd()
        {
            while (_isHitAnimationPlaying && _anim != null && !_isDead && !string.IsNullOrEmpty(_currentHitAnimation) && _anim.IsPlaying(_currentHitAnimation))
            {
                yield return null;
            }
            _isHitAnimationPlaying = false;
            _hitAnimationCoroutine = null;
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;
            OnDeath?.Invoke();

            if (_controller != null)
                _controller.Locked = true;

            if (_anim != null)
                _anim.ForcePlay(PlayerAnimDriver.States.Shirimochi_Dead);

            _audio?.PlayDamageADead();

            ShowDeathUI();
        }

        private void ShowDeathUI()
        {
            if (deathUIPanel != null)
                deathUIPanel.SetActive(true);
            // 若需暂停游戏，取消注释下一行：
            // Time.timeScale = 0f;
        }

        public void ReturnToMainMenu()
        {
            // Time.timeScale = 1f; // 若暂停则恢复
            StartCoroutine(LoadMainMenuCoroutine());
        }

        private IEnumerator LoadMainMenuCoroutine()
        {
            if (ScreenFader.Instance != null)
                yield return StartCoroutine(ScreenFader.Instance.FadeOutWithLoading());
            else
                yield return null;

            AsyncOperation async = SceneManager.LoadSceneAsync(mainMenuSceneName);
            while (!async.isDone)
                yield return null;

            if (ScreenFader.Instance != null)
                yield return StartCoroutine(ScreenFader.Instance.FadeInAndHideLoading());

            // 重置标志（场景切换后物体销毁，无需重置，但保留以防）
            _returningToMenu = false;
        }

        public void Heal(int amount)
        {
            if (_isDead) return;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

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