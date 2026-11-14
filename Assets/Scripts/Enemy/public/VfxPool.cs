using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// 打击特效对象池：避免每次命中都 Instantiate/Destroy 造成卡顿与 GC。
    /// 第一次使用时会创建实例（可在启动时用 Warmup 预热，避开第一次命中的卡顿）。
    /// </summary>
    public static class VfxPool
    {
        private class Pooled : MonoBehaviour
        {
            public GameObject Prefab;
            public float Lifetime;
            private Coroutine _routine;

            public void Play(float lifetime)
            {
                Lifetime = lifetime;
                gameObject.SetActive(true);
                if (_routine != null) StopCoroutine(_routine);
                _routine = StartCoroutine(Co());
            }

            private IEnumerator Co()
            {
                yield return new WaitForSeconds(Lifetime);
                gameObject.SetActive(false);
                _routine = null;
            }
        }

        private static readonly Dictionary<GameObject, Queue<GameObject>> _pools =
            new Dictionary<GameObject, Queue<GameObject>>();

        /// <summary>启动时调用：预先创建实例，避免第一次命中触发实例化/着色器编译卡顿。</summary>
        public static void Warmup(GameObject prefab, int count = 1)
        {
            if (prefab == null) return;

            var queue = GetQueue(prefab);
            for (int i = 0; i < count; i++)
            {
                var go = Object.Instantiate(prefab);
                var pooled = go.AddComponent<Pooled>();
                pooled.Prefab = prefab;
                go.SetActive(false);
                queue.Enqueue(go);
            }
        }

        public static void Spawn(GameObject prefab, Vector3 position, float lifetime)
        {
            if (prefab == null) return;

            var queue = GetQueue(prefab);

            GameObject instance = null;
            while (queue.Count > 0)
            {
                var candidate = queue.Dequeue();
                if (candidate != null) { instance = candidate; break; }
            }

            if (instance == null)
            {
                instance = Object.Instantiate(prefab);
                var pooled = instance.AddComponent<Pooled>();
                pooled.Prefab = prefab;
            }

            instance.transform.position = position;
            instance.SetActive(true);

            var item = instance.GetComponent<Pooled>();
            if (item != null) item.Play(lifetime);
        }

        private static Queue<GameObject> GetQueue(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
            }
            return queue;
        }
    }
}
