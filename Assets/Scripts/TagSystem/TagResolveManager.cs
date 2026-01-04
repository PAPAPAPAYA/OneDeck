using System;
using UnityEngine;

namespace TagSystem
{
	public class TagResolveManager : MonoBehaviour
	{
		#region SINGLETON
		public static TagResolveManager Me;

		private void Awake()
		{
			Me = this;
		}
		#endregion
		public InfectedResolver infectedResolver;

		public void ProcessTags(CardScript card)
		{
			if (card.myTags.Contains(EnumStorage.Tag.Infected))
			{
				infectedResolver.ResolveTag();
				card.myTags.Remove(EnumStorage.Tag.Infected);
			}
		}
	}
}