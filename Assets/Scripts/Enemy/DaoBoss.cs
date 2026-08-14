using System.Collections;
using UnityEngine;
using HundredSchools.Core;

namespace HundredSchools.Enemy
{
    /// <summary>
    /// DaoBoss —— Boss 基类（三阶段版）。
    ///
    /// GDD §8：HP 100%-60% 阶段1，60%-30% 阶段2，30%-0% 阶段3。
    /// 每次跨阶段：全屏闪白 0.3s + 清除敌方弹幕 + 短暂无敌 1s + 广播 EventBus.OnBossPhaseChange。
    /// </summary>
    public class DaoBoss : EnemyBase
    {
        [Header("Boss 体型")]
        [SerializeField, Range(1f, 5f)] private float bossScale = 2f;

        [Header("弹幕参数")]
        [SerializeField, Range(3f, 15f)]  private float projectileSpeed = 6f;
        [SerializeField, Range(5, 30)]    private int projectileDamage = 12;

        private float _projectileTimer;
        private Transform _playerTransform;
        private bool _isActive;

        // 三阶段机制
        private int _currentPhase = 1;
        private bool _isTransitioning;
        private float _baseMoveSpeed;
        private float _phaseBaseScale;
        private bool _isTeleporting;

        // 阶段行为状态
        private readonly System.Collections.Generic.List<GameObject> _summonedMinions = new System.Collections.Generic.List<GameObject>();
        private int _maxMinions;
        private float _summonTimer;
        private float _executionTimer;
        private float _teleportTimer;
        private float _ringTimer;
        private float _vortexTimer;
        private Coroutine _auraCoroutine;
        private GameObject _arenaFloor;
        private readonly System.Collections.Generic.List<GameObject> _vortexMarkers = new System.Collections.Generic.List<GameObject>();

        protected override void Start()
        {
            base.Start();
            transform.localScale = Vector3.one * bossScale;

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _playerTransform = playerObj.transform;

            _baseMoveSpeed = moveSpeed;
            _phaseBaseScale = bossScale;
        }

        private void Update()
        {
            if (!_isActive || IsDead || _isTransitioning) return;

            if (_currentPhase >= 3 && !_isTeleporting)
            {
                float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 6f);
                transform.localScale = Vector3.one * _phaseBaseScale * pulse;
            }

            if (school != ESchool.Legalist)
                MoveTowardsPlayer();

            switch (school)
            {
                case ESchool.Confucian: UpdateConfucian(); break;
                case ESchool.Legalist:  UpdateLegalist();  break;
                case ESchool.Taoist:    UpdateTaoist();    break;
            }
        }

        private void UpdateConfucian()
        {
            float interval = 2f;
            _projectileTimer += Time.deltaTime;
            if (_projectileTimer >= interval)
            {
                _projectileTimer = 0f;
                ShootConfucianProjectile();
            }

            _summonTimer += Time.deltaTime;
            float summonInterval = _currentPhase >= 3 ? 2f : 3f;
            _maxMinions = _currentPhase >= 3 ? 4 : 2;
            if (_currentPhase >= 2 && _summonTimer >= summonInterval)
            {
                _summonTimer = 0f;
                CleanupDeadMinions();
                if (_summonedMinions.Count < _maxMinions)
                    SummonDisciple(ESchool.Confucian);
            }
        }

        private void UpdateLegalist()
        {
            float interval = 1.5f;
            _projectileTimer += Time.deltaTime;
            if (_projectileTimer >= interval)
            {
                _projectileTimer = 0f;
                int burstCount = _currentPhase >= 3 ? 3 : (_currentPhase >= 2 ? 2 : 1);
                StartCoroutine(BurstShootLegalist(burstCount));
            }

            if (_currentPhase >= 3)
            {
                _executionTimer += Time.deltaTime;
                if (_executionTimer >= 4f)
                {
                    _executionTimer = 0f;
                    StartCoroutine(SpawnExecutionGround());
                }
            }
        }

        private void UpdateTaoist()
        {
            if (_currentPhase >= 2)
            {
                _teleportTimer += Time.deltaTime;
                float tpInterval = _currentPhase >= 3 ? 3f : 5f;
                if (_teleportTimer >= tpInterval)
                {
                    _teleportTimer = 0f;
                    StartCoroutine(Teleport());
                }
            }

            _ringTimer += Time.deltaTime;
            float ringInterval = _currentPhase >= 3 ? 1.5f : (_currentPhase >= 2 ? 2f : 2.5f);
            if (_ringTimer >= ringInterval)
            {
                _ringTimer = 0f;
                int count = _currentPhase >= 3 ? 16 : (_currentPhase >= 2 ? 12 : 8);
                ShootRing(count);
            }

            if (_currentPhase >= 3)
            {
                _vortexTimer += Time.deltaTime;
                if (_vortexTimer >= 3f)
                {
                    _vortexTimer = 0f;
                    StartCoroutine(ShootVortex());
                }
            }
        }

        /// <summary>激活 Boss，由 WaveSpawner 在生成后调用。</summary>
        public void ActivateBoss()
        {
            _isActive = true;

            if (school == ESchool.Taoist)
            {
                CreateArenaFloor();
            }
        }

        private void CreateArenaFloor()
        {
            float arenaRadius = Mathf.Min(GameManager.Instance.roomSize.x, GameManager.Instance.roomSize.y) * 0.5f * 0.8f;

            _arenaFloor = new GameObject("ArenaFloor");
            _arenaFloor.transform.position = Vector3.zero;

            var sr = _arenaFloor.AddComponent<SpriteRenderer>();
            sr.sprite = Combat.WeaponUtils.GetOrCreateCircleSprite();
            sr.color = new Color(0.3f, 0.8f, 0.8f, 0.2f);
            sr.sortingOrder = -1;

            float diameter = arenaRadius * 2f;
            _arenaFloor.transform.localScale = new Vector3(diameter, diameter, 1f);

            Debug.Log($"[DaoBoss] 场地底色已创建: radius={arenaRadius}, scale=({diameter},{diameter}), alpha=0.2");
        }

        private void ShowVortexRing()
        {
            var marker = new GameObject("VortexRing");
            marker.transform.position = transform.position;

            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = Combat.WeaponUtils.GetOrCreateRingSprite();
            sr.color = new Color(1f, 1f, 1f, 0.5f);
            sr.sortingOrder = 2;
            marker.transform.localScale = Vector3.one * 4f; // 直径4单位

            _vortexMarkers.Add(marker);
            StartCoroutine(FadeAndDestroy(marker, 2f));

            Debug.Log($"[DaoBoss] 气旋标记已创建 @ {transform.position}");
        }

        private System.Collections.IEnumerator FadeAndDestroy(GameObject obj, float duration)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr == null) yield break;

            float elapsed = 0f;
            Color c0 = sr.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(c0.a, 0f, elapsed / duration);
                sr.color = new Color(c0.r, c0.g, c0.b, a);
                yield return null;
            }

            _vortexMarkers.Remove(obj);
            Destroy(obj);
        }

        /// <summary>覆写 Init，根据学派调整 Boss 初始参数。</summary>
        public new void Init(ESchool s, float speed, int hp, int score)
        {
            base.Init(s, speed, hp, score);

            switch (s)
            {
                case ESchool.Confucian:
                    moveSpeed = 7.5f;
                    projectileDamage = 12;
                    break;
                case ESchool.Legalist:
                    moveSpeed = 0f;
                    projectileDamage = 12;
                    break;
                case ESchool.Taoist:
                    moveSpeed *= 1.8f;
                    projectileDamage = 8;
                    break;
            }

            knowledgeValue = Mathf.RoundToInt(200 * (ConfigLoader.GetSchoolConfig(s)?.knowledgeCoeff ?? 1f)) + 100;
        }

        public override void TakeDamage(int damage)
        {
            if (IsDead || damage <= 0 || _isTransitioning) return;

            currentHp -= damage;

            if (currentHp <= 0)
            {
                currentHp = 0;
                Die();
                return;
            }

            // 阶段切换检测（HP阈值: 60%→阶段2, 30%→阶段3）
            float hpRatio = (float)currentHp / maxHp;
            if (_currentPhase == 1 && hpRatio <= 0.6f)
                StartCoroutine(EnterPhase(2));
            else if (_currentPhase == 2 && hpRatio <= 0.3f)
                StartCoroutine(EnterPhase(3));

            // 受击闪白
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashWhite());
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

        // ==================== 儒宗师攻击 ====================

        private void ShootConfucianProjectile()
        {
            if (_playerTransform == null) return;
            Vector3 dir = (_playerTransform.position - transform.position).normalized;
            var proj = CreateBullet(dir, projectileSpeed, projectileDamage);
            proj.behavior = Combat.EBulletBehavior.Splash;
        }

        private void SummonDisciple(ESchool discipleSchool)
        {
            Vector3 behind = transform.position - (_playerTransform != null
                ? (_playerTransform.position - transform.position).normalized
                : Vector3.right) * 2f;

            var obj = new GameObject($"Summoned_{discipleSchool}");
            obj.transform.position = behind + Random.insideUnitSphere * 1f;
            obj.transform.position = new Vector3(obj.transform.position.x, obj.transform.position.y, 0);

            var enemy = obj.AddComponent<EnemyBase>();
            var cfg = ConfigLoader.GetSchoolConfig(discipleSchool);
            int hp = 15; // 普通弟子的50%
            enemy.Init(discipleSchool, 2.5f, hp, 5);
            enemy.knowledgeValue = Mathf.RoundToInt(2.5f * (cfg?.knowledgeCoeff ?? 1f));

            var ai = obj.AddComponent<EnemyAI>();
            if (_playerTransform != null) ai.SetTarget(_playerTransform);

            obj.tag = "Enemy";
            _summonedMinions.Add(obj);
            GameManager.Instance?.OnEnemySpawned();
        }

        private void CleanupDeadMinions()
        {
            _summonedMinions.RemoveAll(m => m == null);
        }

        // ==================== 法宗师攻击 ====================

        private System.Collections.IEnumerator BurstShootLegalist(int count)
        {
            for (int i = 0; i < count; i++)
            {
                ShootLegalistProjectile();
                if (i < count - 1)
                    yield return new WaitForSeconds(0.2f);
            }
        }

        private void ShootLegalistProjectile()
        {
            if (_playerTransform == null) return;
            Vector3 dir = (_playerTransform.position - transform.position).normalized;
            float spd = projectileSpeed * (_currentPhase >= 2 ? 1.3f : 1f);
            var proj = CreateBullet(dir, spd, projectileDamage);
            proj.behavior = Combat.EBulletBehavior.Pierce;
            proj.pierceCount = 1;
        }

        private System.Collections.IEnumerator SpawnExecutionGround()
        {
            if (_playerTransform == null) yield break;

            Vector3 targetPos = _playerTransform.position;
            float radius = 1.5f;

            // 红色圆形标记
            var marker = new GameObject("ExecutionGround");
            marker.transform.position = targetPos;
            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = Combat.WeaponUtils.GetOrCreateRingSprite();
            sr.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            sr.sortingOrder = 4;
            marker.transform.localScale = Vector3.one * radius * 2f;

            yield return new WaitForSeconds(1f);

            // 爆发伤害
            var hits = Physics2D.OverlapCircleAll(targetPos, radius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    var pm = hit.GetComponent<Player.PlayerMovement>();
                    pm?.TakeDamage(15);
                }
            }

            Destroy(marker);
        }

        // ==================== 道宗师攻击 ====================

        private System.Collections.IEnumerator Teleport()
        {
            _isTeleporting = true;

            // 缩小消失
            float shrinkDuration = 0.1f;
            float t = 0f;
            Vector3 originalScale = transform.localScale;
            while (t < shrinkDuration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t / shrinkDuration);
                yield return null;
            }

            // 瞬移到随机位置（距玩家至少3单位）
            Vector3 newPos;
            int attempts = 0;
            do
            {
                float x = Random.Range(-8f, 8f);
                float y = Random.Range(-5f, 5f);
                newPos = new Vector3(x, y, 0);
                attempts++;
            }
            while (_playerTransform != null
                   && Vector3.Distance(newPos, _playerTransform.position) < 3f
                   && attempts < 20);

            transform.position = newPos;

            // 放大出现
            t = 0f;
            float growDuration = 0.1f;
            while (t < growDuration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, t / growDuration);
                yield return null;
            }
            transform.localScale = originalScale;
            _isTeleporting = false;
        }

        private void ShootRing(int count)
        {
            float angleStep = 360f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                var proj = CreateBullet(dir, projectileSpeed, projectileDamage);
                proj.behavior = Combat.EBulletBehavior.Normal;
            }
        }

        private System.Collections.IEnumerator ShootVortex()
        {
            ShowVortexRing();

            int count = 8;
            float angleStep = 360f / count;
            float duration = 2f;
            float elapsed = 0f;
            float rotationSpeed = 180f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float baseAngle = elapsed * rotationSpeed * Mathf.Deg2Rad;
                for (int i = 0; i < count; i++)
                {
                    float angle = baseAngle + i * angleStep * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                    var proj = CreateBullet(dir, projectileSpeed * 0.8f, Mathf.RoundToInt(projectileDamage * 0.7f));
                    proj.behavior = Combat.EBulletBehavior.Normal;
                    proj.gameObject.transform.localScale = Vector3.one * 0.5f;
                }
                yield return new WaitForSeconds(0.15f);
            }
        }

        // ==================== 弹幕工厂 ====================

        private Combat.ProjectileBase CreateBullet(Vector3 dir, float speed, int dmg)
        {
            Vector3 spawnPos = transform.position + dir * 1.5f;
            var bulletObj = new GameObject("Bullet_DaoBoss");
            bulletObj.transform.position = spawnPos;

            var sr = bulletObj.AddComponent<SpriteRenderer>();
            sr.sprite = Combat.WeaponUtils.GetOrCreateSquareSprite();
            sr.sortingOrder = 3;
            bulletObj.transform.localScale = Vector3.one * 0.8f;

            var proj = bulletObj.AddComponent<Combat.ProjectileBase>();
            proj.Init(dir, speed, dmg, school);
            proj.SetOwner(gameObject);
            proj.IsEnemyProjectile = true;

            var col = bulletObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one * 0.5f;

            var rb = bulletObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            return proj;
        }

        // ==================== 三阶段机制 ====================

        private System.Collections.IEnumerator EnterPhase(int phase)
        {
            _isTransitioning = true;
            _currentPhase = phase;
            Debug.Log($"[DaoBoss] 进入阶段 {phase}！HP={currentHp}/{maxHp}");

            // 1. 清除场上所有敌方弹幕
            var allBullets = FindObjectsOfType<Combat.ProjectileBase>();
            foreach (var b in allBullets)
            {
                if (b.IsEnemyProjectile)
                    Destroy(b.gameObject);
            }

            // 2. 全屏闪白 0.3s
            yield return StartCoroutine(FlashScreen());

            // 3. 广播阶段切换事件
            EventBus.TriggerBossPhaseChange(phase);

            // 4. 短暂无敌 1s（含闪白时间，剩余 0.7s）
            yield return new WaitForSeconds(0.7f);

            // 5. 应用阶段视觉变化（闪白结束后再设，避免被覆盖）
            ApplyPhaseVisual(_currentPhase);

            _isTransitioning = false;

            // 6. 阶段行为
            OnPhaseBehavior(phase);
        }

        private System.Collections.IEnumerator FlashScreen()
        {
            var flash = new GameObject("BossPhaseFlash");
            var sr = flash.AddComponent<SpriteRenderer>();
            sr.sprite = Combat.WeaponUtils.GetOrCreateSquareSprite();
            sr.color = Color.white;
            sr.sortingOrder = 999;

            var cam = Camera.main;
            if (cam != null)
            {
                float height = cam.orthographicSize * 2f;
                float width = height * cam.aspect;
                flash.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0);
                flash.transform.localScale = new Vector3(width, height, 1f);
            }

            float elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                sr.color = new Color(1, 1, 1, 1f - elapsed / 0.3f);
                yield return null;
            }

            Destroy(flash);
        }

        private void ApplyPhaseVisual(int phase)
        {
            switch (phase)
            {
                case 1:
                    _phaseBaseScale = bossScale;
                    if (spriteRenderer != null) spriteRenderer.color = originalColor;
                    transform.localScale = Vector3.one * _phaseBaseScale;
                    break;
                case 2:
                    _phaseBaseScale = bossScale * 1.15f;
                    if (spriteRenderer != null) spriteRenderer.color = Color.Lerp(originalColor, Color.red, 0.4f);
                    transform.localScale = Vector3.one * _phaseBaseScale;
                    break;
                case 3:
                    _phaseBaseScale = bossScale * 1.3f;
                    if (spriteRenderer != null) spriteRenderer.color = Color.Lerp(originalColor, Color.red, 0.7f);
                    break;
            }
        }

        protected virtual void OnPhaseBehavior(int phase)
        {
            Debug.Log($"[DaoBoss] OnPhaseBehavior: school={school} phase={phase}");

            switch (school)
            {
                case ESchool.Confucian:
                    OnConfucianPhase(phase);
                    break;
                case ESchool.Legalist:
                    OnLegalistPhase(phase);
                    break;
                case ESchool.Taoist:
                    OnTaoistPhase(phase);
                    break;
            }
        }

        private void OnConfucianPhase(int phase)
        {
            if (phase == 2)
            {
                moveSpeed = _baseMoveSpeed * 1.5f;
                if (_auraCoroutine != null) StopCoroutine(_auraCoroutine);
                _auraCoroutine = StartCoroutine(BenevolenceAura(2f, 5));
            }
            else if (phase == 3)
            {
                moveSpeed = _baseMoveSpeed * 2f;
                if (_auraCoroutine != null) StopCoroutine(_auraCoroutine);
                _auraCoroutine = StartCoroutine(BenevolenceAura(3f, 8));
            }
        }

        private void OnLegalistPhase(int phase)
        {
            // 法宗师各阶段均站桩，速度由 Init 设为 0
        }

        private void OnTaoistPhase(int phase)
        {
            if (phase >= 2)
                moveSpeed = _baseMoveSpeed * 1.3f;
        }

        /// <summary>仁义光环：Boss 周围圆形区域，玩家在内持续受伤</summary>
        private System.Collections.IEnumerator BenevolenceAura(float radius, int damagePerTick)
        {
            while (true)
            {
                yield return new WaitForSeconds(0.5f);
                if (_playerTransform == null) continue;

                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                if (dist <= radius)
                {
                    var pm = _playerTransform.GetComponent<Player.PlayerMovement>();
                    pm?.TakeDamage(damagePerTick);
                }
            }
        }

        // ==================== 死亡 ====================

        protected override void Die()
        {
            if (_auraCoroutine != null)
            {
                StopCoroutine(_auraCoroutine);
                _auraCoroutine = null;
            }

            // 清理召唤弟子
            CleanupDeadMinions();
            foreach (var m in _summonedMinions)
            {
                if (m != null) Destroy(m);
            }
            _summonedMinions.Clear();

            // 死亡爆炸：8 个大型波纹
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 2f;
                SpawnWave(transform.position + offset, 7.5f, 3f, projectileDamage);
            }

            if (_arenaFloor != null)
            {
                Destroy(_arenaFloor);
                _arenaFloor = null;
            }

            foreach (var m in _vortexMarkers)
            {
                if (m != null) Destroy(m);
            }
            _vortexMarkers.Clear();

            Debug.Log("[DaoBoss] ★ Boss 已被击败！");
            EventBus.TriggerBossKilled();
            base.Die();
        }
    }
}
