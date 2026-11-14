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

        public bool HasController => animator != null && animator.runtimeAnimatorController != null;

        private bool _spineFixed;

        private void Start()
        {
            FixSpineMecanim();
        }

        /// <summary>
        /// 和 HeroineSpineMecanimFix 同样的修法：spine 3.8 的 SkeletonMecanim 经常
        /// Translator.Animator 为空，导致 Animator 在切状态但骨架完全不动（角色"平移"）。
        /// </summary>
        private void FixSpineMecanim()
        {
            if (_spineFixed) return;
            _spineFixed = true;

            var mecanim = GetComponent<Spine.Unity.SkeletonMecanim>();
            if (mecanim == null || animator == null) return;

            try
            {
                if (animator.runtimeAnimatorController != null)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }

                mecanim.Initialize(true);

                if (mecanim.Translator != null && mecanim.Translator.Animator == null)
                {
                    mecanim.Translator.Initialize(animator, mecanim.skeletonDataAsset);
                    Debug.Log("[EnemyAnimDriver] SkeletonMecanim.Translator 重新绑定完成", this);
                }

                AuditClips(mecanim);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[EnemyAnimDriver] Spine 修复失败: " + e.Message, this);
            }
        }

        private void AuditClips(Spine.Unity.SkeletonMecanim mecanim)
        {
            if (mecanim.skeletonDataAsset == null || animator == null || animator.runtimeAnimatorController == null) return;

            var data = mecanim.skeletonDataAsset.GetSkeletonData(true);
            if (data == null) return;

            var spineNames = new System.Collections.Generic.HashSet<string>();
            foreach (var anim in data.Animations) spineNames.Add(anim.Name);

            int matched = 0;
            var missing = new System.Collections.Generic.List<string>();
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                if (spineNames.Contains(clip.name)) matched++;
                else missing.Add(clip.name);
            }

            Debug.Log("[EnemyAnimDriver] Spine 动画匹配: " + matched + " 个命中, " + missing.Count + " 个缺失"
                + (missing.Count > 0 ? "  缺失示例: " + string.Join(", ", missing.GetRange(0, Mathf.Min(8, missing.Count)).ToArray()) : ""), this);
        }

        public string CurrentClipName
        {
            get
            {
                if (animator == null) return "null";
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                return clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "(none)";
            }
        }

        public float CurrentNormalizedTime
        {
            get
            {
                if (animator == null) return 0f;
                var info = animator.GetCurrentAnimatorStateInfo(0);
                return info.normalizedTime % 1f;
            }
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