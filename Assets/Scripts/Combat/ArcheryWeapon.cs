using UnityEngine;

namespace HundredSchools.Combat
{
    /// <summary>
    /// ArcheryWeapon —— 射艺攻击组件。
    ///
    /// 职责：鼠标左键射击 + 长按蓄力机制。
    /// 从旧 PlayerCombat 提取而来，保持功能完全一致。
    ///
    /// 挂载到：Player GameObject（须有 SpriteRenderer 用于蓄力缩放反馈）
    /// 调用方式：由 PlayerCombat 在 Update 中调用 HandleInput()
    /// </summary>
    public class ArcheryWeapon : MonoBehaviour
    {
        // ==================== 序列化配置 ====================

        [Header("普通射击")]
        [SerializeField, Range(0f, 2f)]
        private float cooldown = 0.2f;

        [SerializeField, Range(1f, 30f)]
        private float bulletSpeed = 12f;

        [SerializeField, Range(1, 50)]
        private int damage = 10;

        [SerializeField, Range(0.3f, 3f)]
        private float spawnOffset = 1f;

        [Header("蓄力射击")]
        [SerializeField, Range(0.2f, 2f)]
        private float chargeThreshold = 0.5f;

        [SerializeField, Range(1.5f, 5f)]
        private float chargeDamageMultiplier = 2f;

        [SerializeField, Range(1f, 3f)]
        private float chargeSpeedMultiplier = 1.5f;

        [Header("蓄力视觉")]
        [SerializeField, Range(1f, 2f)]
        private float chargeMaxScale = 1.3f;

        // ==================== 运行时状态 ====================

        private float _chargeTime;
        private bool _isCharging;
        private float _cooldownTimer;
        private Vector3 _originalScale;
        private SpriteRenderer _playerSprite;

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            _playerSprite = GetComponent<SpriteRenderer>();
            _originalScale = transform.localScale;
        }

        // ==================== 公开接口 ====================

        /// <summary>
        /// 每帧由 PlayerCombat 调用。处理射艺的蓄力 → 射击状态机。
        /// </summary>
        public void HandleInput()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;

            // 左键按下：开始蓄力
            if (Input.GetMouseButtonDown(0))
            {
                _isCharging = true;
                _chargeTime = 0f;
            }

            // 按住中：蓄力 + 视觉膨胀
            if (Input.GetMouseButton(0) && _isCharging)
            {
                _chargeTime += Time.deltaTime;
                float t = Mathf.Clamp01(_chargeTime / chargeThreshold);
                transform.localScale = _originalScale * Mathf.Lerp(1f, chargeMaxScale, t);
            }

            // 松开左键：判定射击类型
            if (Input.GetMouseButtonUp(0) && _isCharging)
            {
                _isCharging = false;
                transform.localScale = _originalScale;

                if (_cooldownTimer > 0f) return;

                if (_chargeTime >= chargeThreshold)
                    FireChargedShot();
                else
                    FireNormalShot();

                _chargeTime = 0f;
            }
        }

        /// <summary>切换武器时重置状态，防止跨武器状态污染</summary>
        public void ResetState()
        {
            _isCharging = false;
            _chargeTime = 0f;
            _cooldownTimer = 0f;
            transform.localScale = _originalScale;
        }

        // ==================== 射击实现 ====================

        private void FireNormalShot()
        {
            Vector3 mouseWorld = WeaponUtils.GetMouseWorldPosition();
            Vector3 playerPos = transform.position;

            if (Vector3.Distance(mouseWorld, playerPos) < 0.1f) return;

            Vector3 direction = (mouseWorld - playerPos).normalized;
            GameObject bullet = CreateBullet(direction, 1f);
            ProjectileBase projectile = bullet.GetComponent<ProjectileBase>();

            ESchool currentSchool = WeaponUtils.GetCurrentSchool(this);
            projectile.Init(direction, bulletSpeed, damage, currentSchool);
            ApplyBulletBehavior(projectile, currentSchool);
            projectile.SetOwner(gameObject);
            _cooldownTimer = cooldown;
        }

        private void FireChargedShot()
        {
            Vector3 mouseWorld = WeaponUtils.GetMouseWorldPosition();
            Vector3 playerPos = transform.position;

            if (Vector3.Distance(mouseWorld, playerPos) < 0.1f) return;

            Vector3 direction = (mouseWorld - playerPos).normalized;
            GameObject bullet = CreateBullet(direction, 1.5f);
            ProjectileBase projectile = bullet.GetComponent<ProjectileBase>();

            int chargedDmg = Mathf.RoundToInt(damage * chargeDamageMultiplier);
            ESchool currentSchool = WeaponUtils.GetCurrentSchool(this);
            projectile.Init(direction, bulletSpeed * chargeSpeedMultiplier, chargedDmg, currentSchool);
            ApplyBulletBehavior(projectile, currentSchool);
            projectile.MarkAsCharged();
            projectile.SetOwner(gameObject);

            _cooldownTimer = cooldown;
        }

        /// <summary>
        /// GDD v1.9 "弹幕即思想"：根据玩家学派设置子弹行为。
        ///   儒家 → Splash（溅射）/ 法家 → Pierce（穿透）/ 道家 → Return（回转）
        ///   墨家 / 无学派 → Normal
        /// </summary>
        private void ApplyBulletBehavior(ProjectileBase projectile, ESchool school)
        {
            switch (school)
            {
                case ESchool.Confucian:
                    projectile.behavior = EBulletBehavior.Splash;
                    break;
                case ESchool.Legalist:
                    projectile.behavior = EBulletBehavior.Pierce;
                    projectile.pierceCount = 2;
                    break;
                case ESchool.Taoist:
                    projectile.behavior = EBulletBehavior.Return;
                    break;
                default:
                    projectile.behavior = EBulletBehavior.Normal;
                    break;
            }
        }

        // ==================== 子弹创建 ====================

        private GameObject CreateBullet(Vector3 direction, float visualScale)
        {
            Vector3 spawnPos = transform.position + direction * spawnOffset;

            GameObject bulletObj = new GameObject("Bullet_Archery");
            bulletObj.transform.position = spawnPos;

            SpriteRenderer sr = bulletObj.AddComponent<SpriteRenderer>();
            sr.sprite = WeaponUtils.GetOrCreateSquareSprite();
            sr.sortingOrder = 1;

            bulletObj.transform.localScale = Vector3.one * visualScale;
            bulletObj.AddComponent<ProjectileBase>();

            BoxCollider2D col = bulletObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one * 0.5f;

            Rigidbody2D rb = bulletObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            return bulletObj;
        }
    }
}
