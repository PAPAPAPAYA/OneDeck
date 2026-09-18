using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Port of the UIKitDemo §02 ".phys" interaction state machine (docs/UIUX_Guidelines.md §2),
/// world-space only since the 2026-09-18 chrome convergence: rest -> hover (face lifts
/// toward the top-left light, hard shadow stays anchored on the ground) -> press (face
/// lands on the shadow) -> activate on release inside; holding and dragging out cancels
/// (R6); release fires (R7); a disabled face is dim, shadowless, and plays the
/// once-per-pointer-enter refusal animation (§2.2).
///
/// One invariant — the hit target never moves, only the visuals do (a moving collider
/// under a stationary cursor flickers enter/exit): a static root (BoxCollider2D sized to
/// the face/shadow/travel envelope, OnMouse input) plus a moving Visual group
/// (face + label) and a grounded shadow sibling. Release is read via a deterministic
/// Input.GetMouseButtonUp poll because OnMouseUp alone cannot distinguish an inside
/// release from a drag-out release (R6/R7).
///
/// Offsets are in face-local units; the caller converts design px per context (card-local
/// ≈ 3.2 units / 118 demo px, chrome uses RestShadowUnits-style constants).
///
/// All presses and activations consult ShopInputGate — the single shop-phase input gate.
/// </summary>
public class PhysButton : MonoBehaviour
{
	[Header("Physical Params (Guidelines 2.1; face-local units)")]
	[Tooltip("Rest shadow offset rs (demo default 4 design px)")]
	public float restShadow = 0.1085f;
	[Tooltip("Hover lift toward the light hl")]
	public float hoverLift = 0.1085f;
	public float hoverDuration = 0.12f;
	[Tooltip("Press must stay shorter than hoverDuration (R5)")]
	public float pressDuration = 0.06f;

	[Header("Disabled Refusal (Guidelines 2.2; face-local units / seconds)")]
	public float denyShift = 0.1628f;
	public float denyOutDuration = 0.08f;
	public int denyWiggles = 1;
	public float denySwingDuration = 0.11f;
	[Range(0f, 1f)]
	[Tooltip("Swing amplitude = denyShift * this, decaying 0.55 per wiggle")]
	public float denyAmplitudeFactor = 0.4f;
	public float denyHoldDuration = 0.15f;
	public float denyRetreatDuration = 0.12f;

	[Header("Hover Text Swap (Guidelines 3.1; optional, empty = off)")]
	public TMP_Text label;
	[Tooltip("Shown while hovered; the rest text is owned by the caller and rewritten on unhover")]
	public string hoverText = "";

	private const float WiggleDecay = 0.55f;

	private SpriteRenderer _faceRenderer;
	private Transform _shadow;
	private Transform _visualGroup;
	private Color _faceColor = Color.white;
	private Vector2 _faceOffset;
	private Tween _moveTween;
	private Coroutine _denyRoutine;
	private bool _denyPlayedThisEnter;
	private bool _pressed;
	private bool _externalDisabled;
	private bool _appliedDisabled;
	private Action _worldAction;

	/// <summary>True while the pointer is over the face. Callers holding the rest label text (live price) skip their rewrite while this is set.</summary>
	public bool IsPointerOver { get; private set; }

	public bool IsDisabled => _externalDisabled;

	private void Awake()
	{
		// Self-renderer only (callers normally put the face on a child and assign it via
		// SetWorldFace); the shadow child must never be picked up here.
		_faceRenderer = GetComponent<SpriteRenderer>();
	}

	private void Update()
	{
		if (_pressed && Input.GetMouseButtonUp(0))
		{
			// Deterministic release poll: OnMouseUp alone cannot distinguish an inside
			// release from a drag-out release, and R6/R7 hinge on that difference.
			FinishPress(IsPointerOver);
		}
	}

	private void LateUpdate()
	{
		// R2: disabled = shadowless, except while the refusal animation mirrors the face
		// offset (Guidelines 2.2).
		if (_shadow == null) return;
		bool visible = isActiveAndEnabled && (!IsDisabled || _denyRoutine != null);
		if (_shadow.gameObject.activeSelf != visible) _shadow.gameObject.SetActive(visible);
	}

	private void OnDisable()
	{
		StopDeny();
		if (_moveTween != null && _moveTween.IsActive()) _moveTween.Kill();
		_pressed = false;
		IsPointerOver = false;
		_denyPlayedThisEnter = false;
		if (_visualGroup != null) _visualGroup.localPosition = Vector3.zero;
	}

	#region Configuration

	/// <summary>
	/// The ground-anchored shadow transform (a child of this object). The shadow never
	/// moves — during press the face lands on top of it, which reads as the demo's
	/// shadow collapse without animating it.
	/// </summary>
	public void SetShadowTransform(Transform shadow)
	{
		_shadow = shadow;
	}

	/// <summary>
	/// The group carrying face + label. This is what hover/press/deny move; the root stays
	/// put so its collider is a stable hit target — a moving collider under a stationary
	/// cursor would flicker enter/exit at the edges.
	/// </summary>
	public void SetVisualGroup(Transform visualGroup)
	{
		_visualGroup = visualGroup;
	}

	/// <summary>
	/// The face sprite as a CHILD of this object — never the renderer on the root, because
	/// ConfigureWorldFaceSize may reposition the face and the root itself must not move
	/// (the label lives at the root origin).
	/// </summary>
	public void SetWorldFace(SpriteRenderer face)
	{
		_faceRenderer = face;
		if (face != null) _faceColor = face.color;
	}

	/// <summary>Caller-driven disabled state.</summary>
	public void SetDisabled(bool disabled)
	{
		_externalDisabled = disabled;
		if (disabled != _appliedDisabled)
		{
			_appliedDisabled = disabled;
			ApplyDisabledVisual(disabled);
		}
	}

	/// <summary>What a release inside invokes. Swappable at runtime (shop price button flips buy/sell as the card changes side).</summary>
	public void SetWorldAction(Action action)
	{
		_worldAction = action;
	}

	/// <summary>
	/// Face/shadow sliced size plus the root collider's envelope — the union of the face at
	/// rest, hover and press, the grounded shadow, and the deny excursion — so the pointer
	/// never leaves the hit area because the face moved away (edge flicker).
	/// </summary>
	public void ConfigureWorldFaceSize(Vector2 size, Vector2 center)
	{
		if (_faceRenderer != null)
		{
			_faceRenderer.size = size;
			Vector3 p = _faceRenderer.transform.localPosition;
			_faceRenderer.transform.localPosition = new Vector3(center.x, center.y, p.z);
		}
		if (_shadow != null)
		{
			SpriteRenderer shadowSr = _shadow.GetComponent<SpriteRenderer>();
			if (shadowSr != null) shadowSr.size = size;
			Vector3 sp = _shadow.localPosition;
			_shadow.localPosition = new Vector3(center.x + restShadow, center.y - restShadow, sp.z);
		}
		BoxCollider2D col = GetComponent<BoxCollider2D>();
		if (col != null)
		{
			col.size = size + new Vector2(restShadow + denyShift, restShadow + denyShift);
			col.offset = center + new Vector2((restShadow - denyShift) / 2f, (denyShift - restShadow) / 2f);
		}
	}

	#endregion

	#region Input

	private void OnMouseEnter()
	{
		PointerEnterCommon();
	}

	private void OnMouseExit()
	{
		PointerExitCommon();
	}

	private void OnMouseDown()
	{
		if (IsDisabled || ShopInputGate.Blocked) return;
		_pressed = true;
		EnterPressVisual();
	}

	private void PointerEnterCommon()
	{
		IsPointerOver = true;
		_denyPlayedThisEnter = false;
		if (IsDisabled)
		{
			PlayDenyOncePerEnter();
			return;
		}
		// R6: re-entering while still held re-presses.
		if (_pressed)
		{
			EnterPressVisual();
			return;
		}
		EnterHoverVisual();
	}

	private void PointerExitCommon()
	{
		IsPointerOver = false;
		StopDeny();
		// Held-out: the release poll cancels (R6); keep the pressed pose meanwhile.
		if (_pressed) return;
		EnterRestVisual();
	}

	private void FinishPress(bool inside)
	{
		_pressed = false;
		if (inside && !IsDisabled && !ShopInputGate.Blocked && _worldAction != null)
		{
			_worldAction();
		}
		RestoreHoverOrRest();
	}

	private void RestoreHoverOrRest()
	{
		if (IsPointerOver && !IsDisabled) EnterHoverVisual();
		else EnterRestVisual();
	}

	#endregion

	#region Visual State Machine

	private Vector2 HoverVector
	{
		get { return new Vector2(-hoverLift, hoverLift); } // toward the top-left light
	}

	private Vector2 PressVector
	{
		get { return new Vector2(restShadow, -restShadow); } // lands on the shadow (R5)
	}

	private void EnterHoverVisual()
	{
		TweenOffsetTo(HoverVector, hoverDuration, Ease.OutBack);
		UpdateLabelSwap();
	}

	private void EnterRestVisual()
	{
		TweenOffsetTo(Vector2.zero, hoverDuration, Ease.OutQuad);
		UpdateLabelSwap();
	}

	private void EnterPressVisual()
	{
		TweenOffsetTo(PressVector, pressDuration, Ease.OutQuad);
	}

	private void ApplyDisabledVisual(bool disabled)
	{
		if (_faceRenderer != null)
		{
			_faceRenderer.color = disabled ? GameColorPalette.CardFaceDimColor : _faceColor;
		}
		if (disabled)
		{
			if (IsPointerOver) PlayDenyOncePerEnter();
		}
		else
		{
			StopDeny();
			RestoreHoverOrRest();
		}
	}

	private void PlayDenyOncePerEnter()
	{
		if (_denyPlayedThisEnter) return;
		_denyPlayedThisEnter = true;
		_denyRoutine = StartCoroutine(DenyRoutine());
	}

	/// <summary>
	/// Guidelines 2.2 refusal: slide out toward the light -> wiggle pairs with decaying
	/// amplitude around the out point -> recenter -> hold -> retreat. The grounded shadow
	/// never moves; the face slides over it.
	/// </summary>
	private IEnumerator DenyRoutine()
	{
		Vector2 outVec = new Vector2(-denyShift, denyShift);
		yield return TweenOffsetTo(outVec, denyOutDuration, Ease.OutQuad).WaitForCompletion();
		for (int k = 1; k <= denyWiggles; k++)
		{
			float amp = denyShift * denyAmplitudeFactor * Mathf.Pow(WiggleDecay, k - 1);
			yield return TweenOffsetTo(new Vector2(outVec.x + amp, outVec.y), denySwingDuration, Ease.Linear).WaitForCompletion();
			yield return TweenOffsetTo(new Vector2(outVec.x - amp, outVec.y), denySwingDuration, Ease.Linear).WaitForCompletion();
		}
		yield return TweenOffsetTo(outVec, denySwingDuration, Ease.Linear).WaitForCompletion();
		if (denyHoldDuration > 0f) yield return new WaitForSeconds(denyHoldDuration);
		yield return TweenOffsetTo(Vector2.zero, denyRetreatDuration, Ease.OutQuad).WaitForCompletion();
		_denyRoutine = null;
	}

	private void StopDeny()
	{
		if (_denyRoutine == null) return;
		StopCoroutine(_denyRoutine);
		_denyRoutine = null;
	}

	#endregion

	#region Offset Apply

	/// <summary>Moves only the Visual group; the root collider and grounded shadow stay put.</summary>
	private void ApplyOffset(Vector2 offset)
	{
		_faceOffset = offset;
		if (_visualGroup != null)
		{
			_visualGroup.localPosition = offset; // group rest base is the local origin
		}
	}

	private Tween TweenOffsetTo(Vector2 target, float duration, Ease ease)
	{
		if (_moveTween != null && _moveTween.IsActive()) _moveTween.Kill();
		_moveTween = DOTween.To(() => _faceOffset, ApplyOffset, target, duration).SetEase(ease);
		return _moveTween;
	}

	private void UpdateLabelSwap()
	{
		if (label == null || string.IsNullOrEmpty(hoverText)) return;
		if (IsPointerOver && !IsDisabled) label.text = hoverText;
		// The rest text is owned by the caller; it rewrites on the next unhover tick.
	}

	#endregion
}
