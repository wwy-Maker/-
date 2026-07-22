using System.Collections;
using UnityEngine;
using HundredSchools.Core;

namespace HundredSchools.Enemy
{
    /// <summary>
    /// DaoBoss —— Boss 基类（单阶段版）。
    ///
    /// GDD 骨灰版：每个 Boss 只保留一种核心行为模式。
    ///   儒宗师：高速逼近 + 近战礼击伤
    ///   法宗师：站桩发射追踪弹幕
    ///   道宗师：高速移动 + 周期性波纹扩散
    ///
    /// 统一行为：向玩家移动，周期性产生波纹。
    /// 不同学派通过 Init 参数调整速度/频率/伤害。
    /// </summary>
    public class DaoBoss : EnemyBase
    {
        [Header("Boss 体型")]
        [SerializeField, Range(1f, 5f)] private float bossScale = 2f;

        [Header("波纹参数")]
        [SerializeField, Range(0.2f, 3f)]  private float waveInterval = 0.5f;
        [SerializeField, Range(3f, 10f)]   private float waveRadius = 5f;
        [SerializeField, Range(0.5f, 3f)]  private float waveDuration = 2f;
        [SerializeField, Range(5, 30)]     private int waveDamage = 8;

        [Header("弹幕参数（法宗师专用）")]
        [SerializeField, Range(1f, 10f)]  private float projectileInterval = 2.5f;
        [SerializeField, Range(3f, 15f)]  private float projectileSpeed = 6f;
        [SerializeField, Range(5, 30)]    private int projectileDamage = 12;

        private float _waveTimer;
        private float _projectileTimer;
        private Transform _playerTransform;
        private bool _isActive;
        private bool _shootsProjectiles; // 法宗师会发射弹幕

        protected override void Start()
        {
            base.Start();
            transform.localScale = Vector3.one * bossScale;

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _playerTransform = playerObj.transform;
        }

        private void Update()
        {
            if (!_isActive || IsDead) return;

            MoveTowardsPlayer();

            // 波纹攻击
            _waveTimer += Time.deltaTime;
            if (_waveTimer >= waveInterval)
            {
                _waveTimer -= waveInterval;
                SpawnWave(transform.position, waveRadius, waveDuration, waveDamage);
            }

            // 法宗师：额外发射追踪弹幕
            if (_shootsProjectiles && _playerTransform != null)
            {
                _projectileTimer += Time.deltaTime;
                if (_projectileTimer >= projectileInterval)
                {
                    _projectileTimer = 0f;
                    ShootProjectile();
                }
            }
        }

        /// <summary>激活 Boss，由 WaveSpawner 在生成后调用。</summary>
        public void ActivateBoss()
        {
            _isActive = true;
        }

        /// <summary>覆写 Init，根据学派调整 Boss 行为参数。</summary>
        public new void Init(ESchool s, float speed, int hp, int score)
        {
            base.Init(s, speed, hp, score);

            // 根据学派调整行为（GDD §7 三Boss差异化）
            switch (s)
            {
                case ESchool.Confucian:
                    // 儒宗师：高速逼近（1.5×玩家移速≈7.5）+ 低频扩散弹幕
                    moveSpeed = 7.5f;
                    waveInterval = 1.5f;
                    waveDamage = 12;
                    _shootsProjectiles = false;
                    break;
                case ESchool.Legalist:
                    // 法宗师：站桩（不移位）+ 高频锁定追踪弹幕
                    moveSpeed = 0f;
                    waveInterval = 10f;    // 几乎不发波纹
                    waveDamage = 0;
                    _shootsProjectiles = true;
                    projectileInterval = 0.67f;  // 1.5发/秒
                    projectileDamage = 12;
                    break;
                case ESchool.Taoist:
                    // 道宗师：高速游走（1.8×）+ 高频波纹扩散
                    moveSpeed *= 1.8f;
                    waveInterval = 0.5f;
                    waveDamage = 8;
                    _shootsProjectiles = false;
                    break;
            }

            knowledgeValue = Mathf.RoundToInt(200 * (ConfigLoader.GetSchoolConfig(s)?.knowledgeCoeff ?? 1f)) + 100;
        }

        public override void TakeDamage(int damage)
        {
            base.TakeDamage(damage);
        }

        private void MoveTowardsPlayer()
        {
            if (_playerTransform != null)
                MoveTowards(_playerTransform.position);
        }

        private void SpawnWave(Vector3 pos, float radius, float duration, int dmg)
        {
            var waveObj = new GameObject("BossWave");
            waveObj.transform.position = pos;
            var wave = waveObj.AddComponent<Combat.BossWave>();
            wave.Init(radius, duration, dmg, this);
        }

        private void ShootProjectile()
        {
            if (_playerTransform == null) return;

            Vector3 dir = (_playerTransform.position - transform.position).normalized;
            Vector3 spawnPos = transform.position + dir * 1.5f;

            var bulletObj = new GameObject("Bullet_DaoBoss");
            bulletObj.transform.position = spawnPos;

            var sr = bulletObj.AddComponent<SpriteRenderer>();
            sr.sprite = Combat.WeaponUtils.GetOrCreateSquareSprite();
            sr.sortingOrder = 3;
            bulletObj.transform.localScale = Vector3.one * 0.8f;

            var proj = bulletObj.AddComponent<Combat.ProjectileBase>();
            proj.Init(dir, projectileSpeed, projectileDamage, school);
            proj.behavior = Combat.EBulletBehavior.Normal;
            proj.SetOwner(gameObject);

            var col = bulletObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one * 0.5f;

            var rb = bulletObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        protected override void Die()
        {
            // 死亡爆炸：8 个大型波纹
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 2f;
                SpawnWave(transform.position + offset, waveRadius * 1.5f, waveDuration * 1.5f, waveDamage);
            }

            Debug.Log("[DaoBoss] ★ Boss 已被击败！");
            base.Die();
        }
    }
}
