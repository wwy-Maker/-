using System.Collections;
using UnityEngine;
using HundredSchools.Core;

namespace HundredSchools.Enemy
{
    /// <summary>
    /// EnemyBase —— 所有敌人的基类。
    /// 灰阶可辨原则：通过形状+颜色区分学派，无需外部图片。
    ///   儒家：金色圆形 (CircleCollider2D) —— 稳重、逼近
    ///   法家：黑色正方形 (BoxCollider2D) —— 锐利、直线
    ///   道家：青色三角形 (PolygonCollider2D) —— 飘忽、灵动
    /// </summary>
    public class EnemyBase : MonoBehaviour
    {
        [Header("基础属性")]
        [SerializeField, Range(1, 500)]
        protected int maxHp = 30;

        [SerializeField, Range(0.5f, 15f)]
        protected float moveSpeed = 3f;

        [SerializeField]
        protected ESchool school = ESchool.Confucian;

        [SerializeField, Range(0, 100)]
        protected int scoreValue = 10;

        /// <summary>学识掉落值（GDD：普通5×学派系数，精英25×系数，Boss 200×系数+100）</summary>
        public int knowledgeValue = 5;

        /// <summary>是否为精英变体</summary>
        public bool IsElite;
        /// <summary>精英特殊行为学派</summary>
        public ESchool EliteAffinity;

        protected int currentHp;
        public bool IsDead => currentHp <= 0;

        protected SpriteRenderer spriteRenderer;
        protected Rigidbody2D rb;
        protected Collider2D col;
        protected Color originalColor;
        protected Coroutine flashCoroutine;
        private bool _isFrozen;
        private Coroutine _freezeCoroutine;

        private Flow.WaveSpawner _waveSpawner;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        protected virtual void Start()
        {
            currentHp = maxHp;
            ApplySchoolVisual();
            _waveSpawner = FindObjectOfType<Flow.WaveSpawner>();
        }

        protected virtual void ApplySchoolVisual()
        {
            Collider2D existingCol = GetComponent<Collider2D>();
            if (existingCol != null && existingCol != col)
                Destroy(existingCol);

            switch (school)
            {
                case ESchool.Confucian: SetupConfucianVisual(); break;
                case ESchool.Legalist:  SetupLegalistVisual(); break;
                case ESchool.Taoist:    SetupTaoistVisual(); break;
            }

            originalColor = spriteRenderer.color;
            transform.localScale = Vector3.one;
        }

        private void SetupConfucianVisual()
        {
            spriteRenderer.sprite = Combat.WeaponUtils.GetOrCreateCircleSprite();
            spriteRenderer.color = Color.yellow;
            CircleCollider2D c = gameObject.AddComponent<CircleCollider2D>();
            c.isTrigger = false; // 非 Trigger → 与玩家产生物理碰撞
            c.radius = 0.5f;
            col = c;
        }

        private void SetupLegalistVisual()
        {
            spriteRenderer.sprite = Combat.WeaponUtils.GetOrCreateSquareSprite();
            spriteRenderer.color = Color.black;
            BoxCollider2D b = gameObject.AddComponent<BoxCollider2D>();
            b.isTrigger = false; // 非 Trigger → 与玩家产生物理碰撞
            b.size = Vector2.one;
            col = b;
        }

        private void SetupTaoistVisual()
        {
            spriteRenderer.sprite = Combat.WeaponUtils.GetOrCreateTriangleSprite();
            spriteRenderer.color = Color.cyan;
            PolygonCollider2D p = gameObject.AddComponent<PolygonCollider2D>();
            p.points = new Vector2[] {
                new Vector2(0f, 0.44f),
                new Vector2(-0.44f, -0.44f),
                new Vector2(0.44f, -0.44f)
            };
            p.isTrigger = false; // 非 Trigger → 与玩家产生物理碰撞
            col = p;
        }

        public virtual void TakeDamage(int damage)
        {
            if (IsDead || damage <= 0 || _isFrozen) return;

            // 道系精英：30% 概率闪避
            if (IsElite && EliteAffinity == ESchool.Taoist && Random.value < 0.3f)
            {
                StartCoroutine(DodgeFlash());
                return;
            }

            currentHp -= damage;
            if (currentHp <= 0) { currentHp = 0; Die(); }
            else
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(FlashWhite());
            }
        }

        protected virtual IEnumerator FlashWhite()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
                yield return new WaitForSeconds(0.1f);
                spriteRenderer.color = originalColor;
            }
            flashCoroutine = null;
        }

        protected virtual void Die()
        {
            int finalKnowledge = knowledgeValue;
            var gm = GameManager.Instance;
            if (gm != null)
                finalKnowledge = Mathf.RoundToInt(finalKnowledge * gm.GetDifficultyConfig().knowledgeMult);
            EventBus.TriggerEnemyKilled(transform.position, finalKnowledge);
            Destroy(gameObject);
        }

        public virtual void MoveTowards(Vector3 targetPosition)
        {
            if (_isFrozen) return;
            Vector3 dir = (targetPosition - transform.position).normalized;
            rb.MovePosition(transform.position + dir * moveSpeed * Time.fixedDeltaTime);
        }

        // ==================== 学派弹幕攻击系统（GDD v1.9） ====================

        [Header("攻击配置")]
        [SerializeField, Range(2f, 15f)] private float shootInterval = 3f;
        [SerializeField, Range(3f, 15f)] private float projectileSpeed = 5f;
        [SerializeField, Range(5, 30)] private int projectileDamage = 10;
        [SerializeField, Range(3f, 20f)] private float shootRange = 8f;
        private float _shootTimer;

        private Transform _cachedPlayerTransform;

        private Transform GetPlayer()
        {
            if (_cachedPlayerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _cachedPlayerTransform = p.transform;
            }
            return _cachedPlayerTransform;
        }

        /// <summary>
        /// 由 EnemyAI.Update() 每帧调用。检查射击条件，按学派分流攻击。
        /// </summary>
        public void TryShoot(Transform playerTarget)
        {
            if (IsDead || _isFrozen) return;
            if (playerTarget == null) return;

            float dist = Vector3.Distance(transform.position, playerTarget.position);
            if (dist > shootRange) return;

            _shootTimer -= Time.deltaTime;
            if (_shootTimer > 0f) return;
            _shootTimer = shootInterval + Random.Range(-0.3f, 0.3f);

            switch (school)
            {
                case ESchool.Confucian: Shoot_Confucian(playerTarget); break;
                case ESchool.Legalist:  Shoot_Legalist(playerTarget);  break;
                case ESchool.Taoist:    Shoot_Taoist(playerTarget);    break;
                default: break;
            }
        }

        // ==================== 儒家弹幕：圆形·金色·溅射 ====================

        private void Shoot_Confucian(Transform target)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            GameObject bulletObj = CreateBullet("Bullet_Confucian", dir);
            if (bulletObj == null) return;

            // 圆形 Sprite
            bulletObj.GetComponent<SpriteRenderer>().sprite = Combat.WeaponUtils.GetOrCreateCircleSprite();

            Combat.ProjectileBase proj = bulletObj.GetComponent<Combat.ProjectileBase>();
            proj.behavior = Combat.EBulletBehavior.Splash;
            proj.damageMultiplier = 0.8f;
            if (IsElite) proj.IsSplash = true;

            Debug.Log("[EnemyBase] 儒家弹幕发射！（圆形·金色·溅射）");
        }

        // ==================== 法家弹幕：锐三角·黑色·穿透 ====================

        private void Shoot_Legalist(Transform target)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            GameObject bulletObj = CreateBullet("Bullet_Legalist", dir, 1.3f);
            if (bulletObj == null) return;

            // 锐角三角形 Sprite
            bulletObj.GetComponent<SpriteRenderer>().sprite = Combat.WeaponUtils.GetOrCreateTriangleSprite();

            Combat.ProjectileBase proj = bulletObj.GetComponent<Combat.ProjectileBase>();
            proj.behavior = Combat.EBulletBehavior.Pierce;
            proj.pierceCount = 1;
            proj.damageMultiplier = 1.2f;
            if (IsElite) proj.IsTracking = true;

            Debug.Log("[EnemyBase] 法家弹幕发射！（锐三角·黑色·穿透）");
        }

        // ==================== 道家弹幕：弯月·青色·回转 ====================

        private void Shoot_Taoist(Transform target)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            GameObject bulletObj = CreateBullet("Bullet_Taoist", dir);
            if (bulletObj == null) return;

            // 弯月形 Sprite
            bulletObj.GetComponent<SpriteRenderer>().sprite = Combat.WeaponUtils.GetOrCreateCrescentSprite();

            Combat.ProjectileBase proj = bulletObj.GetComponent<Combat.ProjectileBase>();
            proj.behavior = Combat.EBulletBehavior.Return;
            proj.returnTarget = target;
            proj.damageMultiplier = 0.6f;

            Debug.Log("[EnemyBase] 道家弹幕发射！（弯月·青色·回转）");
        }

        // ==================== 子弹创建工厂 ====================

        private GameObject CreateBullet(string name, Vector3 direction, float scale = 1.0f)
        {
            Vector3 spawnPos = transform.position + direction * 1.2f;

            GameObject bulletObj = new GameObject(name);
            bulletObj.transform.position = spawnPos;

            SpriteRenderer sr = bulletObj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3;

            bulletObj.transform.localScale = Vector3.one * scale;

            Combat.ProjectileBase proj = bulletObj.AddComponent<Combat.ProjectileBase>();
            proj.Init(direction, projectileSpeed, projectileDamage, school);
            proj.SetOwner(gameObject);
            proj.IsEnemyProjectile = true;

            BoxCollider2D col = bulletObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one * 0.5f;

            Rigidbody2D rb = bulletObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            return bulletObj;
        }

        // ==================== 公开接口 ====================

        public void Init(ESchool s, float speed, int hp, int score)
        {
            school = s;
            moveSpeed = speed;
            maxHp = hp;
            currentHp = hp;
            scoreValue = score;

            var schoolCfg = ConfigLoader.GetSchoolConfig(s);
            float coeff = schoolCfg?.knowledgeCoeff ?? 1.0f;
            knowledgeValue = Mathf.RoundToInt(5 * coeff);

            // 难度乘数（作用于基础值）
            var gm = GameManager.Instance;
            if (gm != null)
            {
                var dc = gm.GetDifficultyConfig();
                maxHp = Mathf.RoundToInt(maxHp * dc.hpMult);
                currentHp = maxHp;
                moveSpeed *= dc.spdMult;
                shootInterval /= dc.fireRateMult;
            }

            ApplySchoolVisual();
            if (_waveSpawner == null)
                _waveSpawner = FindObjectOfType<Flow.WaveSpawner>();
        }

        public void Freeze(float duration)
        {
            if (_freezeCoroutine != null) StopCoroutine(_freezeCoroutine);
            _freezeCoroutine = StartCoroutine(FreezeCoroutine(duration));
        }

        private System.Collections.IEnumerator FreezeCoroutine(float duration)
        {
            _isFrozen = true;
            var sr = GetComponent<SpriteRenderer>();
            Color frozen = sr != null ? sr.color : Color.white;
            if (sr != null) sr.color = new Color(0.3f, 0.7f, 1f, 1f); // 青蓝色
            yield return new WaitForSeconds(duration);
            _isFrozen = false;
            if (sr != null) sr.color = frozen;
        }

        public void InitElite(ESchool eliteSchool)
        {
            IsElite = true;
            EliteAffinity = eliteSchool;
            maxHp *= 2;
            currentHp = maxHp;
            transform.localScale *= 1.6f;
            knowledgeValue *= 2;

            // 白色光环子物体 —— 一眼认精英
            var ringObj = new GameObject("EliteRing");
            ringObj.transform.SetParent(transform, false);
            ringObj.transform.localPosition = Vector3.zero;
            ringObj.transform.localScale = Vector3.one * 1.1f;
            var ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = Combat.WeaponUtils.GetOrCreateRingSprite();
            ringSr.color = Color.white;
            ringSr.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 0;
        }

        private System.Collections.IEnumerator DodgeFlash()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
                yield return new WaitForSeconds(0.1f);
                spriteRenderer.color = originalColor;
            }
        }

        public bool IsFrozen => _isFrozen;
        public ESchool School => school;
        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
    }
}
