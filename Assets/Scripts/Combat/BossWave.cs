using UnityEngine;

namespace HundredSchools.Combat
{
    /// <summary>
    /// BossWave —— Boss 移动时产生的波纹攻击。
    ///
    /// 行为：
    ///   1. 每帧从半径 0.1 扩大到 maxRadius（默认 5），持续 duration 秒
    ///   2. 视觉：淡灰色圆环，随着扩大逐渐变透明
    ///   3. 碰撞：CircleCollider2D 随视觉同步扩大，碰到玩家造成伤害
    ///   4. 持续时间结束后自动销毁
    ///
    /// 完全由代码生成，不依赖任何外部资源。
    /// </summary>
    public class BossWave : MonoBehaviour
    {
        [Header("波纹参数")]
        [SerializeField] private float maxRadius = 5f;
        [SerializeField] private float duration = 2f;
        [SerializeField] private int damage = 8;

        [Header("视觉")]
        [SerializeField] private float startAlpha = 0.5f;

        /// <summary>发射此波纹的 Boss 引用（由 DaoBoss 在生成时注入）</summary>
        public Enemy.DaoBoss ownerBoss;

        private float _elapsed;
        private CircleCollider2D _collider;
        private SpriteRenderer _spriteRenderer;
        private bool _hasHit;

        private void Awake()
        {
            _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.sprite = WeaponUtils.GetOrCreateRingSprite();

            // 调试阶段使用高亮红色，确保可见；正式版改回浅灰
            _spriteRenderer.color = new Color(1f, 0f, 0f, startAlpha);

            // 排序层级设为 10，高于地板(0)、敌人(0)、玩家(0)，确保不被遮挡
            _spriteRenderer.sortingOrder = 10;

            _collider = gameObject.AddComponent<CircleCollider2D>();
            _collider.isTrigger = true;
            _collider.radius = 0.5f; // 初始可见大小（半径 0.5 = 直径 1）

            Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            Debug.Log("[BossWave] 波纹已生成 @ " + transform.position);
        }

        /// <summary>
        /// 由 DaoBoss 调用，传入自定义参数和自身引用。
        /// </summary>
        public void Init(float radius, float dur, int dmg, Enemy.DaoBoss boss)
        {
            maxRadius = radius;
            duration = dur;
            damage = dmg;
            ownerBoss = boss;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / duration);

            // 从初始可见大小（0.5）扩大到 maxRadius
            float currentRadius = Mathf.Lerp(0.5f, maxRadius, t);
            float diameter = currentRadius * 2f;
            transform.localScale = new Vector3(diameter, diameter, 1f);

            _collider.radius = currentRadius;

            Color c = _spriteRenderer.color;
            c.a = Mathf.Lerp(startAlpha, 0f, t);
            _spriteRenderer.color = c;

            if (_elapsed >= duration)
            {
                Debug.Log("[BossWave] 波纹生命周期结束，自毁");
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            Debug.Log("[BossWave] 波纹已被销毁");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit) return;

            Player.PlayerMovement player = other.GetComponent<Player.PlayerMovement>();
            if (player != null)
            {
                player.TakeDamage(damage);
                _hasHit = true;
                Debug.Log("[BossWave] 命中玩家，伤害: " + damage);
            }
        }
    }
}
