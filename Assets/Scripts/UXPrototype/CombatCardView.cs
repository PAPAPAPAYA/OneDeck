using UnityEngine;

[RequireComponent(typeof(CardPhysObjScript))]
public class CombatCardView : MonoBehaviour
{
	private CardPhysObjScript _cardPhysObj;
	// VISUAL-FIX(2026-09-11): tracks the isPlayingSpecialAnimation falling edge so the card can
	// re-converge to its TargetPosition after the special animation's direct transform drive.
	private bool _wasSpecialAnimating;

	void OnEnable()
	{
		_cardPhysObj = GetComponent<CardPhysObjScript>();
	}

	void Update()
	{
		UpdateMotion();
	}

	/// <summary>
	/// Handle animation-related logic in Update.
	/// Stops CardPhysObjScript's DOTween tweens when a special animation is playing,
	/// so that CombatUXManager / AttackAnimationManager can drive the transform directly.
	/// VISUAL-FIX(2026-09-11): on the special-animation falling edge, re-converge the card to
	/// its TargetPosition.
	///   Cause:    KillTweens() (every frame during a special animation) killed the position
	///             tween mid-flight and nothing re-drove the transform afterwards. A reveal-zone
	///             card interrupted mid re-clamp flight froze at a z BEHIND the deck front card
	///             permanently (RIFT_PRIEST batch-token repro: frozen at -7.233 vs front -7.5,
	///             target -8.0, no tween playing) - the fixed card-over-card cover. Plan A's
	///             immediate re-clamp restarts made mid-flight interruptions far more likely,
	///             which surfaced this pre-existing gap.
	///   Affects:  CombatCardView.UpdateMotion falling edge, CardPhysObjScript.ReconvergeToTargetPosition.
	///   Regress:  RIFT_PRIEST batch generation: after the source-card pop-up/slot-in finishes,
	///             the revealed card must glide to its reveal pose (gap 0.5 in front of the deck
	///             front card) and stay there; hover pop-up/slot-in, attack flight and return,
	///             and shuffle staggers all end with cards resting on their targets.
	/// VISUAL-FIX(2026-09-12): kill only what the special animation actually owns.
	///   Cause:    KillTweens() also killed the POSITION tween, including for a scale-only special
	///             animation (the emphasize pulse). The reveal-zone card was therefore pinned at its
	///             pre-shift z for the whole pulse while deck cards glided forward 0.5 per generated
	///             card (RIFT_PRIEST batch): the deck front card crossed in front of the reveal card
	///             and covered it. Editor-prev.log evidence: every hard inversion recorded AFTER the
	///             V2 falling-edge fix has special=True + tween=False, gap up to -0.378.
	///   Fix:      Ask the card whether the active special animation pins its position
	///             (CardPhysObjScript.SpecialAnimationPinsPosition). If it does not (scale-only pulse
	///             on a card that is not parked at a pop-up peak), spare the position tween so the
	///             card keeps tracking its re-clamped reveal z; scale/rotation tweens are always
	///             killed so they cannot fight the pulse's own DOScale.
	///   Affects:  CombatCardView.UpdateMotion; CardPhysObjScript (SpecialAnimationPinsPosition,
	///             KillScaleAndRotationTweens, BeginSpecialAnimation).
	///   Regress:  RIFT_PRIEST / RIFT_HATCHERY batch generation: the revealed card must stay in
	///             front of the deck front card through the whole emphasize pulse, not only after
	///             it. Pop-up peaks, Stage/Bury arcs, attack flights, peel exits and shuffle must
	///             still pin the position (no double-drive jitter).
	/// </summary>
	private void UpdateMotion()
	{
		if (_cardPhysObj.isPlayingSpecialAnimation)
		{
			_wasSpecialAnimating = true;
			if (_cardPhysObj.SpecialAnimationPinsPosition)
				_cardPhysObj.KillTweens();
			else
				_cardPhysObj.KillScaleAndRotationTweens();
		}
		else if (_wasSpecialAnimating)
		{
			_wasSpecialAnimating = false;
			_cardPhysObj.ReconvergeToTargetPosition();
		}
	}
}
