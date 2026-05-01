using UnityEngine;

[RequireComponent(typeof(CardPhysObjScript))]
public class CombatCardView : MonoBehaviour
{
	private CardPhysObjScript _cardPhysObj;

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
	/// </summary>
	private void UpdateMotion()
	{
		if (_cardPhysObj.isPlayingSpecialAnimation)
		{
			_cardPhysObj.KillTweens();
		}
	}
}
