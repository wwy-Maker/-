using UnityEngine;
using HundredSchools.Core;

namespace HundredSchools.Economy
{
    /// <summary>
    /// KnowledgePickup —— 学识掉落物行为组件。
    ///
    /// 生命周期：
    ///   1. 对象池取出 → Init(amount, lifetime, attractRange, speed, player)
    ///   2. 每帧检测玩家距离，< attractRange 时自动吸附
    ///   3. 碰撞到玩家(OnTriggerEnter2D) → Collect() → 回池
    ///   4. 超时(8s) → Collect()（不触发 AddKnowledge，学识归零）
    ///   5. 最后 FadeOutDuration 秒淡出
    ///
    /// 挂载到：由 KnowledgeManager 创建/池化的掉落物 GameObject 上。
    /// </summary>
    public class KnowledgePickup : MonoBehaviour
    {
        private int _knowledgeAmount;
        private float _lifetime;
        private float _elapsed;
        private float _attractRange;
        private float _attractSpeed;
        private Transform _playerTransform;
        private SpriteRenderer _spriteRenderer;
        private bool _isActive;

        private const float FadeOutDuration = 1.5f;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// 从对象池取出后调用，初始化掉落物参数。
        /// </summary>
        public void Init(int amount, float lifetime, float attractRange, float attractSpeed, Transform player)
        {
            _knowledgeAmount = amount;
            _lifetime = lifetime;
            _elapsed = 0f;
            _attractRange = attractRange;
            _attractSpeed = attractSpeed;
            _playerTransform = player;
            _isActive = true;

            if (_spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                c.a = 1f;
                _spriteRenderer.color = c;
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            _elapsed += Time.deltaTime;

            float remainingTime = _lifetime - _elapsed;
            if (remainingTime <= 0f)
            {
                // 超时不触发学识获取，直接回池
                _isActive = false;
                KnowledgeManager.Instance?.ReturnToPool(gameObject);
                return;
            }

            // 最后 FadeOutDuration 秒淡出
            if (remainingTime <= FadeOutDuration && _spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                c.a = Mathf.Clamp01(remainingTime / FadeOutDuration);
                _spriteRenderer.color = c;
            }

            // 吸附：玩家距离 < attractRange 时向玩家移动
            if (_playerTransform != null)
            {
                float distance = Vector3.Distance(transform.position, _playerTransform.position);
                if (distance < _attractRange)
                {
                    Vector3 direction = (_playerTransform.position - transform.position).normalized;
                    transform.position += direction * _attractSpeed * Time.deltaTime;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isActive) return;
            if (other.CompareTag("Player"))
            {
                _isActive = false;
                KnowledgeManager.Instance?.AddKnowledge(_knowledgeAmount);
                KnowledgeManager.Instance?.ReturnToPool(gameObject);
            }
        }
    }
}
