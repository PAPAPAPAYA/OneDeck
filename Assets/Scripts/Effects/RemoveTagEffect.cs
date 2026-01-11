using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class RemoveTagEffect : EffectScript
	{
		public EnumStorage.Tag tagToRemove;

		public void RemoveTag()
		{
			myCardScript.myTags.Remove(tagToRemove);
			effectResultString.value += "// [" + tagToRemove + "] is removed\n";
		}
	}
}