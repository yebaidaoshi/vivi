using UnityEngine;
using Spine;
using Spine.Unity;

namespace Player
{
    public class PlayerStruggleUI : MonoBehaviour
    {
        [Header("Spine 组件")]
        [SerializeField] private SkeletonAnimation skeletonAnimation;

        [Header("主动画 (轨道 0)")]
        [SerializeField] private string lockedAnim = "Locked";
        [SerializeField] private string shakeAnim = "Shake";
        [SerializeField] private string unlockAnim = "Unlock";

        [Header("骨骼名配置")]
        [SerializeField] private string barBoneName = "Gauge";
        [SerializeField] private string frameBoneName = "Gauge_Frame";
        [SerializeField] private string arrowLeftName = "Arrow_Left";
        [SerializeField] private string arrowRightName = "Arrow_Right";

        [Header("红条拉伸")]
        [Tooltip("红条居中补偿值。")]
        [SerializeField] private float barCenterOffsetX = 0.9f;

        [Header("黑框缩放 (Gauge_Frame)")]
        [SerializeField] private float frameScaleX_0 = 0.1f;
        [SerializeField] private float frameScaleX_100 = 1.0f;

        [Header("箭头位置 (绝对坐标)")]
        [SerializeField] private float leftArrowX_0 = -50f;
        [SerializeField] private float leftArrowX_100 = -115f;
        [SerializeField] private float rightArrowX_0 = 50f;
        [SerializeField] private float rightArrowX_100 = 115f;

        [Header("位置")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, 0f);
        [SerializeField] private bool followPlayerScale = false;

        private Transform _target;
        private bool _isUnlocking;
        private float _currentProgress;

        // 骨骼缓存
        private Bone _barBone;
        private Bone _frameBone;
        private Bone _arrowLeftBone;
        private Bone _arrowRightBone;
        private float _origBarX;

        private void Awake()
        {
            if (skeletonAnimation == null)
                skeletonAnimation = GetComponent<SkeletonAnimation>();
        }

        public void Show(Transform target)
        {
            _target = target;
            transform.position = _target.position + offset;
            if (followPlayerScale) transform.localScale = _target.localScale;
            
            gameObject.SetActive(true);
            _isUnlocking = false;

            var state = skeletonAnimation.AnimationState;
            state.SetAnimation(0, lockedAnim, false);
            state.AddAnimation(0, shakeAnim, true, 0f);

            var skeleton = skeletonAnimation.Skeleton;

            _barBone = skeleton.FindBone(barBoneName);
            if (_barBone != null) _origBarX = _barBone.X;

            _frameBone = skeleton.FindBone(frameBoneName);
            _arrowLeftBone = skeleton.FindBone(arrowLeftName);
            _arrowRightBone = skeleton.FindBone(arrowRightName);

            UpdateProgress(0f);
        }

        public void UpdateProgress(float progress)
        {
            if (_isUnlocking) return;
            // 缓存进度，在 LateUpdate 里每帧应用（防止被 Shake 动画覆盖）
            _currentProgress = Mathf.Clamp01(progress);
        }

        private void LateUpdate()
        {
            // 1. 跟随玩家位置
            if (_target != null)
            {
                transform.position = _target.position + offset;
                if (followPlayerScale) transform.localScale = _target.localScale;
            }

            if (_isUnlocking || skeletonAnimation == null) return;

            float p = _currentProgress;

            // 2. 强制覆盖红条缩放
            if (_barBone != null)
            {
                _barBone.ScaleX = p;
                _barBone.X = _origBarX + (1f - p) * barCenterOffsetX;
            }

            // 3. 强制覆盖黑框缩放
            if (_frameBone != null)
            {
                _frameBone.ScaleX = Mathf.Lerp(frameScaleX_0, frameScaleX_100, p);
            }

            // 4. 强制覆盖箭头位置
            if (_arrowLeftBone != null)
            {
                _arrowLeftBone.X = Mathf.Lerp(leftArrowX_0, leftArrowX_100, p);
            }
            if (_arrowRightBone != null)
            {
                _arrowRightBone.X = Mathf.Lerp(rightArrowX_0, rightArrowX_100, p);
            }

            // 5. 关键：因为我们在动画更新之后强行修改了骨骼数据，必须手动刷新 Spine 骨架，否则画面不会变化
            skeletonAnimation.Skeleton.UpdateWorldTransform();
        }

        public void PlayUnlock()
        {
            _isUnlocking = true;
            if (skeletonAnimation != null)
            {
                skeletonAnimation.AnimationState.SetAnimation(0, unlockAnim, false);
            }
            Destroy(gameObject, 1f);
        }
    }
}