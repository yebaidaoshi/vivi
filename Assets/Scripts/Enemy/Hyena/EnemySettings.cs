using UnityEngine;

namespace Enemy
{
    [CreateAssetMenu(fileName = "EnemySettings", menuName = "Enemy/Settings")]
    public class EnemySettings : ScriptableObject
    {
        public int maxHealth = 100;
        public float moveSpeed = 3f;
        public float chaseSpeed = 5f;
        public float sightRange = 10f;
        public float attackRange = 2.5f;
        public float attackCooldown = 1.5f;
        public int damage = 20;
        public float hitStun = 0.3f;
    }
}