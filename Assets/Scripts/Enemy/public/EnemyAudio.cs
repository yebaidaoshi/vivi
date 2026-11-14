using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人音效（原版各状态 AudioPlay 的集中收口）。
    ///
    /// 原版 _HyenaParent 实际引用的音频：
    ///   Hyena_VClaw（挥爪喊声）、Hyena_Vdeath（死亡）、Hyena_Slide（滑步/逃跑）、
    ///   Damage1（普通受击）、MeleeCritical（暴击受击）、Break1（破甲/眩晕）、
    ///   Spotted（发现玩家）、Mounted_Hyena_Loop（束缚循环）、Hyena_VJumpAttack1/2（跳攻，已不用）
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class EnemyAudio : MonoBehaviour
    {
        [Header("语音 / 动作")]
        public AudioClip attackWindup;      // Hyena_VClaw
        public AudioClip attackHit;         // Hyena_Clawhit（爪击命中玩家）
        public AudioClip jumpAttack;        // Hyena_VJumpAttack1（跳攻已移除，可留空）
        public AudioClip death;             // Hyena_Vdeath
        public AudioClip flee;              // Hyena_Slide
        public AudioClip spotted;           // Spotted（发现玩家）

        [Header("受击")]
        public AudioClip hurt;              // Damage1
        public AudioClip hurtCritical;      // MeleeCritical
        public AudioClip stun;              // Break1

        [Header("束缚")]
        public AudioClip mountStart;        // Hyena_VJumpAttack2（发动束缚瞬间）
        public AudioClip mountLoop;         // Hyena_JumpAttackLoop（束缚过程中循环）

        [Header("音量")]
        [Range(0f, 1f)] public float volume = 0.8f;

        private AudioSource _source;
        private AudioSource _loopSource;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            // 束缚循环用独立音源，避免和一次性音效互相打断
            _loopSource = gameObject.AddComponent<AudioSource>();
            _loopSource.playOnAwake = false;
            _loopSource.loop = true;
            _loopSource.spatialBlend = 0f;
            _loopSource.volume = volume;
        }

        private void Play(AudioClip clip, float pitch = 1f)
        {
            if (clip == null || _source == null) return;
            _source.pitch = pitch;
            _source.PlayOneShot(clip, volume);
        }

        public void PlayHurt() => Play(hurt, Random.Range(0.95f, 1.05f));
        public void PlayHurtCritical() => Play(hurtCritical, Random.Range(0.95f, 1.05f));
        public void PlayDeath() => Play(death);
        public void PlayAttackWindup() => Play(attackWindup, Random.Range(0.97f, 1.03f));
        public void PlayAttackHit() => Play(attackHit);
        public void PlayStun() => Play(stun);
        public void PlayFlee() => Play(flee);
        public void PlaySpotted() => Play(spotted);
        public void PlayJumpAttack() => Play(jumpAttack, Random.Range(0.96f, 1.04f));

        public void PlayMountStart() => Play(mountStart, Random.Range(0.97f, 1.03f));

        public void PlayMountLoop()
        {
            if (mountLoop == null || _loopSource == null) return;
            if (_loopSource.isPlaying) return;
            _loopSource.clip = mountLoop;
            _loopSource.volume = volume;
            _loopSource.Play();
        }

        public void StopMountLoop()
        {
            if (_loopSource != null && _loopSource.isPlaying) _loopSource.Stop();
        }
    }
}
