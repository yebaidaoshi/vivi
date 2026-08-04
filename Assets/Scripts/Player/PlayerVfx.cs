using UnityEngine;

namespace Player
{
    /// <summary>
    /// 共享的一次性 VFX 生成器。镜像 floor.unity 的 <c>CreateObject</c>：在生成根节点
    /// 实例化预制体（不挂父），保留预制体自身的旋转/缩放，可选按朝向翻转 X
    ///（GetScale → SetScale）。循环拖尾传入 <c>lifetime &lt;= 0</c>，
    /// 由调用方用 <see cref="StopAndDestroy"/> 停止。
    /// </summary>
    public static class PlayerVfx
    {
        public static GameObject SpawnOneShot(GameObject prefab, Transform root, Vector2 offset,
            int facing, bool mirrorByFacing, float lifetime)
        {
            if (prefab == null || root == null)
            {
                return null;
            }

            int sign = facing >= 0 ? 1 : -1;
            var fx = Object.Instantiate(prefab);
            fx.transform.position = root.position + new Vector3(offset.x * sign, offset.y, 0f);
            fx.transform.rotation = root.rotation;

            if (mirrorByFacing)
            {
                Vector3 s = fx.transform.localScale;
                s.x = Mathf.Abs(s.x) * sign;
                fx.transform.localScale = s;
            }

            if (lifetime > 0f)
            {
                Object.Destroy(fx, lifetime);
            }

            return fx;
        }

        /// <summary>
        /// 将 FX 挂到 <paramref name="root"/> 下，使循环发射器（SlideEffect）沿路径跟随。
        /// 保留预制体自带的本地旋转（粒子常为 −90°）。
        /// </summary>
        public static GameObject SpawnAttached(GameObject prefab, Transform root, Vector2 offset,
            int facing, bool mirrorByFacing)
        {
            if (prefab == null || root == null)
            {
                return null;
            }

            int sign = facing >= 0 ? 1 : -1;
            var fx = Object.Instantiate(prefab, root);
            fx.transform.localPosition = new Vector3(offset.x * sign, offset.y, 0f);

            if (mirrorByFacing)
            {
                Vector3 s = fx.transform.localScale;
                s.x = Mathf.Abs(s.x) * sign;
                fx.transform.localScale = s;
            }

            return fx;
        }

        /// <summary>
        /// 停止循环特效的发射，让已有粒子淡出后再销毁，
        /// 避免滑铲结束瞬间拖尾突然消失。
        /// </summary>
        public static void StopAndDestroy(GameObject fx, float tail = 0.6f)
        {
            if (fx == null)
            {
                return;
            }

            var systems = fx.GetComponentsInChildren<ParticleSystem>();
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Object.Destroy(fx, tail);
        }
    }
}
