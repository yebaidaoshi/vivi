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
                    mecanim.Translator.Initialize(animator, mecanim.skeletonDataAsset);                }

                AuditClips(mecanim);
            }
            catch (System.Exception e)
            {            }
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
            }        }

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

        private readonly System.Collections.Generic.Dictionary<string, int> _stateLayers =
            new System.Collections.Generic.Dictionary<string, int>();

        /// <summary>在任意层查找该状态，返回层号；找不到返回 -1。</summary>
        public int FindStateLayer(string stateName)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return -1;
            if (_stateLayers.TryGetValue(stateName, out int cached)) return cached;

            int hash = Animator.StringToHash(stateName);
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                if (animator.HasState(layer, hash))
                {
                    _stateLayers[stateName] = layer;
                    return layer;
                }
            }

            return -1;   // 不缓存未找到，控制器可能还没初始化
        }

        public void PlayState(string stateName, float normalizedTime = 0f)
        {
            if (animator == null) return;

            int layer = FindStateLayer(stateName);
            if (layer < 0)
            {
                if (_warnedMissing == null) _warnedMissing = new System.Collections.Generic.HashSet<string>();
                if (_warnedMissing.Add(stateName))
                    Debug.LogError("[EnemyAnimDriver] Controller 里找不到状态 '" + stateName + "'（共 " + animator.layerCount + " 层）", this);
                return;
            }

            animator.Play(stateName, layer, normalizedTime);
        }

        private System.Collections.Generic.HashSet<string> _warnedMissing;

        public bool IsPlaying(string stateName)
        {
            if (animator == null) return false;

            int layer = FindStateLayer(stateName);
            if (layer < 0) layer = 0;
            return animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName);
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
        public bool IsFrozen => animator != null && animator.speed <= 0.001f;

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