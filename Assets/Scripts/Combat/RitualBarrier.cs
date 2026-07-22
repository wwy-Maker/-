using UnityEngine;

namespace HundredSchools.Combat
{
    /// <summary>
    /// RitualBarrier —— 礼屏障。
    ///
    /// 当玩家使用礼艺时，在玩家周围生成一个圆环形屏障。
    /// 检测到 BossWave 波纹时，销毁波纹并反弹一颗金色反击波。
    ///
    /// 完全由代码生成，零外部依赖。
    /// </summary>
    public class RitualBarrier : MonoBehaviour
    {
        [Header("屏障参数")]
        [SerializeField] private float radius = 5f;
        [SerializeField] private float duration = 0.8f;
        [SerializeField] private int reflectDamage = 30;

        private float _elapsed;
        private SpriteRenderer _sr;
        private CircleCollider2D _col;

        private void Awake()
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = WeaponUtils.GetOrCreateRingSprite();
            _sr.sortingOrder = 5;

            _col = gameObject.AddComponent<CircleCollider2D>();
            _col.isTrigger = true;

            Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        private void Start()
        {
            // Start 在 Init 之后执行，此时参数已由 Init 设好
            ApplyVisual();
            Debug.Log("[RitualBarrier] 礼屏障已展开 radius=" + radius + " damage=" + reflectDamage);
        }

        /// <summary>
        /// 由 RitualWeapon 调用，传入自定义参数。必须在 Start 之前调用。
        /// </summary>
        public void Init(float r, float dur, int dmg)
        {
            radius = r;
            duration = dur;
            reflectDamage = dmg;
        }

        private void ApplyVisual()
        {
            _sr.color = new Color(0f, 1f, 1f, 0.5f);
            float diameter = radius * 2f;
            transform.localScale = new Vector3(diameter, diameter, 1f);
            _col.radius = radius;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            // 逐渐变透明
            Color c = _sr.color;
            c.a = Mathf.Lerp(0.5f, 0f, _elapsed / duration);
            _sr.color = c;

            if (_elapsed >= duration)
            {
                Debug.Log("[RitualBarrier] 礼屏障消散");
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 拦截 Boss 波纹
            BossWave bossWave = other.GetComponent<BossWave>();
            if (bossWave != null)
            {
                Enemy.DaoBoss boss = bossWave.ownerBoss;
                if (boss != null && !boss.IsDead)
                {
                    Debug.Log("[RitualBarrier] 拦截到 BossWave，反弹至 " + boss.name);
                    SpawnReflectedWave(boss.gameObject, boss.transform.position, reflectDamage);
                    Destroy(other.gameObject);
                }
                else
                {
                    Debug.Log("[RitualBarrier] Boss 已不存在，仅清除波纹");
                    Destroy(other.gameObject);
                }
                return;
            }

            // ★ GDD v1.9：拦截普通敌人弹幕（ProjectileBase）
            ProjectileBase enemyBullet = other.GetComponent<ProjectileBase>();
            if (enemyBullet != null)
            {
                Debug.Log($"[RitualBarrier] 拦截敌人弹幕，反弹！");
                // 向弹幕来源的反方向发射反击波
                Vector3 reflectDir = -enemyBullet.transform.right;
                SpawnReflectedWave(null, transform.position + reflectDir * 2f, reflectDamage);
                Destroy(other.gameObject);
            }
        }

        /// <summary>
        /// 向指定目标发射一颗反弹波（金色反击弹）。
        /// </summary>
        /// <param name="targetObj">目标 GameObject（Boss），null 时向指定方向发射</param>
        /// <param name="targetPos">目标位置（用于计算方向）</param>
        /// <param name="dmg">反弹伤害</param>
        private void SpawnReflectedWave(GameObject targetObj, Vector3 targetPos, int dmg)
        {
            Vector3 direction = (targetPos - transform.position).normalized;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector3.right;

            GameObject waveObj = new GameObject("ReflectedWave");
            waveObj.transform.position = transform.position;

            ReflectedWave rw = waveObj.AddComponent<ReflectedWave>();
            rw.Init(direction, dmg, targetObj);

            Debug.Log("[RitualBarrier] 反击波发射！方向=" + direction + " 伤害=" + dmg);
        }
    }
}
