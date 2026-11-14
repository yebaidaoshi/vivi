using SceneCamera;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 敌人交战时把镜头拉过去（原版 PutMeInCamera FSM + PC2DAddCameraTarget）。
    /// 挂在敌人根节点即可，无需其它系统配合。
    /// </summary>
    public class EnemyCameraLink : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("进入这个距离开始参与镜头构图")]
        [SerializeField] private float engageRange = 18f;
        [Tooltip("超过这个距离解除构图")]
        [SerializeField] private float releaseRange = 26f;
        [Tooltip("构图权重（原版 PC2DAddCameraTarget 的 Weight）")]
        [SerializeField] private float weight = 1f;

        private CameraDirector _director;
        private bool _engaged;

        private void Start()
        {
            _director = CameraDirector.Instance;
            if (_director == null) _director = FindObjectOfType<CameraDirector>();

            if (target == null)
            {
                var pc = FindObjectOfType<Player.PlayerController>();
                if (pc != null) target = pc.transform;
            }
        }

        private void Update()
        {
            if (_director == null || target == null) return;

            float d = Mathf.Abs(target.position.x - transform.position.x);

            if (!_engaged && d <= engageRange)
            {
                _engaged = true;
                _director.AddCombatTarget(transform, weight);
            }
            else if (_engaged && d > releaseRange)
            {
                _engaged = false;
                _director.RemoveCombatTarget(transform);
            }
        }

        public void Release()
        {
            if (_director != null && _engaged) _director.RemoveCombatTarget(transform);
            _engaged = false;
        }

        private void OnDisable() => Release();
    }
}
