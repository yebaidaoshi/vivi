using UnityEngine;

namespace Enemy
{
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimDriver : MonoBehaviour
    {
        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void PlayState(string stateName, float normalizedTime = 0f)
        {
            if (animator != null)
                animator.Play(stateName, 0, normalizedTime);
        }

        public bool IsPlaying(string stateName)
        {
            if (animator == null) return false;
            return animator.GetCurrentAnimatorStateInfo(0).IsName(stateName);
        }

        public void SetTrigger(string name)
        {
            if (animator != null)
                animator.SetTrigger(name);
        }

        public void SetFloat(string name, float value)
        {
            if (animator != null)
                animator.SetFloat(name, value);
        }

        // =========================================================
        // ★ 动画冻结（停在最后一帧）
        // =========================================================
        public void FreezeAnimator()
        {
            if (animator != null)
                animator.speed = 0f;
        }

        public void ResumeAnimator()
        {
            if (animator != null)
                animator.speed = 1f;
        }
    }
}