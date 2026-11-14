using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 伤害数字配置：场景里随便挂一个物体上就能在 Inspector 里实时调。
    /// 没挂就用代码默认值。
    /// </summary>
    public class DamageNumberConfig : MonoBehaviour
    {
        [Header("字体")]
        [Tooltip("TextMesh 的 fontSize（渲染精度），一般 48~96")]
        public int fontSize = 64;
        [Tooltip("世界空间字符大小，越大字越大")]
        public float characterSize = 0.08f;

        [Header("大小")]
        public float enemyScale = 1f;
        public float playerScale = 1.1f;

        [Header("动画")]
        public float lifetime = 0.8f;
        public float riseSpeed = 2.2f;
        [Tooltip("相对命中点的高度偏移")]
        public float heightOffset = 0.4f;

        [Header("颜色")]
        public Color enemyColor = Color.white;
        public Color playerColor = new Color(1f, 0.25f, 0.25f, 1f);

        private void Awake()
        {
            DamageNumber.ApplyConfig(this);
        }
    }
}
