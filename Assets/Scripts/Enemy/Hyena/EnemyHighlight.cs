using UnityEngine;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace Enemy
{
    public class EnemyHighlight : MonoBehaviour
    {
        [Header("头顶警告光")]
        [SerializeField] private SpriteRenderer warningLight;
        [SerializeField] private float warningFadeInTime = 0.15f;
        [SerializeField] private float warningFadeOutTime = 0.1f;
        [SerializeField] private float warningPopStart = 0.5f;
        [SerializeField] private float warningPopOvershoot = 1.2f;
        [SerializeField] private float warningBreathAmount = 0.1f;
        [SerializeField] private float warningBreathSpeed = 6f;

        [Header("粉色描边")]
        [Tooltip("Spine 描边材质（Spine/Outline/Skeleton 或类似）。")]
        [SerializeField] private Material pinkOutlineMaterial;
        [Tooltip("描边层相对主渲染器的 sortingOrder 偏移。")]
        [SerializeField] private int outlineSortingOrderOffset = 1;

        [Header("调试")]
        [SerializeField] private bool debugOutline = false;

        private SkeletonRenderer skeletonRenderer;
        private MeshFilter mainMeshFilter;
        private MeshRenderer mainMeshRenderer;

        private Transform outlineLayer;
        private MeshFilter outlineMeshFilter;
        private MeshRenderer outlineMeshRenderer;

        private bool outlineActive;
        private Coroutine warningCo;
        private Vector3 warningBaseScale = Vector3.one;

        private void Awake()
        {
            skeletonRenderer = GetComponent<SkeletonRenderer>();
            mainMeshFilter = GetComponent<MeshFilter>();
            mainMeshRenderer = GetComponent<MeshRenderer>();

            if (warningLight != null)
            {
                warningBaseScale = warningLight.transform.localScale;
                SetWarningAlpha(0f);
                warningLight.gameObject.SetActive(false);
            }

            EnsureOutlineLayer();
        }

        // =========================================================
        // 创建/复用描边层
        // =========================================================
        private void EnsureOutlineLayer()
        {
            if (pinkOutlineMaterial == null) return;

            // 找现成的
            var existing = transform.Find("OutlineLayer");
            if (existing != null)
                outlineLayer = existing;
            else
            {
                var go = new GameObject("OutlineLayer");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                outlineLayer = go.transform;
            }

            outlineMeshFilter = outlineLayer.GetComponent<MeshFilter>();
            if (outlineMeshFilter == null)
                outlineMeshFilter = outlineLayer.gameObject.AddComponent<MeshFilter>();

            outlineMeshRenderer = outlineLayer.GetComponent<MeshRenderer>();
            if (outlineMeshRenderer == null)
                outlineMeshRenderer = outlineLayer.gameObject.AddComponent<MeshRenderer>();

            // 复制主渲染器的 sorting 设置
            if (mainMeshRenderer != null)
            {
                outlineMeshRenderer.sortingLayerID = mainMeshRenderer.sortingLayerID;
                outlineMeshRenderer.sortingOrder = mainMeshRenderer.sortingOrder + outlineSortingOrderOffset;
                outlineMeshRenderer.sharedMaterial = pinkOutlineMaterial;
                outlineMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                outlineMeshRenderer.receiveShadows = false;
                outlineMeshRenderer.lightProbeUsage = LightProbeUsage.Off;
                outlineMeshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            outlineLayer.gameObject.SetActive(false);

            if (debugOutline)
                Debug.Log($"[EnemyHighlight] OutlineLayer 创建完成, 材质 = {pinkOutlineMaterial.name}");
        }

        // =========================================================
        // 每帧同步 mesh
        // =========================================================
        private void LateUpdate()
        {
            if (!outlineActive) return;
            if (mainMeshFilter == null || outlineMeshFilter == null) return;

            // 复制 mesh（同一个引用，Spine 每帧更新主 mesh 后，描边层自动同步）
            if (outlineMeshFilter.sharedMesh != mainMeshFilter.sharedMesh)
                outlineMeshFilter.sharedMesh = mainMeshFilter.sharedMesh;
        }

        // =========================================================
        // 描边开关
        // =========================================================
        public void SetPinkOutline(bool on)
        {
            if (outlineActive == on) return;
            outlineActive = on;

            if (outlineLayer != null)
                outlineLayer.gameObject.SetActive(on);

            if (on && outlineMeshFilter != null && mainMeshFilter != null)
                outlineMeshFilter.sharedMesh = mainMeshFilter.sharedMesh;

            if (debugOutline)
                Debug.Log($"[EnemyHighlight] SetPinkOutline({on})");
        }

        // =========================================================
        // 头顶警告光
        // =========================================================
        public void ShowWarning()
        {
            if (warningLight == null) return;
            if (warningCo != null) StopCoroutine(warningCo);
            warningCo = StartCoroutine(WarningIn());
        }

        public void HideWarning()
        {
            if (warningLight == null) return;
            if (warningCo != null) StopCoroutine(warningCo);
            warningCo = StartCoroutine(WarningOut());
        }

        private IEnumerator WarningIn()
        {
            warningLight.gameObject.SetActive(true);

            var tr = warningLight.transform;
            float t = 0f;
            float dur = Mathf.Max(0.01f, warningFadeInTime);

            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                SetWarningAlpha(eased);

                float scale;
                if (eased < 0.7f)
                {
                    float k = eased / 0.7f;
                    scale = Mathf.Lerp(warningPopStart, warningPopOvershoot, k);
                }
                else
                {
                    float k = (eased - 0.7f) / 0.3f;
                    scale = Mathf.Lerp(warningPopOvershoot, 1f, k);
                }
                tr.localScale = warningBaseScale * scale;

                yield return null;
            }

            SetWarningAlpha(1f);
            tr.localScale = warningBaseScale;

            while (true)
            {
                float s = 1f + Mathf.Sin(Time.time * warningBreathSpeed) * warningBreathAmount;
                tr.localScale = warningBaseScale * s;
                yield return null;
            }
        }

        private IEnumerator WarningOut()
        {
            var tr = warningLight.transform;
            float t = 0f;
            float dur = Mathf.Max(0.01f, warningFadeOutTime);
            float startAlpha = warningLight.color.a;
            Vector3 startScale = tr.localScale;

            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                SetWarningAlpha(Mathf.Lerp(startAlpha, 0f, t));
                tr.localScale = Vector3.Lerp(startScale, warningBaseScale * 1.3f, t);
                yield return null;
            }

            SetWarningAlpha(0f);
            tr.localScale = warningBaseScale;
            warningLight.gameObject.SetActive(false);
            warningCo = null;
        }

        private void SetWarningAlpha(float a)
        {
            if (warningLight == null) return;
            var c = warningLight.color;
            c.a = a;
            warningLight.color = c;
        }

        private void OnDisable()
        {
            if (warningLight != null)
            {
                SetWarningAlpha(0f);
                warningLight.gameObject.SetActive(false);
            }
            if (outlineLayer != null)
                outlineLayer.gameObject.SetActive(false);

            warningCo = null;
            outlineActive = false;
        }
    }
}