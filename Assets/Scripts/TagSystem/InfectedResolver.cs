using UnityEngine;

namespace TagSystem
{
    [RequireComponent(typeof(HPAlterEffect))]
    public class InfectedResolver : TagResolver
    {
        private HPAlterEffect _myHpAlterScript;
        [Tooltip("be minus to decrease hp")]
        public int dmgAmount = -1;

        private void Start()
        {
            _myHpAlterScript = GetComponent<HPAlterEffect>();
        }

        public override void ResolveTag(CardScript card)
        {
            // apply dmg to card owner
            _myHpAlterScript.AlterHP(dmgAmount, card.myStatusRef);
            // remove tag
            card.myTags.Remove(EnumStorage.Tag.Infected);
        }
    }
}
