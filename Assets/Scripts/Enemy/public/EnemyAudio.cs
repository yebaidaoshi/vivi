using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人音效（原版各状态里的 AudioPlay 的集中收口）。
    /// 先手动拖 Clip，或用菜单 Tools/Vivi/敌人/自动匹配音效 从 Assets/Audio/Clips 里按名字扫。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class EnemyAudio : MonoBehaviour
    {
        [Header("Clip")]
        public AudioClip hurt;
        public AudioClip death;
        public AudioClip attackWindup;
        public AudioClip attackHit;
        public AudioClip stun;
        public AudioClip flee;
        public AudioClip jumpAttack;

        [Header("音量")]
        [Range(0f, 1f)] public float volume = 0.8f;

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
        }

        private void Play(AudioClip clip, float pitch = 1f)
        {
            if (clip == null || _source == null) return;
            _source.pitch = pitch;
            _source.PlayOneShot(clip, volume);
        }

        public void PlayHurt() => Play(hurt, Random.Range(0.95f, 1.05f));
        public void PlayDeath() => Play(death);
        public void PlayAttackWindup() => Play(attackWindup);
        public void PlayAttackHit() => Play(attackHit);
        public void PlayStun() => Play(stun);
        public void PlayFlee() => Play(flee);
        public void PlayJumpAttack() => Play(jumpAttack);
    }
}
