using UnityEngine;

namespace HundredSchools.Combat
{
    /// <summary>
    /// 弹幕行为枚举 —— GDD v1.9 "弹幕即思想" 核心机制。
    ///
    /// 儒家 → Splash（溅射）：命中后爆炸，对周围敌人造成 50% 溅射伤害
    /// 法家 → Pierce（穿透）：命中后继续飞行，可穿透多个敌人
    /// 道家 → Return（回转）：命中后折返飞向玩家，二次命中后销毁
    /// 墨家 / 无学派 → Normal：命中即销毁
    /// </summary>
    public enum EBulletBehavior
    {
        Normal,
        Splash,
        Pierce,
        Return
    }

    /// <summary>
    /// ProjectileBase —— 所有子弹 / 弹幕的基类。
    ///
    /// GDD v1.9 "弹幕即思想"：
    ///   子弹不仅是伤害载体，更是学派思想的具象化。
    ///   不同学派发射的子弹具有不同的碰撞行为（溅射/穿透/回转），
    ///   由 EBulletBehavior 枚举驱动。
    ///
    /// 挂载到：任意子弹 GameObject（须有 SpriteRenderer + Collider2D + Rigidbody2D）
    /// </summary>
    public class ProjectileBase : MonoBehaviour
    {
        // ==================== 基础属性 ====================

        [Header("基础属性")]
        [SerializeField, Range(1f, 30f)]
        protected float moveSpeed = 10f;

        [SerializeField, Range(1, 100)]
        protected int damage = 10;

        public ESchool school = ESchool.Confucian;

        [Header("生命周期")]
        [SerializeField, Range(1f, 30f)]
        protected float maxLifetime = 5f;

        // ==================== GDD v1.9 弹幕行为 ====================

        /// <summary>弹幕行为类型（由武器脚本根据玩家学派设置）</summary>
        public EBulletBehavior behavior = EBulletBehavior.Normal;

        /// <summary>穿透剩余次数（仅 Pierce 行为有效）</summary>
        public int pierceCount = 2;

        /// <summary>溅射半径（仅 Splash 行为有效，GDD 规格: 1.0）</summary>
        public float splashRadius = 1.0f;

        /// <summary>溅射伤害倍率（仅 Splash 行为有效，默认 50%）</summary>
        public float splashDamageMultiplier = 0.5f;

        /// <summary>伤害倍率（GDD：法家 1.2x / 儒家 0.8x / 道家 0.6x）</summary>
        public float damageMultiplier = 1.0f;

        /// <summary>最终伤害 = 基础伤害 × damageMultiplier</summary>
        public int FinalDamage => Mathf.RoundToInt(damage * damageMultiplier);

        /// <summary>标记为敌方弹幕，Boss 阶段切换时用于清理</summary>
        public bool IsEnemyProjectile;

        /// <summary>全局冻结：true 时所有弹幕停止移动</summary>
        public static bool GlobalFreeze;

        /// <summary>精英儒系溅射：命中后对周围 2m 造成 50% 伤害</summary>
        public bool IsSplash;
        /// <summary>精英法系追踪：弹幕缓慢转向玩家（90°/s）</summary>
        public bool IsTracking;
        /// <summary>追踪转向速率（度/秒）</summary>
        public float trackingTurnRate = 90f;

        // ==================== 运行时状态 ====================

        protected Vector3 flightDirection = Vector3.right;
        protected float elapsedTime;
        public bool isPiercing;
        protected GameObject _owner;

        /// <summary>Return 行为专用：是否已经命中过一次（命中后折返）</summary>
        private bool _hasHitOnce;

        /// <summary>Return 行为专用：折返目标（敌人子弹应设为玩家 Transform，null 则回退到 FindPlayer）</summary>
        public Transform returnTarget;

        /// <summary>当前帧是否已命中目标。防止同一帧对多个重叠敌人造成伤害。</summary>
        private int _hitFrame = -1;

        // ==================== 组件引用 ====================

        protected SpriteRenderer spriteRenderer;

        // ==================== Unity 生命周期 ====================

        protected virtual void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        protected virtual void Update()
        {
            // 精英法系追踪：弹幕缓慢转向玩家
            if (IsTracking)
            {
                var player = FindPlayer();
                if (player != null)
                {
                    Vector3 toPlayer = (player.position - transform.position).normalized;
                    float maxAngle = trackingTurnRate * Mathf.Deg2Rad * Time.deltaTime;
                    flightDirection = Vector3.RotateTowards(flightDirection, toPlayer, maxAngle, 0f);
                    float angle = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }

            if (!GlobalFreeze)
                transform.position += flightDirection * moveSpeed * Time.deltaTime;

            elapsedTime += Time.deltaTime;
            if (elapsedTime >= maxLifetime)
                Destroy(gameObject);
        }

        // ==================== 公开接口 ====================

        /// <summary>
        /// 初始化子弹数据。Instantiate 之后立即调用。
        /// </summary>
        public virtual void Init(Vector3 direction, float speed, int dmg, ESchool type)
        {
            flightDirection = direction.normalized;
            moveSpeed = speed;
            damage = dmg;
            school = type;
            elapsedTime = 0f;
            _hasHitOnce = false;
            _hitFrame = -1;

            if (flightDirection.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            ApplySchoolColor();
        }

        public void SetOwner(GameObject owner)
        {
            _owner = owner;
        }

        // ==================== 碰撞处理（GDD v1.9 核心） ====================

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            // ★ 防止同一帧命中多个重叠敌人（"一箭一敌"）
            if (_hitFrame == Time.frameCount) return;
            _hitFrame = Time.frameCount;

            if (_owner != null && other.gameObject == _owner)
                return;

            // 敌人子弹命中玩家（由 ProjectileBase 统一处理行为分流）
            if (other.CompareTag("Player"))
            {
                HandlePlayerHit(other);
                return;
            }

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
                case EBulletBehavior.Return:
                    HandleReturn(enemy);
                    break;
                default:
                    HandleNormal(enemy);
                    break;
            }
        }

        // ==================== Splash（儒家·溅射） ====================

        /// <summary>
        /// 溅射行为：命中敌人 → 造成伤害 → OverlapCircleAll 检测周围敌人
        /// → 造成 50% 溅射伤害 → 播放爆炸特效 → 销毁子弹。
        /// </summary>
        private void HandleSplash(Enemy.EnemyBase primaryTarget)
        {
            primaryTarget.TakeDamage(FinalDamage);
            Debug.Log($"[ProjectileBase] 溅射命中主目标 {primaryTarget.name}，伤害: {FinalDamage}");

            // 溅射范围检测
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
            Debug.Log($"[ProjectileBase] 溅射伤害 {splashCount} 个周围敌人（{splashDamageMultiplier * 100}% 伤害）");

            PlayExplosionEffect();
            Destroy(gameObject);
        }

        /// <summary>
        /// 溅射爆炸视觉：生成一个短暂的半透明圆环。
        /// </summary>
        private void PlayExplosionEffect()
        {
            GameObject fx = new GameObject("VFX_Splash");
            fx.transform.position = transform.position;

            SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
            sr.sprite = WeaponUtils.GetOrCreateRingSprite();
            sr.sortingOrder = 5;

            Color baseColor = WeaponUtils.GetSchoolColor(school);
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.6f);

            fx.transform.localScale = Vector3.one * splashRadius * 2f;

            Destroy(fx, 0.3f);
        }

        // ==================== Pierce（法家·穿透） ====================

        /// <summary>
        /// 穿透行为：命中敌人 → 造成伤害 → pierceCount--。
        /// 如果 pierceCount > 0：子弹不销毁，继续飞行。
        /// 如果 pierceCount ≤ 0：销毁子弹。
        /// </summary>
        private void HandlePierce(Enemy.EnemyBase enemy)
        {
            enemy.TakeDamage(FinalDamage);
            pierceCount--;
            Debug.Log($"[ProjectileBase] 穿透命中 {enemy.name}，伤害: {FinalDamage}，剩余穿透: {pierceCount}");

            if (pierceCount <= 0)
            {
                Debug.Log("[ProjectileBase] 穿透次数用尽，销毁子弹");
                Destroy(gameObject);
            }
        }

        // ==================== Return（道家·回转） ====================

        /// <summary>
        /// 回转行为：
        ///   第一次命中 → 造成伤害 → 子弹不销毁 → 改变方向飞向玩家位置（回转）。
        ///   回转途中再次命中 → 造成伤害 → 销毁子弹。
        /// </summary>
        private void HandleReturn(Enemy.EnemyBase enemy)
        {
            enemy.TakeDamage(FinalDamage);

            if (!_hasHitOnce)
            {
                // 第一次命中：折返，飞向 returnTarget（敌人子弹）或 FindPlayer（玩家子弹）
                _hasHitOnce = true;
                Transform target = returnTarget != null ? returnTarget : FindPlayer();
                if (target != null)
                {
                    flightDirection = (target.position - transform.position).normalized;
                    float angle = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    Debug.Log($"[ProjectileBase] 回转！子弹折返飞向目标位置 ({target.position.x:F1}, {target.position.y:F1})");
                }
                else
                {
                    Debug.LogWarning("[ProjectileBase] 回转失败：找不到目标，子弹销毁");
                    Destroy(gameObject);
                }
            }
            else
            {
                // 回转途中第二次命中 → 销毁
                Debug.Log($"[ProjectileBase] 回转途中命中 {enemy.name}，伤害: {FinalDamage}，子弹销毁");
                Destroy(gameObject);
            }
        }

                // ==================== 命中玩家 ====================

        private void HandlePlayerHit(Collider2D playerCol)
        {
            Player.PlayerMovement player = playerCol.GetComponent<Player.PlayerMovement>();
            if (player == null) return;

            // 精英儒系溅射：对玩家周围 2m 也造成 50% 伤害
            if (IsSplash)
            {
                player.TakeDamage(FinalDamage);
            }

            switch (behavior)
            {
                case EBulletBehavior.Splash:
                    player.TakeDamage(FinalDamage);
                    Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, splashRadius);
                    foreach (Collider2D col in nearby) { Enemy.EnemyBase e = col.GetComponent<Enemy.EnemyBase>(); if (e != null) e.TakeDamage(Mathf.RoundToInt(damage * splashDamageMultiplier)); }
                    PlayExplosionEffect(); Destroy(gameObject); break;
                case EBulletBehavior.Pierce: player.TakeDamage(FinalDamage); pierceCount--; if (pierceCount <= 0) Destroy(gameObject); break;
                case EBulletBehavior.Return: player.TakeDamage(FinalDamage); if (!_hasHitOnce) { _hasHitOnce = true; Transform t = returnTarget != null ? returnTarget : FindPlayer(); if (t != null) { flightDirection = (t.position - transform.position).normalized; float a = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg; transform.rotation = Quaternion.Euler(0f, 0f, a); } else Destroy(gameObject); } else Destroy(gameObject); break;
                default: player.TakeDamage(FinalDamage); Destroy(gameObject); break;
            }
        }

// ==================== Normal（墨家/无学派·普通） ====================

        private void HandleNormal(Enemy.EnemyBase enemy)
        {
            enemy.TakeDamage(FinalDamage);
            Debug.Log($"[ProjectileBase] 命中敌人 {enemy.name}，伤害: {FinalDamage}");
            Destroy(gameObject);
        }

        // ==================== 内部辅助方法 ====================

        /// <summary>
        /// 根据学派枚举值设置 SpriteRenderer 颜色。
        /// GDD v1.9 第3节颜色映射：
        ///   儒家 → 金色 / 法家 → 黑色 / 道家 → 青色 / 墨家 → 灰色 / 无学派 → 白色
        /// </summary>
        protected void ApplySchoolColor(float darkenMultiplier = 1f)
        {
            if (spriteRenderer == null) return;

            Color baseColor = WeaponUtils.GetSchoolColor(school);

            spriteRenderer.color = new Color(
                baseColor.r * darkenMultiplier,
                baseColor.g * darkenMultiplier,
                baseColor.b * darkenMultiplier,
                1f
            );
        }

        /// <summary>将此子弹标记为蓄力弹：开启穿透 + 颜色变深。</summary>
        public void MarkAsCharged()
        {
            isPiercing = true;
            ApplySchoolColor(0.6f);
        }

        private Transform FindPlayer()
        {
            if (_owner != null && _owner.CompareTag("Player"))
                return _owner.transform;
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            return playerObj != null ? playerObj.transform : null;
        }

        // ==================== 只读属性 ====================

        public int Damage => damage;
        public ESchool School => school;
    }
}
