using UnityEngine;

namespace HundredSchools.Combat
{
    /// <summary>
    /// TrailDamage —— 挂载到御艺冲刺轨迹矩形上，处理碰撞伤害。
    ///
    /// GDD v1.9 "弹幕即思想" 副技能同步：
    ///   轨迹命中时根据玩家学派施加对应的弹幕行为：
    ///     儒家 Splash：命中后溅射周围敌人 50% 伤害，轨迹销毁
    ///     法家 Pierce：命中后不销毁，继续判定后续碰到的敌人（pierceCount 次）
    ///     道家 Return：轨迹是静态区域，按 Normal 处理
    ///     墨家/无 Normal：命中即销毁
    /// </summary>
    public class TrailDamage : MonoBehaviour
    {
        // ==================== 伤害属性 ====================

        public int damage = 15;
        public ESchool school = ESchool.Confucian;

        // ==================== GDD v1.9 弹幕行为 ====================

        public EBulletBehavior behavior = EBulletBehavior.Normal;
        public int pierceCount = 2;
        public float splashRadius = 1.0f;
        public float splashDamageMultiplier = 0.5f;

        // ==================== 运行时状态 ====================

        private int _remainingPierces;

        private void Start()
        {
            _remainingPierces = pierceCount;
        }

        // ==================== 碰撞处理 ====================

        private void OnTriggerEnter2D(Collider2D other)
        {
            Enemy.EnemyBase enemy = other.GetComponent<Enemy.EnemyBase>();
            if (enemy == null) return;

            switch (behavior)
            {
                case EBulletBehavior.Splash:
                    HandleSplash(enemy);
                    break;
                case EBulletBehavior.Pierce:
                    HandlePierce(enemy);
                    break;
                default:
                    // Return / Normal：轨迹是静态区域，统一命中销毁
                    HandleNormal(enemy);
                    break;
            }
        }

        // ==================== Splash（儒家·溅射） ====================

        private void HandleSplash(Enemy.EnemyBase primaryTarget)
        {
            primaryTarget.TakeDamage(damage);
            Debug.Log($"[TrailDamage] 溅射命中主目标 {primaryTarget.name}，伤害: {damage}");

            Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, splashRadius);
            int splashCount = 0;
            foreach (Collider2D col in nearby)
            {
                if (col.gameObject == primaryTarget.gameObject) continue;
                Enemy.EnemyBase nearbyEnemy = col.GetComponent<Enemy.EnemyBase>();
                if (nearbyEnemy != null)
                {
                    int splashDmg = Mathf.RoundToInt(damage * splashDamageMultiplier);
                    nearbyEnemy.TakeDamage(splashDmg);
                    splashCount++;
                }
            }
            Debug.Log($"[TrailDamage] 溅射 {splashCount} 个周围敌人（{splashDamageMultiplier * 100}% 伤害）");

            Destroy(gameObject);
        }

        // ==================== Pierce（法家·穿透） ====================

        private void HandlePierce(Enemy.EnemyBase enemy)
        {
            enemy.TakeDamage(damage);
            _remainingPierces--;
            Debug.Log($"[TrailDamage] 穿透命中 {enemy.name}，伤害: {damage}，剩余穿透: {_remainingPierces}");

            if (_remainingPierces <= 0)
            {
                Debug.Log("[TrailDamage] 穿透次数用尽，销毁轨迹");
                Destroy(gameObject);
            }
        }

        // ==================== Normal（墨家/无/道·普通） ====================

        private void HandleNormal(Enemy.EnemyBase enemy)
        {
            enemy.TakeDamage(damage);
            Debug.Log($"[TrailDamage] 命中 {enemy.name}，伤害: {damage}");
            Destroy(gameObject);
        }
    }
}
