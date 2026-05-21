using System;
using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
	public enum AnimationRequestType
	{
		Attack,
		MoveToBottom,
		MoveToBottomBatch,
		MoveToTop,
		MoveToTopBatch,
		MoveToIndex,
		Destroy,
		StatusEffectChange,
		StatusEffectProjectile,
		PopUp,
		SlotIn,
		MoveToPopUpPosition,
		PopUpBatch,
		SlotInBatch
	}

	public class AnimationRequest
	{
		public AnimationRequestType type;
		public GameObject attackerCard;
		public bool isAttackingEnemy;
		public GameObject targetCard;
		public List<GameObject> targetCards;
		public Action onHit;
		public Action onComplete;
		public float duration = 0.5f;
		public bool useArc = true;
		public int targetIndex;
		public List<int> targetIndices;
		public int snapshotDeckSize; // Deck size at the time of snapshot, used to correct index when deck size changes before animation playback

		// StatusEffectChange specific fields
		public EnumStorage.StatusEffect statusEffect;
		public int statusEffectAmount;
		public ParticleSystem statusEffectParticlePrefab;
		public float statusEffectParticleYOffset;
	}
}
