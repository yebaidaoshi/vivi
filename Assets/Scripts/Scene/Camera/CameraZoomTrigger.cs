using UnityEngine;

namespace SceneCamera
{
    /// <summary>
    /// 区域缩放触发器：玩家进入时把正交尺寸改到 ZoomSize，离开时恢复。
    /// 对应原版的 ZoomKey 物体 + GetCameraOrthoSize / FloatOperator 那套 FSM。
    ///
    /// 需要：Collider2D（Is Trigger = true）+ CameraDirector 引用。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class CameraZoomTrigger : MonoBehaviour
    {
        [SerializeField] private CameraDirector director;
        [SerializeField] private float zoomSize = 12f;
        [SerializeField] private float damping = 0.4f;
        [SerializeField] private bool restoreOnExit = true;
        [SerializeField] private bool requirePlayerTag = true;
        [SerializeField] private string playerTag = "Player";

        private float previousSize = -1f;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null && !col.isTrigger) col.isTrigger = true;

            if (director == null) director = FindObjectOfType<CameraDirector>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (director == null) return;
            if (!IsPlayer(other)) return;

            if (previousSize < 0f && director.Camera != null) previousSize = director.Camera.orthographicSize;
            director.SetZoom(zoomSize, damping);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (director == null) return;
            if (!IsPlayer(other)) return;
            if (restoreOnExit && previousSize > 0f) director.SetZoom(previousSize, damping);
        }

        private bool IsPlayer(Collider2D other)
        {
            if (!requirePlayerTag) return true;
            if (other == null) return false;
            if (other.CompareTag(playerTag)) return true;
            return other.transform.root != null && other.transform.root.CompareTag(playerTag);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            var col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.offset, box.size);
            }
        }
    }
}
