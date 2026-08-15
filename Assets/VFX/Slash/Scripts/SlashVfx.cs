using UnityEngine;

namespace Vivi.Slash
{
    /// <summary>
    /// 刀光预制体生成入口。拖到角色上，或从代码调用 <see cref="Play"/>。
    /// 与 <c>PlayerVfx.SpawnOneShot</c> 一致：不挂父、按朝向镜像 X。
    /// </summary>
    public static class SlashVfx
    {
        public static GameObject Play(GameObject prefab, Vector3 worldPos, int facing = 1,
            Quaternion? worldRot = null)
        {
            if (prefab == null)
            {
                return null;
            }

            var fx = Object.Instantiate(prefab);
            fx.transform.position = worldPos;
            fx.transform.rotation = worldRot ?? Quaternion.identity;
            Vector3 s = fx.transform.localScale;
            s.x = Mathf.Abs(s.x) * (facing >= 0 ? 1f : -1f);
            fx.transform.localScale = s;
            return fx;
        }
    }
}
