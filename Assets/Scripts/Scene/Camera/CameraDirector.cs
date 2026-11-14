using System.Collections.Generic;
using UnityEngine;

namespace SceneCamera
{
    /// <summary>
    /// 主相机主控（接管 ProCamera2D / ScoutCameraFinal 的跟随职责）。
    ///
    /// 设计目标：把原版「Cinemachine vcam + TargetGroup + Confiner」的行为
    /// 用纯 C# 复刻到本工程，不引入 Cinemachine 依赖：
    ///   - 跟随 + 死区 + 阻尼        ← CinemachineFramingTransposer
    ///   - 战斗多目标构图            ← CinemachineTargetGroup + PC2DAddCameraTarget
    ///   - 相机边界                  ← CinemachineConfiner
    ///   - 震屏                      ← PC2DShakeWithValues
    ///   - 缩放                      ← ZoomKey / GetCameraOrthoSize
    ///
    /// 使用：挂在 Main Camera 上，Target 留空时会自动找 tag = PlayerPresence / Player。
    /// 注意：同一个相机上不要同时启用 ProCamera2D，否则两边都会写 transform。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class CameraDirector : MonoBehaviour
    {
        [Header("跟随目标")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 targetOffset = new Vector2(0f, 7f);
        [SerializeField] private string[] autoFindTags = { "PlayerPresence", "Player" };

        [Header("跟随（死区 / 阻尼）")]
        [Tooltip("死区半径（世界单位）。目标在死区内相机不动；0 = 完全跟随。")]
        [SerializeField] private Vector2 deadZone = new Vector2(0.5f, 0.5f);
        [Tooltip("跟随阻尼（秒）。原版 Cinemachine FramingTransposer 为 0.5。")]
        [SerializeField] private float followDamping = 0.5f;
        [SerializeField] private float maxFollowSpeed = 60f;
        [SerializeField] private bool followX = true;
        [SerializeField] private bool followY = true;

        [Header("跳跃上抬")]
        [SerializeField] private float jumpOffset = 1.5f;
        [SerializeField] private float jumpSmooth = 0.15f;
        [SerializeField] private float jumpVelocityThreshold = 1.5f;

        [Header("战斗构图（多目标）")]
        [Tooltip("交战目标总权重。原版 PC2DAddCameraTarget 的 Weight = 0.5。")]
        [SerializeField, Range(0f, 1f)] private float combatInfluence = 0.5f;
        [SerializeField] private float combatBlendSpeed = 2f;
        [SerializeField] private int maxCombatTargets = 3;
        [SerializeField] private float combatTargetMaxDistance = 18f;

        [Header("缩放")]
        [SerializeField] private float defaultOrthoSize = 10f;
        [SerializeField] private float zoomDamping = 0.35f;

        [Header("边界")]
        [SerializeField] private CameraBounds2D bounds;

        [Header("震屏")]
        [SerializeField] private float shakeFrequency = 26f;
        [SerializeField] private float shakeRecovery = 6f;

        [Header("兼容性")]
        [Tooltip("Awake 时自动禁用同物体上的 ProCamera2D / ProCamera2DShake。")]
        [SerializeField] private bool disableProCamera2DOnAwake = true;
        [Tooltip("Awake 时自动禁用同物体上的其它相机跟随脚本（ScoutCameraFinal 等），避免两个脚本抢 transform 导致画面抖动。")]
        [SerializeField] private bool disableOtherCameraFollowersOnAwake = true;
        [SerializeField] private string[] conflictingTypeNames = { "ScoutCameraFinal", "ProCamera2D", "ProCamera2DShake" };
        [Tooltip("启动时采用相机当前的正交尺寸作为基准，避免一进场景被强制缩放到默认值。")]
        [SerializeField] private bool adoptCameraSizeOnAwake = true;

        // ---- 运行时 ----
        private Camera cam;
        private float fixedZ;
        private Vector3 basePosition;
        private bool baseInitialized;
        private Vector3 followVelocity;
        private float currentJumpOffset;
        private float jumpVelocity;
        private float combatBlend;          // 0..1
        private float zoomVelocity;
        private float shakeTimeLeft;
        private float shakeDuration;
        private float shakeStrength;
        private float shakeSeed;

        private readonly List<CombatTarget> combatTargets = new List<CombatTarget>(4);

        private class CombatTarget
        {
            public Transform Transform;
            public float Weight;
        }

        public Transform Target => target;
        public Camera Camera => cam;
        public bool IsShaking => shakeTimeLeft > 0f;

        /// <summary>当前参与构图的敌人数量。</summary>
        public int CombatTargetCount => combatTargets.Count;

        /// <summary>场景内实例，方便敌人 / 玩家系统直接调用。</summary>
        public static CameraDirector Instance { get; private set; }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            fixedZ = transform.position.z;

            if (cam != null && adoptCameraSizeOnAwake && cam.orthographicSize > 0.01f)
                defaultOrthoSize = cam.orthographicSize;
            else if (defaultOrthoSize <= 0f && cam != null)
                defaultOrthoSize = cam.orthographicSize;

            basePosition = transform.position;
            baseInitialized = true;

            if (disableProCamera2DOnAwake || disableOtherCameraFollowersOnAwake)
                DisableConflictingComponents();
        }

        private void Start()
        {
            AutoFindTarget();
            if (target != null) SnapToTarget();
        }

        private static readonly string[] DefaultConflicts = { "ScoutCameraFinal", "ProCamera2D", "ProCamera2DShake" };

        private void DisableConflictingComponents()
        {
            // 场景里已存在的组件可能是旧版本序列化的，数组字段可能为 null —— 这里兜底。
            string[] blocked = conflictingTypeNames;
            if (blocked == null || blocked.Length == 0) blocked = DefaultConflicts;

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour mb = behaviours[i];
                if (mb == null || mb == this || !mb.enabled) continue;

                string typeName = mb.GetType().Name;
                for (int j = 0; j < blocked.Length; j++)
                {
                    string name = blocked[j];
                    if (string.IsNullOrEmpty(name)) continue;
                    if (typeName != name) continue;

                    bool isProCamera = name.StartsWith("ProCamera2D");
                    if (isProCamera && !disableProCamera2DOnAwake) continue;
                    if (!isProCamera && !disableOtherCameraFollowersOnAwake) continue;

                    mb.enabled = false;
                    Debug.LogWarning(
                        "[CameraDirector] 检测到冲突的相机脚本 " + typeName + "，已自动禁用，避免画面抖动。" +
                        "（如确实需要它，请在 Inspector 里关掉对应的“自动禁用”开关）", this);
                    break;
                }
            }
        }

        private void AutoFindTarget()
        {
            if (target != null) return;
            if (autoFindTags == null) return;

            for (int i = 0; i < autoFindTags.Length; i++)
            {
                if (string.IsNullOrEmpty(autoFindTags[i])) continue;
                GameObject go = GameObject.FindGameObjectWithTag(autoFindTags[i]);
                if (go != null)
                {
                    target = go.transform;
                    return;
                }
            }
        }

        // =====================================================================
        //  公开 API
        // =====================================================================

        public void SetTarget(Transform newTarget, bool snap = false)
        {
            target = newTarget;
            if (snap) SnapToTarget();
        }

        /// <summary>瞬间对齐到目标（换场景 / 过场结束后用）。</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            Vector3 pos = ComputeFocusPoint();
            pos.z = fixedZ;
            if (bounds != null && cam != null) pos = bounds.Clamp(cam, pos);
            basePosition = new Vector3(pos.x, pos.y, fixedZ);
            baseInitialized = true;
            transform.position = basePosition;
            followVelocity = Vector3.zero;
            currentJumpOffset = 0f;
            jumpVelocity = 0f;
        }

        /// <summary>加入战斗构图目标（敌人进入交战状态时调用）。</summary>
        public void AddCombatTarget(Transform t, float weight = -1f)
        {
            if (t == null) return;
            if (weight < 0f) weight = 1f;

            for (int i = 0; i < combatTargets.Count; i++)
            {
                if (combatTargets[i].Transform == t)
                {
                    combatTargets[i].Weight = weight;
                    return;
                }
            }

            if (combatTargets.Count >= maxCombatTargets) return;
            combatTargets.Add(new CombatTarget { Transform = t, Weight = weight });
        }

        /// <summary>移除战斗构图目标（敌人脱战 / 死亡时调用）。</summary>
        public void RemoveCombatTarget(Transform t)
        {
            if (t == null) return;
            for (int i = combatTargets.Count - 1; i >= 0; i--)
            {
                if (combatTargets[i].Transform == t) combatTargets.RemoveAt(i);
            }
        }

        public void ClearCombatTargets()
        {
            combatTargets.Clear();
        }

        /// <summary>请求缩放（正交尺寸）。</summary>
        public void SetZoom(float orthoSize, float damping = -1f)
        {
            defaultOrthoSize = orthoSize;
            if (damping >= 0f) zoomDamping = damping;
        }

        /// <summary>相对当前缩放。</summary>
        public void SetZoomDelta(float delta)
        {
            defaultOrthoSize = Mathf.Max(0.5f, defaultOrthoSize + delta);
        }

        /// <summary>震屏。duration 秒，strength 为世界单位偏移峰值（0.2~0.6 比较合适）。</summary>
        public void Shake(float duration = 0.2f, float strength = 0.35f)
        {
            if (duration <= 0f || strength <= 0f) return;
            if (duration > shakeTimeLeft) shakeDuration = duration;
            shakeTimeLeft = Mathf.Max(shakeTimeLeft, duration);
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeSeed = Random.value * 100f;
        }

        public void StopShake()
        {
            shakeTimeLeft = 0f;
            shakeStrength = 0f;
        }

        public void SetBounds(CameraBounds2D newBounds)
        {
            bounds = newBounds;
        }

        // =====================================================================
        //  每帧
        // =====================================================================

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) dt = Time.unscaledDeltaTime;

            if (target == null) AutoFindTarget();

            // ★ 用自己上一帧的基准位置做积分，不用 transform.position。
            //   否则场景里若还有别的脚本写 transform，会把它的位移当成“相机速度”，产生持续抖动。
            Vector3 pos = baseInitialized ? basePosition : transform.position;

            if (target != null)
            {
                pos = FollowTarget(pos, dt);
            }

            if (bounds != null && cam != null) pos = bounds.Clamp(cam, pos);

            basePosition = new Vector3(pos.x, pos.y, fixedZ);
            baseInitialized = true;

            Vector3 visual = ApplyShake(basePosition, dt);
            transform.position = new Vector3(visual.x, visual.y, fixedZ);

            ApplyZoom(dt);
        }

        private Vector3 ComputeFocusPoint()
        {
            if (target == null) return transform.position;
            return target.position + (Vector3)targetOffset + Vector3.up * currentJumpOffset;
        }

        private Vector3 FollowTarget(Vector3 camPos, float dt)
        {
            // 1) 跳跃上抬（从 ScoutCameraFinal 迁过来）
            bool jumping = false;
            Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
            if (rb != null && rb.velocity.y > jumpVelocityThreshold) jumping = true;

            var jumpModule = target.GetComponent<IPlayerAirState>();
            if (jumpModule != null && jumpModule.IsInAir) jumping = true;

            float jumpTarget = jumping ? jumpOffset : 0f;
            currentJumpOffset = Mathf.SmoothDamp(currentJumpOffset, jumpTarget, ref jumpVelocity, jumpSmooth);

            // 2) 目标点
            Vector3 focus = ComputeFocusPoint();

            // 3) 战斗构图
            Vector3 combatCenter = Vector3.zero;
            float combatWeightSum = 0f;
            if (combatTargets.Count > 0)
            {
                int valid = 0;
                Vector3 sum = Vector3.zero;
                for (int i = combatTargets.Count - 1; i >= 0; i--)
                {
                    var ct = combatTargets[i];
                    if (ct.Transform == null)
                    {
                        combatTargets.RemoveAt(i);
                        continue;
                    }

                    float dist = Vector2.Distance(ct.Transform.position, target.position);
                    if (dist > combatTargetMaxDistance)
                    {
                        combatTargets.RemoveAt(i);
                        continue;
                    }

                    sum += ct.Transform.position * ct.Weight;
                    combatWeightSum += ct.Weight;
                    valid++;
                }

                if (valid > 0 && combatWeightSum > 0.0001f)
                {
                    combatCenter = sum / combatWeightSum;

                    // 玩家权重固定 1，敌人权重组按 combatInfluence 折进去
                    float playerWeight = 1f;
                    Vector3 merged = (focus * playerWeight + combatCenter * combatInfluence)
                                     / (playerWeight + combatInfluence);
                    focus = Vector3.Lerp(focus, merged, combatBlend);
                }
            }

            float blendTarget = combatTargets.Count > 0 ? 1f : 0f;
            combatBlend = Mathf.MoveTowards(combatBlend, blendTarget, combatBlendSpeed * dt);

            // 4) 死区
            Vector3 delta = focus - camPos;
            delta.z = 0f;

            Vector3 desired = camPos;
            if (followX)
            {
                float dx = delta.x;
                if (Mathf.Abs(dx) > deadZone.x)
                    desired.x = camPos.x + dx - Mathf.Sign(dx) * deadZone.x;
            }
            else
            {
                desired.x = camPos.x;
            }

            if (followY)
            {
                float dy = delta.y;
                if (Mathf.Abs(dy) > deadZone.y)
                    desired.y = camPos.y + dy - Mathf.Sign(dy) * deadZone.y;
            }
            else
            {
                desired.y = camPos.y;
            }

            // 5) 阻尼
            Vector3 smoothed = Vector3.SmoothDamp(camPos, desired, ref followVelocity, followDamping, maxFollowSpeed, dt);
            if (!followX) smoothed.x = camPos.x;
            if (!followY) smoothed.y = camPos.y;
            smoothed.z = camPos.z;
            return smoothed;
        }

        private Vector3 ApplyShake(Vector3 pos, float dt)
        {
            if (shakeTimeLeft <= 0f) return pos;

            shakeTimeLeft -= dt;

            float t = Mathf.Clamp01(shakeTimeLeft / Mathf.Max(0.0001f, shakeDuration));
            float falloff = t * t;
            float amp = shakeStrength * falloff;

            float time = Time.time * shakeFrequency;
            float ox = (Mathf.PerlinNoise(shakeSeed, time) * 2f - 1f) * amp;
            float oy = (Mathf.PerlinNoise(shakeSeed + 13.37f, time) * 2f - 1f) * amp;

            if (shakeTimeLeft <= 0f)
            {
                shakeTimeLeft = 0f;
                shakeStrength = 0f;
            }
            else
            {
                shakeStrength = Mathf.Lerp(shakeStrength, 0f, shakeRecovery * dt * 0.1f);
            }

            return pos + new Vector3(ox, oy, 0f);
        }

        private void ApplyZoom(float dt)
        {
            if (cam == null || !cam.orthographic) return;
            if (Mathf.Approximately(cam.orthographicSize, defaultOrthoSize)) return;

            cam.orthographicSize = Mathf.SmoothDamp(
                cam.orthographicSize, defaultOrthoSize, ref zoomVelocity, zoomDamping, Mathf.Infinity, dt);
        }
    }

    /// <summary>
    /// 让 CameraDirector 不用直接引用 Player 程序集：玩家侧实现这个接口即可获得「跳跃上抬」。
    /// </summary>
    public interface IPlayerAirState
    {
        bool IsInAir { get; }
    }
}
