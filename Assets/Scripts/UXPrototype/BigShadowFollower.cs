using DG.Tweening;
using UnityEngine;

/// <summary>
/// Float Stack big-shadow follower (plan-float-stack-shadow-follow-2026-08-16 §2).
/// Attached to the driven PhysicalCardBigShadow GameObject by
/// CardPhysObjScript.DriveBigShadowToPose; disabled again by RestoreBigShadowFromDrive.
/// While enabled, LateUpdate places the shadow at the revealed card's live pose plus the
/// constant rest offset every frame, so flip / attack / emphasize / pop-up / return-flight
/// motions all keep their shadow sync (the shipped static pin lost it).
/// </summary>
// VISUAL-FIX(2026-08-16): Float Stack driven big shadow lost sync with the revealed card.
//   Cause:    TryDriveRevealBigShadow re-parents the shadow to the deck anchor and the old
//             DriveBigShadowToPose tweened it once to a static slot-0 home pose; the shadow
//             never moved again while driven, so the reveal flip squash (_flipRoot scaleX),
//             the attack wind-up/charge/overshoot/return, the emphasize pulse and hover
//             pop-ups all played shadow-less.
//   Affects:  BigShadowFollower, CardPhysObjScript.DriveBigShadowToPose /
//             RestoreBigShadowFromDrive, CombatUXManager.TryDriveRevealBigShadow,
//             AttackAnimationManager + RecorderAnimationPlayer (lift hooks)
//   Regress:  Float Stack mode: reveal a card — the shadow squashes in sync with the flip;
//             attack (e.g. BLACKSMITH) — the shadow tracks wind-up / charge / overshoot /
//             return, scales 1.4x/0.85x and rotates with the card, grows the anti-light lift
//             offset (wind-up ramp-in, charge hold, return ease-out) and lands exactly back
//             at the slot-0 home; the emphasize pulse grows + settles the same lift offset
//             with its 1.2x scale pulse; hover pop-up keeps the shadow glued; exile/destroy
//             while revealed leaves no orphan shadow under the anchor.
//             Cascade/Linear/ArcLoop unchanged (shadow stays card-local under _flipRoot).
//   Related:  plans/plan-float-stack-shadow-follow-2026-08-16.md;
//             demo docs/demo/CardStackRevealDemo.html
public class BigShadowFollower : MonoBehaviour
{
	private Transform _source;          // revealed card root
	private Transform _squashSource;    // card _flipRoot (nullable: the Start Card has none)
	private Transform _anchor;          // parent while driven (physicalCardDeckPos); scale 1 / identity rotation assumed, same as the old static drive
	private Vector3 _offsetWorld;       // rest offset (shadowHomeWorld - revealHomeWorld), captured at drive start
	private Vector3 _restScale;         // reveal-card rest world scale at drive start (GetRevealZoneScale)
	private Vector3 _bakedLocalScale;   // shadow's baked home local scale (captured under _flipRoot)
	private float _pinnedLocalZ;        // anchor-local z (front-gap formula)
	private bool _followRotation;
	private Vector3 _liftOffsetWorld;   // full-lift anti-light offset (world)
	private float _liftK;               // 0..1 lift weight, driven via SetLift (attack wind-up/return, emphasize pulse)
	private float _blend = 1f;          // drive glide-in weight 0 -> 1
	private Vector3 _blendStartLocalPos;
	private Vector3 _blendStartLocalScale;
	private Quaternion _blendStartLocalRot;
	private Tween _blendTween;
	private Tween _liftTween;

	/// <summary>
	/// (Re)starts the follow drive: snapshots the shadow's current local pose and glides it
	/// into the follow pose over blendDuration (matches the old one-shot tween-in).
	/// </summary>
	public void Init(Transform source, Transform squashSource, Transform anchor,
		Vector3 offsetWorld, Vector3 restScale, Vector3 bakedLocalScale, float pinnedLocalZ,
		bool followRotation, Vector3 liftOffsetWorld, float blendDuration, Ease blendEase)
	{
		_source = source;
		_squashSource = squashSource;
		_anchor = anchor;
		_offsetWorld = offsetWorld;
		_restScale = restScale;
		_bakedLocalScale = bakedLocalScale;
		_pinnedLocalZ = pinnedLocalZ;
		_followRotation = followRotation;
		_liftOffsetWorld = liftOffsetWorld;
		_liftK = 0f;
		_blendStartLocalPos = transform.localPosition;
		_blendStartLocalScale = transform.localScale;
		_blendStartLocalRot = transform.localRotation;
		_blend = 0f;
		_blendTween?.Kill();
		// Tween target = this component: card tweens (CombatCardView kills card tweens every
		// frame during special animations) and RestoreBigShadowFromDrive's transform DOKill
		// must never stop the blend early.
		_blendTween = DOTween.To(() => _blend, v => _blend = v, 1f, blendDuration)
			.SetEase(blendEase).SetUpdate(UpdateType.Normal, true).SetTarget(this);
		enabled = true;
	}

	/// <summary>
	/// Lift hook (AttackAnimationManager wind-up/return, RecorderAnimationPlayer emphasize
	/// pulse): ramps the anti-light lift weight to target over duration. The offset is
	/// additionally scaled by the live-scale ratio, so the emphasize 1.2x pulse reads
	/// proportionally smaller than the attack 1.4x lift.
	/// </summary>
	public void SetLift(float target, float duration)
	{
		_liftTween?.Kill();
		_liftTween = DOTween.To(() => _liftK, v => _liftK = v, target, duration)
			.SetUpdate(UpdateType.Normal, true).SetTarget(this);
	}

	void LateUpdate()
	{
		if (_source == null)
		{
			// Card destroyed/exiled mid-drive: without this, the re-parented shadow orphans
			// invisibly under the anchor (pre-existing leak — the restore callback early-returns
			// on a destroyed card and never re-parents or destroys the shadow).
			Destroy(gameObject);
			return;
		}
		float squashX = _squashSource != null ? _squashSource.localScale.x : 1f;
		// scaleK: the whole offset is proportional to the card's live size (2026-08-16
		// decision) — the charge shrink pulls the shadow in, the wind-up grow pushes it out;
		// k == 1 at rest, so the rest pose reproduces the old static home exactly.
		float k = _restScale.x != 0f ? _source.lossyScale.x / _restScale.x : 1f;
		float liftX = _liftOffsetWorld.x * _liftK * k; // world-space: never squashed
		float liftY = _liftOffsetWorld.y * _liftK * k;
		Vector3 pos = _source.position;
		// Flip pivot correction: the squash collapses the offset around the card center.
		pos.x += _offsetWorld.x * k * squashX + liftX;
		pos.y += _offsetWorld.y * k + liftY;
		Vector3 local = _anchor.InverseTransformPoint(pos);
		local.z = _pinnedLocalZ; // reveal-z re-clamp / arc-return z drift never leak into the shadow
		Vector3 scale = Vector3.Scale(_source.lossyScale, _bakedLocalScale);
		scale.x *= squashX;
		Quaternion rot = _followRotation ? Quaternion.Euler(0f, 0f, _source.eulerAngles.z) : Quaternion.identity;
		transform.localPosition = Vector3.Lerp(_blendStartLocalPos, local, _blend);
		transform.localScale = Vector3.Lerp(_blendStartLocalScale, scale, _blend);
		transform.localRotation = Quaternion.Slerp(_blendStartLocalRot, rot, _blend);
	}

	void OnDestroy()
	{
		_blendTween?.Kill();
		_liftTween?.Kill();
	}
}
