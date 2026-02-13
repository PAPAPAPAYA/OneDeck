using UnityEngine;

namespace DefaultNamespace.Managers
{
    /// <summary>
    /// 管理玩家每局游戏的起始卡牌
    /// 在第一次进入商店时，从起始卡牌池中随机选择一张添加到玩家卡组
    /// </summary>
    public class StartingCardManager : MonoBehaviour
    {
        [Header("初始卡牌池配置")]
        [Tooltip("包含所有可能作为起始卡牌的DeckSO，从中随机选择一张")]
        public DeckSO startingCardPool;
        
        [Header("玩家卡组")]
        [Tooltip("玩家的DeckSO，起始卡牌将被添加到这里")]
        public DeckSO playerDeck;
        
        [Header("游戏状态")]
        [Tooltip("当前游戏局数引用，用于判断是否是第一局")]
        public IntSO sessionNum;
        
        // 标记本局游戏是否已经发放过起始卡牌
        private bool _hasGivenStartingCardThisRun;

        private void OnEnable()
        {
            // 游戏开始时重置标记
            _hasGivenStartingCardThisRun = false;
        }

        /// <summary>
        /// 尝试给玩家发放起始卡牌
        /// 仅在每局游戏第一次进入商店时执行
        /// 通过PhaseManager的onEnterShopPhase事件调用
        /// </summary>
        public void TryGiveStartingCard()
        {
            // 只在本局游戏第一次且是第一局(sessionNum == 0)时执行
            if (_hasGivenStartingCardThisRun)
            {
                return;
            }

            if (sessionNum.value != 0)
            {
                return;
            }

            if (startingCardPool == null)
            {
                Debug.LogWarning("[StartingCardManager] 起始卡牌池未配置！");
                return;
            }

            if (playerDeck == null)
            {
                Debug.LogWarning("[StartingCardManager] 玩家卡组未配置！");
                return;
            }

            // 从卡牌池中随机选择一张卡牌
            GameObject selectedCard = GetRandomCardFromPool();
            if (selectedCard == null)
            {
                Debug.LogWarning("[StartingCardManager] 起始卡牌池为空！");
                return;
            }

            // 添加到玩家卡组
            playerDeck.deck.Add(selectedCard);
            _hasGivenStartingCardThisRun = true;
            
            //Debug.Log($"[StartingCardManager] 已添加起始卡牌: {selectedCard.name}");
        }

        /// <summary>
        /// 从起始卡牌池中随机选择一张卡牌
        /// </summary>
        private GameObject GetRandomCardFromPool()
        {
            if (startingCardPool.deck == null || startingCardPool.deck.Count == 0)
            {
                return null;
            }

            int randomIndex = Random.Range(0, startingCardPool.deck.Count);
            return startingCardPool.deck[randomIndex];
        }
        
        /// <summary>
        /// 重置标记，用于新游戏
        /// </summary>
        public void ResetForNewRun()
        {
            _hasGivenStartingCardThisRun = false;
        }
    }
}
