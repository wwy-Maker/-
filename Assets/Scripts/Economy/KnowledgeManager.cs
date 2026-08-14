using System.Collections.Generic;
using UnityEngine;
using HundredSchools.Core;

namespace HundredSchools.Economy
{
    /// <summary>
    /// KnowledgeManager —— 学识经济系统管理器（单例）。
    ///
    /// 职责：
    ///   1. 维护玩家当前学识总量（_currentKnowledge）
    ///   2. 监听 OnEnemyKilled 事件，在死亡位置生成学识掉落物
    ///   3. 管理掉落物对象池（Queue<GameObject>, 初始 20, 动态扩容）
    ///   4. 对外暴露 SpendKnowledge(int)→bool / AddKnowledge(int)
    ///
    /// 挂载到：场景根级空 GameObject "KnowledgeManager" 上。
    /// 不设为 DontDestroyOnLoad —— 场景重载时随场景一起重建。
    /// </summary>
    public class KnowledgeManager : MonoBehaviour
    {
        public static KnowledgeManager Instance { get; private set; }

        [Header("对象池")]
        [SerializeField, Range(5, 100)] private int initialPoolSize = 20;

        [Header("掉落物参数")]
        [SerializeField, Range(2f, 30f)] private float pickupLifetime = 8f;
        [SerializeField, Range(0.3f, 3f)]  private float attractRange = 1f;
        [SerializeField, Range(2f, 20f)]  private float attractSpeed = 8f;

        /// <summary>当前学识总量</summary>
        public int CurrentKnowledge { get; private set; }

        private Queue<GameObject> _pool;
        private Transform _playerTransform;

        /// <summary>掉落物模板（static，跨 KnowledgeManager 实例复用，避免重复创建）</summary>
        private static GameObject _pickupTemplate;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _pool = new Queue<GameObject>(initialPoolSize);
        }

        private void Start()
        {
            if (_pickupTemplate == null)
                CreatePickupTemplate();

            PrewarmPool();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        private void OnEnable()
        {
            EventBus.OnEnemyKilled += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            EventBus.OnEnemyKilled -= HandleEnemyKilled;
        }

        // ==================== 对象池 ====================

        private void CreatePickupTemplate()
        {
            _pickupTemplate = new GameObject("KnowledgePickup_Template");
            _pickupTemplate.SetActive(false);
            DontDestroyOnLoad(_pickupTemplate);

            var sr = _pickupTemplate.AddComponent<SpriteRenderer>();
            sr.sprite = Combat.WeaponUtils.GetOrCreateCircleSprite();
            sr.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            sr.sortingOrder = 2;

            var col = _pickupTemplate.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.3f;

            var rb = _pickupTemplate.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            _pickupTemplate.AddComponent<KnowledgePickup>();
        }

        private void PrewarmPool()
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject obj = Instantiate(_pickupTemplate);
                obj.name = "KnowledgePickup";
                obj.SetActive(false);
                _pool.Enqueue(obj);
            }
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();

            GameObject obj = Instantiate(_pickupTemplate);
            obj.name = "KnowledgePickup";
            return obj;
        }

        /// <summary>将掉落物回收到对象池。</summary>
        public void ReturnToPool(GameObject obj)
        {
            if (obj == null) return;
            obj.SetActive(false);
            _pool.Enqueue(obj);
        }

        // ==================== 掉落生成 ====================

        private void HandleEnemyKilled(Vector3 position, int knowledgeValue)
        {
            if (knowledgeValue <= 0) return;
            SpawnPickup(position, knowledgeValue);
        }

        private void SpawnPickup(Vector3 position, int amount)
        {
            GameObject obj = GetFromPool();
            obj.transform.position = position;
            // Circle sprite 直径 = 1 unit, 要半径 0.15 → 直径 0.3 → scale 0.3
            obj.transform.localScale = Vector3.one * 0.3f;

            var pickup = obj.GetComponent<KnowledgePickup>();
            pickup.Init(amount, pickupLifetime, attractRange, attractSpeed, _playerTransform);

            obj.SetActive(true);
        }

        // ==================== 学识管理 ====================

        /// <summary>增加学识。拾取掉落物时由 KnowledgePickup 调用。</summary>
        public void AddKnowledge(int amount)
        {
            if (amount <= 0) return;
            CurrentKnowledge += amount;
            EventBus.TriggerKnowledgeChanged(CurrentKnowledge);
        }

        /// <summary>
        /// 消费学识。供升级系统调用。
        /// </summary>
        /// <returns>余额是否足够</returns>
        public bool SpendKnowledge(int cost)
        {
            if (cost <= 0) return true;
            if (CurrentKnowledge < cost) return false;
            CurrentKnowledge -= cost;
            EventBus.TriggerKnowledgeChanged(CurrentKnowledge);
            return true;
        }
    }
}
