using UnityEngine;

namespace DefaultNamespace.Managers
{
    public class CardIDRetriever : MonoBehaviour
    {
        #region SINGLETON

        public static CardIDRetriever Me;

        private void Awake()
        {
            Me = this;
        }

        #endregion

        public int cardIDTracker;

        public int RetrieveCardID()
        {
            cardIDTracker++;
            return cardIDTracker;
        }
    }
}