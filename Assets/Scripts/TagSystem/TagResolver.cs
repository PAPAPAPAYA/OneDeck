using UnityEngine;

namespace TagSystem
{
    public abstract class TagResolver : MonoBehaviour
    {
        public abstract void ResolveTag(CardScript card);
    }
}
