using UnityEngine;

namespace HundredSchools.Combat
{
    /// <summary>
    /// ReflectedWave —— 反弹波（金色反击弹）。
    ///
    /// 由 RitualBarrier 生成，向 DaoBoss 飞行。
    /// 命中 Boss 时造成伤害并自毁；超时或飞出边界也自毁。
    ///
    /// 视觉：金色圆形，代表被礼艺转化后的能量。
    /// </summary>
    public class ReflectedWave : MonoBehaviour
    {
        [Header("飞行参数")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float maxLifetime = 4f;

        private Vector3 _direction;
        private int _damage = 30;
        private GameObject _targetBoss;
        private float _elapsed;

        private void Awake()
        {
            // 金色圆形视觉
            SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = WeaponUtils.GetOrCreateCircleSprite();
            sr.color = new Color(1f, 0.84f, 0f, 0.9f); // 金色
            sr.sortingOrder = 8;

            transform.localScale = Vector3.one * 1.5f;

            // 碰撞体
            CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        /// <summary>
        /// 初始化反弹波，由 RitualBarrier 调用。
        /// </summary>
        /// <param name="direction">飞行方向（单位向量）</param>
        /// <param name="damage">命中伤害</param>
        /// <param name="boss">目标 Boss GameObject（用于精确命中判定）</param>
        public void Init(Vector3 direction, int damage, GameObject boss)
        {
            _direction = direction.normalized;
            _damage = damage;
            _targetBoss = boss;

            // 朝向飞行方向
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            Debug.Log("[ReflectedWave] 初始化完成，伤害=" + damage);
        }

        private void Update()
        {
            // 飞行
            transform.position += _direction * moveSpeed * Time.deltaTime;

            // 超时自毁
            _elapsed += Time.deltaTime;
            if (_elapsed >= maxLifetime)
            {
                Debug.Log("[ReflectedWave] 超时自毁");
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 精确命中目标 Boss
            if (_targetBoss != null && other.gameObject == _targetBoss)
            {
                Enemy.DaoBoss boss = other.GetComponent<Enemy.DaoBoss>();
                if (boss != null)
                {
                    boss.TakeDamage(_damage);
                    Debug.Log("[ReflectedWave] 命中 DaoBoss！伤害=" + _damage + " Boss剩余HP=" + boss.CurrentHp);
                }
                Destroy(gameObject);
                return;
            }

            // 也接受命中普通 EnemyBase（兜底）
            if (_targetBoss == null)
            {
                Enemy.EnemyBase enemy = other.GetComponent<Enemy.EnemyBase>();
                if (enemy != null)
                {
                    enemy.TakeDamage(_damage);
                    Debug.Log("[ReflectedWave] 命中敌人 " + other.name + " 伤害=" + _damage);
                    Destroy(gameObject);
                }
            }
        }
    }
}
