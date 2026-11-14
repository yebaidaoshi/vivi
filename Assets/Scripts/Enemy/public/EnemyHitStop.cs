using System.Collections;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 顿帧（原版 TimeControl FSM 的 ScaleClockTime 等效）。
    ///
    /// 注意：这是全局 timeScale，一定要能可靠恢复，否则连续命中会把游戏越拖越慢。
    /// 现在只在首次触发时记录原始 timeScale，之后所有触发都恢复到同一个值。
    /// </summary>
    public class EnemyHitStop : MonoBehaviour
    {
        [SerializeField] private float duration = 0.05f;
        [Range(0f, 1f)]
        [SerializeField] private float timeScale = 0.15f;
        [Tooltip("两次顿帧之间的最小间隔，防止自动武器把时间一直压在慢速")]
        [SerializeField] private float cooldown = 0.18f;

        private static float _savedTimeScale = -1f;
        private static int _activeCount;

        private Coroutine _routine;
        private float _lastPlayTime = -10f;

        public void Play(float customDuration = -1f, float customScale = -1f)
        {
            if (Time.unscaledTime - _lastPlayTime < cooldown) return;
            _lastPlayTime = Time.unscaledTime;

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
                EndOne();
            }

            _routine = StartCoroutine(Run(
                customDuration > 0f ? customDuration : duration,
                customScale >= 0f ? customScale : timeScale));
        }

        private IEnumerator Run(float dur, float scale)
        {
            // 只在没有任何顿帧在跑时记录原始值
            if (_savedTimeScale < 0f) _savedTimeScale = Time.timeScale;
            _activeCount++;

            Time.timeScale = Mathf.Min(scale, _savedTimeScale);

            yield return new WaitForSecondsRealtime(dur);

            EndOne();
            _routine = null;
        }

        private static void EndOne()
        {
            _activeCount = Mathf.Max(0, _activeCount - 1);
            if (_activeCount == 0)
            {
                if (_savedTimeScale >= 0f) Time.timeScale = _savedTimeScale;
                _savedTimeScale = -1f;
            }
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            EndOne();
        }
    }
}
