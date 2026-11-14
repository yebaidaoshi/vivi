using System.Collections;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 顿帧（原版 TimeControl FSM 的 ScaleClockTime + Wait 等效实现）。
    /// 命中时把全局时间压到接近 0，几十毫秒后恢复。
    /// </summary>
    public class EnemyHitStop : MonoBehaviour
    {
        [SerializeField] private float duration = 0.055f;
        [Range(0f, 1f)]
        [SerializeField] private float timeScale = 0.05f;
        [SerializeField] private float cooldown = 0.05f;

        private Coroutine _routine;
        private float _lastPlayTime = -10f;

        /// <summary>触发一次顿帧（duration/scale 传负数则用默认值）。</summary>
        public void Play(float customDuration = -1f, float customScale = -1f)
        {
            if (Time.unscaledTime - _lastPlayTime < cooldown) return;
            _lastPlayTime = Time.unscaledTime;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(
                customDuration > 0f ? customDuration : duration,
                customScale >= 0f ? customScale : timeScale));
        }

        private IEnumerator Run(float dur, float scale)
        {
            float original = Time.timeScale;
            Time.timeScale = scale;

            yield return new WaitForSecondsRealtime(dur);

            Time.timeScale = original;
            _routine = null;
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
                Time.timeScale = 1f;
            }
        }
    }
}
