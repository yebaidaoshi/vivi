using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FogManagerV2 : MonoBehaviour
{
    [Header("摄像机")]
    public Camera targetCamera;

    [Header("雾气预制体")]
    public GameObject fogPrefab;

    [Header("生成矩形范围（以摄像机为中心）")]
    public float halfWidth = 15f;   // 比摄像机视野宽
    public float halfHeight = 8f;   // 比摄像机视野高

    [Header("最大数量 & 生命周期")]
    public int maxCount = 300;      // 足够多
    public float maxLifetimeDistance = 20f; // 大于视野

    [Header("雾团运动")]
    public Vector2 horizontalSpeedRange = new Vector2(0.2f, 0.8f);
    public Vector2 verticalSpeedRange = new Vector2(-0.1f, 0.1f);

    [Header("颜色与透明度")]
    public Color fogColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [Range(0f, 1f)] public float startAlpha = 0f;
    [Range(0f, 1f)] public float maxAlpha = 0.6f;
    public float fadeInDuration = 1.2f;
    public float fadeStartDistance = 10f;
    public float fadeSpeed = 0.6f;

    [Header("渲染层级")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    [Header("调试")]
    public bool instantVisible = true;   // 初始是否立即可见

    private List<FogInstance> fogInstances = new List<FogInstance>();
    private Vector3 cameraPos;
    private float cameraHalfWidth, cameraHalfHeight;

    private class FogInstance
    {
        public GameObject gameObject;
        public SpriteRenderer renderer;
        public Vector3 velocity;
        public float targetAlpha;
        public Coroutine fadeInCoroutine;
        public bool isDestroyed = false;
    }

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (fogPrefab == null) Debug.LogError("[FogManagerV2] 未设置 fogPrefab！");
    }

    private void Start()
    {
        // 初始生成：允许在视野内，一次性生成所有雾团
        for (int i = 0; i < maxCount; i++)
        {
            SpawnFog(forceOutsideCamera: false);
        }
        Debug.Log($"[FogManagerV2] 初始生成 {fogInstances.Count} 个雾团");
    }

    private void Update()
    {
        if (targetCamera == null || fogPrefab == null) return;

        cameraPos = targetCamera.transform.position;
        float orthoSize = targetCamera.orthographicSize;
        float aspect = targetCamera.aspect;
        cameraHalfHeight = orthoSize;
        cameraHalfWidth = orthoSize * aspect;

        // 补充生成：强制在视野外生成
        while (fogInstances.Count < maxCount)
        {
            SpawnFog(forceOutsideCamera: true);
        }

        // 更新所有雾团
        for (int i = fogInstances.Count - 1; i >= 0; i--)
        {
            var inst = fogInstances[i];
            if (inst == null || inst.isDestroyed || inst.gameObject == null)
            {
                fogInstances.RemoveAt(i);
                continue;
            }

            inst.gameObject.transform.position += inst.velocity * Time.deltaTime;
            float dist = Vector3.Distance(inst.gameObject.transform.position, cameraPos);

            if (dist > maxLifetimeDistance)
            {
                DestroyFogInstance(inst);
                fogInstances.RemoveAt(i);
                continue;
            }

            if (dist > fadeStartDistance)
            {
                float t = (dist - fadeStartDistance) / (maxLifetimeDistance - fadeStartDistance);
                float target = Mathf.Lerp(inst.targetAlpha, 0f, t);
                Color c = inst.renderer.color;
                c.a = Mathf.Lerp(c.a, target, Time.deltaTime * fadeSpeed);
                inst.renderer.color = c;

                if (c.a < 0.01f)
                {
                    DestroyFogInstance(inst);
                    fogInstances.RemoveAt(i);
                }
            }
        }
    }

    private void SpawnFog(bool forceOutsideCamera)
    {
        Vector3 spawnPos;
        int attempts = 0;
        do
        {
            float x = Random.Range(-halfWidth, halfWidth);
            float y = Random.Range(-halfHeight, halfHeight);
            spawnPos = cameraPos + new Vector3(x, y, 0f);
            attempts++;
        } while (forceOutsideCamera && IsInsideCameraView(spawnPos) && attempts < 50);

        spawnPos.z = 0f;

        GameObject go = Instantiate(fogPrefab, spawnPos, Quaternion.identity);
        go.transform.rotation = Quaternion.identity;

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();

        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        float scale = Random.Range(0.8f, 1.8f);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        Color baseColor = fogColor;
        baseColor.a = 1f;
        sr.color = baseColor;

        float alphaTarget = Random.Range(maxAlpha * 0.7f, maxAlpha);

        if (instantVisible)
        {
            Color c = sr.color;
            c.a = alphaTarget;
            sr.color = c;
        }
        else
        {
            Color c = sr.color;
            c.a = startAlpha;
            sr.color = c;
        }

        float hSpeed = Random.Range(horizontalSpeedRange.x, horizontalSpeedRange.y);
        float vSpeed = Random.Range(verticalSpeedRange.x, verticalSpeedRange.y);
        if (Random.value > 0.5f) hSpeed = -hSpeed;

        var inst = new FogInstance
        {
            gameObject = go,
            renderer = sr,
            velocity = new Vector3(hSpeed, vSpeed, 0f),
            targetAlpha = alphaTarget,
            isDestroyed = false
        };

        if (!instantVisible)
        {
            inst.fadeInCoroutine = StartCoroutine(FadeIn(inst, alphaTarget));
        }

        fogInstances.Add(inst);
    }

    private bool IsInsideCameraView(Vector3 worldPos)
    {
        float dx = Mathf.Abs(worldPos.x - cameraPos.x);
        float dy = Mathf.Abs(worldPos.y - cameraPos.y);
        return dx < cameraHalfWidth && dy < cameraHalfHeight;
    }

    private IEnumerator FadeIn(FogInstance inst, float targetAlpha)
    {
        float elapsed = 0f;
        Color c = inst.renderer.color;
        float start = c.a;

        while (elapsed < fadeInDuration)
        {
            if (inst == null || inst.isDestroyed || inst.gameObject == null || inst.renderer == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;
            c.a = Mathf.Lerp(start, targetAlpha, t);
            inst.renderer.color = c;
            yield return null;
        }

        if (inst != null && !inst.isDestroyed && inst.gameObject != null && inst.renderer != null)
        {
            c.a = targetAlpha;
            inst.renderer.color = c;
        }
    }

    private void DestroyFogInstance(FogInstance inst)
    {
        if (inst == null || inst.isDestroyed) return;
        inst.isDestroyed = true;
        if (inst.fadeInCoroutine != null) StopCoroutine(inst.fadeInCoroutine);
        if (inst.gameObject != null) Destroy(inst.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (targetCamera == null) return;
        Vector3 center = targetCamera.transform.position;
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireCube(center, new Vector3(halfWidth * 2, halfHeight * 2, 0.1f));
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(center, fadeStartDistance);
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(center, maxLifetimeDistance);
    }
}