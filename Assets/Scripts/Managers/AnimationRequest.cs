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
		MoveToTopPopUpBatch,
		Shuffle,
		Shake
	}

	public class AnimationRequest
	{
		public AnimationRequestType type;
		public GameObject attackerCard;
		public List<GameObject> attackerCards;
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
		/// <summary>
		/// For StatusEffectChange requests: the signed delta applied to the target's display state.
		/// Positive = gain layers, negative = lose layers. Used by RecorderAnimationPlayer to
		/// apply status effect text updates incrementally per projectile instead of committing
		/// the full card state.
		/// </summary>
		public int statusEffectDelta = 0;
		public ParticleSystem statusEffectParticlePrefab;
		public float statusEffectParticleYOffset;
		public GameObject sourceCard; // Used for Shuffle request (Start Card instance)

		/// <summary>
		/// When true, StatusEffectChange will not commit the display state immediately.
		/// Instead, the display state will be committed after the corresponding
		/// StatusEffectProjectile animation completes (for targets that have a projectile).
		/// </summary>
		public bool deferDisplayCommit = false;

		/// <summary>
		/// Internal flag used by RecorderAnimationPlayer to ensure a StatusEffectChange's
		/// display delta is applied exactly once, whether immediately or when its linked
		/// projectile lands.
		/// </summary>
		public bool displayDeltaApplied = false;

		/// <summary>
		/// Optional custom end position for StatusEffectProjectile.
		/// When set, the projectile flies to this world position instead of to targetCard/targetCards.
		/// Used for effects like ConsumeOwnStatusEffect where the projectile should fly
		/// to newCardPos rather than another card.
		/// </summary>
		public Vector3? customProjectileEndPosition;

		// VISUAL-FIX(2026-06-12): Status effect projectiles do not reflect stack count
		//   Cause:    AnimationRequest only carried target information; the visual layer had
		//             no way to know how many status effect layers were being given/consumed,
		//             so every effect played exactly one projectile regardless of stack count.
		//   Affects:  AnimationRequest, ICombatVisuals, CombatUXManager, RecorderAnimationPlayer,
		//             StatusEffectGiverEffect, CurseEffect, ConsumeStatusEffect
		//   Regress:  Reveal a card that gives/consumes multiple status effect layers
		//             Check: one projectile should spawn per layer, with randomized start
		//             positions and staggered launch times, and the effect should wait until
		//             the last projectile finishes before continuing.
		/// <summary>
		/// Number of projectiles to spawn per target for StatusEffectProjectile.
		/// Defaults to 1. Set to the number of status effect layers being applied/consumed
		/// so the visual matches the logic.
		/// </summary>
		public int projectileCount = 1;

		/// <summary>
		/// Random XY offset range applied to each projectile's start position.
		/// X controls horizontal spread, Y controls vertical spread. Z is always 0.
		/// </summary>
		public Vector2 projectileStartRandomOffsetRange;

		/// <summary>
		/// Random delay range (seconds) for staggering projectile launches.
		/// x = minimum delay, y = maximum delay. Each projectile picks a random value
		/// in this range for its launch time.
		/// </summary>
		public Vector2 projectileStartTimeStaggerRange;

		/// <summary>
		/// When true, the status effect projectile flies from each target back to the giver card.
		/// Used for absorption-style effects (e.g. consuming an enemy's status effect).
		/// </summary>
		public bool reverseProjectile;

		/// <summary>
		/// Per-target projectile counts for StatusEffectProjectile when targetCards is populated.
		/// When null or empty, projectileCount is used uniformly for all targets.
		/// Used to accurately reflect non-uniform status effect consumption (e.g. card A loses 2
		/// layers while card B loses 1).
		/// </summary>
		public List<int> projectileCountsPerTarget;
	}
}
