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
		SlotInBatch,
		MoveToTopPopUpBatch
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
		// Semi-deprecated for batch moves: MoveToBottomBatch and MoveToTopBatch no longer use
		// this for index calculation (they read actualPhysIndex from physicalCardsInDeck after
		// ApplyAnimationResult). targetIndices is kept because MoveToTopPopUpBatch still needs
		// pre-computed final deck positions up-front to calculate pop-up peaks in Phase 1.
		public List<int> targetIndices;
		public int snapshotDeckSize; // Historical deck size at time of effect capture, for debug logging only. No longer used for index calculation.

		// StatusEffectChange specific fields
		public EnumStorage.StatusEffect statusEffect;
		public int statusEffectAmount;
		public ParticleSystem statusEffectParticlePrefab;
		public float statusEffectParticleYOffset;
	}
}
