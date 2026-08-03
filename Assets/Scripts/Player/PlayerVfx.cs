using UnityEngine;

namespace Player
{
    /// <summary>
    /// Shared one-shot VFX spawner. Mirrors floor.unity <c>CreateObject</c>: instantiate a
    /// prefab (unparented) at a spawn root, keeping the prefab's own rotation/scale, optionally
    /// flipping X by facing (GetScale → SetScale). Looping trails pass <c>lifetime &lt;= 0</c> and
    /// are stopped by the caller with <see cref="StopAndDestroy"/>.
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
        /// Stop emission on a looping effect and let live particles fade before destroying it,
        /// so a slide trail does not pop out the instant the slide ends.
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
