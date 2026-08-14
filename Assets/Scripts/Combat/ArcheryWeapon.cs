using System.Collections;
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

        // ==================== 三档蓄力常量（chargeUnlocked=true 时启用） ====================

        private const float MaxChargeTime = 1.5f;
        private const float ChargeVisualScale = 1.4f;
        private const float Tier2Threshold = 0.3f;
        private const float Tier3Threshold = 1.0f;

        // ==================== 运行时状态 ====================

        private float _chargeTime;
        private bool _isCharging;
        private bool _chargeUnlocked;
        private float _cooldownTimer;
        private Vector3 _originalScale;
        private SpriteRenderer _playerSprite;

        // ==================== 升级加成（公开字段，由 UpgradeManager 修改） ====================

        /// <summary>伤害倍率（初始1.0，每次升级乘以1.15）</summary>
        [HideInInspector] public float damageMultiplier = 1f;

        /// <summary>攻速倍率（初始1.0，每次升级乘以1.10）</summary>
        [HideInInspector] public float attackSpeedMultiplier = 1f;

        /// <summary>额外弹幕数（扇形散射，间隔10度）</summary>
        [HideInInspector] public int extraProjectiles = 0;

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            _playerSprite = GetComponent<SpriteRenderer>();
            _originalScale = transform.localScale;
        }

        // ==================== 公开接口 ====================

        public void HandleInput()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;

            if (!_chargeUnlocked)
            {
                if (Input.GetMouseButtonDown(0) && _cooldownTimer <= 0f)
                    FireNormalShot();
                return;
            }

            // ── 三档蓄力模式 ──
            if (Input.GetMouseButtonDown(0))
            {
                _isCharging = true;
                _chargeTime = 0f;
            }

            if (Input.GetMouseButton(0) && _isCharging)
            {
                _chargeTime = Mathf.Min(_chargeTime + Time.deltaTime, MaxChargeTime);
                float t = Mathf.Clamp01(_chargeTime / MaxChargeTime);
                transform.localScale = _originalScale * Mathf.Lerp(1f, ChargeVisualScale, t);
            }

            if (Input.GetMouseButtonUp(0) && _isCharging)
            {
                _isCharging = false;
                transform.localScale = _originalScale;

                if (_cooldownTimer > 0f) { _chargeTime = 0f; return; }

                if (_chargeTime < Tier2Threshold)
                    FireNormalShot();
                else if (_chargeTime < Tier3Threshold)
                    FireMediumChargedShot();
                else
                    FireFullChargedShot();

                _chargeTime = 0f;
            }
        }

        /// <summary>应用 WeaponUpgradeEffect 到本武器组件。</summary>
        public void ApplyUpgradeEffect(Core.WeaponUpgradeEffect e)
        {
            if (e.damage > 0) damage = e.damage;
            if (e.fireRate > 0) cooldown = 1f / e.fireRate;
            extraProjectiles = e.extraProjectiles;
            damageMultiplier = 1f;
            attackSpeedMultiplier = 1f;
            _chargeUnlocked = e.chargeUnlocked;
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
            ESchool currentSchool = WeaponUtils.GetCurrentSchool(this);
            int finalDamage = Mathf.RoundToInt(damage * damageMultiplier);

            int totalBullets = 1 + extraProjectiles;
            for (int i = 0; i < totalBullets; i++)
            {
                float angleOffset = GetFanAngle(i);
                Vector3 dir = Quaternion.Euler(0, 0, angleOffset) * direction;
                FireSingleBullet(dir, 1f, finalDamage, bulletSpeed, currentSchool);
            }

            _cooldownTimer = cooldown / attackSpeedMultiplier;
        }

        private void FireMediumChargedShot()
        {
            FireChargedBullet(1.8f, 1.3f, 1.2f, 0);
        }

        private void FireFullChargedShot()
        {
            FireChargedBullet(3.0f, 1.6f, 1.5f, 1);
            StartCoroutine(ScreenShake());
        }

        /// <summary>发射蓄力弹幕。pierceCount=0 保持学派行为，>0 强制穿透。</summary>
        private void FireChargedBullet(float dmgMult, float spdMult, float scaleMult, int extraPierce)
        {
            Vector3 mouseWorld = WeaponUtils.GetMouseWorldPosition();
            Vector3 playerPos = transform.position;
            if (Vector3.Distance(mouseWorld, playerPos) < 0.1f) return;

            Vector3 direction = (mouseWorld - playerPos).normalized;
            ESchool currentSchool = WeaponUtils.GetCurrentSchool(this);
            int finalDamage = Mathf.RoundToInt(damage * dmgMult * damageMultiplier);
            float finalSpeed = bulletSpeed * spdMult;

            int totalBullets = 1 + extraProjectiles;
            for (int i = 0; i < totalBullets; i++)
            {
                float angleOffset = GetFanAngle(i);
                Vector3 dir = Quaternion.Euler(0, 0, angleOffset) * direction;
                var proj = FireSingleBullet(dir, scaleMult, finalDamage, finalSpeed, currentSchool);
                if (extraPierce > 0)
                {
                    proj.behavior = EBulletBehavior.Pierce;
                    proj.pierceCount = extraPierce;
                }
            }

            _cooldownTimer = cooldown / attackSpeedMultiplier;
        }

        private System.Collections.IEnumerator ScreenShake()
        {
            var cam = Camera.main;
            if (cam == null) yield break;

            Vector3 origin = cam.transform.position;
            float duration = 0.1f;
            float intensity = 0.12f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float decay = 1f - elapsed / duration;
                float x = Random.Range(-1f, 1f) * intensity * decay;
                float y = Random.Range(-1f, 1f) * intensity * decay;
                cam.transform.position = origin + new Vector3(x, y, 0);
                yield return null;
            }

            cam.transform.position = origin;
        }

        /// <summary>扇形散射角度计算。主弹幕0°，额外弹幕交替±10°、±20°……</summary>
        private float GetFanAngle(int index)
        {
            if (index == 0) return 0f;
            int step = (index + 1) / 2;
            float sign = (index % 2 == 1) ? 1f : -1f;
            return sign * step * 10f;
        }

        /// <summary>发射单发子弹，返回 ProjectileBase 供调用方进一步配置。</summary>
        private ProjectileBase FireSingleBullet(Vector3 dir, float visualScale, int dmg, float speed, ESchool school)
        {
            GameObject bullet = CreateBullet(dir, visualScale);
            ProjectileBase projectile = bullet.GetComponent<ProjectileBase>();
            projectile.Init(dir, speed, dmg, school);
            ApplyBulletBehavior(projectile, school);
            projectile.SetOwner(gameObject);
            return projectile;
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
