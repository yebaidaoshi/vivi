using UnityEngine;

namespace Player
{
    
    public class DamageTarget : MonoBehaviour
    {
        private IDamageable _cachedDamageable;

        public IDamageable Damageable
        {
            get
            {
                if (_cachedDamageable == null)
                {
                    // 运行时自动查找 PlayerHealth（它由 PlayerController 动态添加）
                    _cachedDamageable = FindObjectOfType<PlayerHealth>();
                    if (_cachedDamageable == null)
                    {
                        Debug.LogError($"[DamageTarget] 找不到 PlayerHealth！请确保 PlayerController 已正确初始化。");
                    }
                }
                return _cachedDamageable;
            }
        }
    }
}