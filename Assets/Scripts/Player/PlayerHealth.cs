using UnityEngine;
using System.Collections;
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
        [SerializeField] private int _maxHealth =114;
        [SerializeField] private float _invincibleTimer = 0.2f;

        

        private int _currentHealth;
        private float _invincibleTime;
        private bool _isDead;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;
        public bool IsDead => _isDead;
        public bool IsInvincible => _invincibleTimer > 0f;

        public System.Action<int, int> OnHealthChanged; // newHealth, maxHealth
        public System.Action OnDeath;

       
        public void Init(PlayerContext context)
        {
            context.Bind(out _motor, out _anim, out _audio, out _settings);
            if (_settings != null && _settings.maxHealth > 0)
            {
                _maxHealth = _settings.maxHealth;
                _invincibleTime = _settings.invincibleTime;
            }
            _currentHealth = _maxHealth;
            _isDead = false;
            _invincibleTimer = 0f;        
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
            _invincibleTimer = _invincibleTime;
         
            if (_motor != null)
            {
                Vector2 v = _motor.GetVelocity();
                v.x = 0f;          
                _motor.SetVelocity(v);
            }
            if (_controller != null)
            {
                _controller.Locked = true;
                StartCoroutine(UnlockAfterHitStun());
            }

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            if (_currentHealth <= 0)
            {
                Die();
            }

        }
        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            OnDeath?.Invoke();

            if (_controller != null)
                _controller.Locked = true;
            if (_anim != null)
            {
                _anim.ForcePlay("Damage_A_Dead");
            }
        }
        public void Heal(int amount)
            {
                if (_isDead) return;
                _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
                OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            }
        private IEnumerator UnlockAfterHitStun()
        {
            yield return new WaitForSeconds(_invincibleTime);
            if (_controller != null && !_isDead)
            {
                _controller.Locked = false;
            }
        }


    }

}









