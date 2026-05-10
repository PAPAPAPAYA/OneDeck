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
		StatusEffectChange
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

		// StatusEffectChange specific fields
		public EnumStorage.StatusEffect statusEffect;
		public int statusEffectAmount;
		public ParticleSystem statusEffectParticlePrefab;
		public float statusEffectParticleYOffset;
	}
}
