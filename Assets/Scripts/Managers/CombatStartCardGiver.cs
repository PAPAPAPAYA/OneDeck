using UnityEngine;
using Random = UnityEngine.Random;

namespace DefaultNamespace.Managers
{
    /// <summary>
    /// 战斗开始前从指定卡池随机赠卡给玩家
    /// </summary>
    public class CombatStartCardGiver : MonoBehaviour
    {
        [Header("卡池配置")]
        [Tooltip("从中随机选卡的源卡池")]
        public DeckSO rewardPoolDeck;
        
        [Tooltip("玩家卡组 (目标)")]
        public DeckSO playerDeck;
        
        [Header("可选限制")]
        [Tooltip("是否检查卡组容量上限")]
        public bool checkDeckSizeLimit = true;
        
        [Tooltip("卡组容量上限引用 (如果 checkDeckSizeLimit 为 true)")]
        public IntSO deckSizeLimit;
        
        [Tooltip("每次触发添加几张卡")]
        public int cardsToGive = 1;
        
        [Header("触发设置")]
        [Tooltip("仅第一次进入商店时触发")]
        public bool onlyFirstTime = true;
        
        [Header("调试")]
        public bool logAddedCard = true;

        // 内部状态：是否已经给过卡
        private bool _hasGivenCard = false;

        private void OnEnable()
        {
            // 重置状态，确保每次游戏运行时都能触发
            _hasGivenCard = false;
        }

        /// <summary>
        /// 从奖励卡池随机选卡添加到玩家卡组
        /// 可绑定到 onEnterShopPhase 事件
        /// </summary>
        public void GiveRandomCardFromPool()
        {
            // 检查是否仅限第一次
            if (onlyFirstTime && _hasGivenCard)
            {
                return;
            }
            
            // 验证源卡池
            if (rewardPoolDeck == null || rewardPoolDeck.deck.Count == 0)
            {
                Debug.LogWarning("[CombatStartCardGiver] 奖励卡池为空或未配置");
                return;
            }
            
            // 验证目标卡组
            if (playerDeck == null)
            {
                Debug.LogError("[CombatStartCardGiver] 玩家卡组未配置");
                return;
            }
            
            // 标记已触发
            _hasGivenCard = true;
            
            for (int i = 0; i < cardsToGive; i++)
            {
                // 检查容量限制
                if (checkDeckSizeLimit && deckSizeLimit != null)
                {
                    int currentSize = GetActualDeckSize();
                    if (currentSize >= deckSizeLimit.value)
                    {
                        Debug.Log("[CombatStartCardGiver] 卡组已满，停止添加");
                        return;
                    }
                }
                
                // 随机选卡
                int randomIndex = Random.Range(0, rewardPoolDeck.deck.Count);
                GameObject cardToAdd = rewardPoolDeck.deck[randomIndex];
                
                // 添加到玩家卡组
                playerDeck.deck.Add(cardToAdd);
                
                if (logAddedCard)
                {
                    Debug.Log($"[CombatStartCardGiver] 添加卡牌: {cardToAdd.name}");
                }
            }
        }
        
        /// <summary>
        /// 计算实际占用卡位的卡牌数量
        /// </summary>
        private int GetActualDeckSize()
        {
            int count = 0;
            foreach (var card in playerDeck.deck)
            {
                var cardScript = card.GetComponent<CardScript>();
                if (cardScript != null && cardScript.takeUpSpace)
                {
                    count++;
                }
            }
            return count;
        }
        
        /// <summary>
        /// 重置触发状态（用于测试或重新开始游戏）
        /// </summary>
        public void ResetTriggerState()
        {
            _hasGivenCard = false;
        }
    }
}
