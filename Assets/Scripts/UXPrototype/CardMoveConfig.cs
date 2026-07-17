using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Card Move Type
/// </summary>
public enum CardMoveType
{
	ToTop,          // Move to top of deck (last card)
	ToBottom,       // Move to bottom of deck (first card)
	ToIndex,        // Move to specified index
	ToPosition,     // Move to specified world position
	ToGrave,        // Move to graveyard (destroy position)
}

/// <summary>
/// Card Move Config
/// </summary>
[Serializable]
public class CardMoveConfig
{
	public CardMoveType moveType = CardMoveType.ToBottom;
	public int targetIndex;                    // Used when ToIndex
	public Vector3? customTarget;              // Used when ToPosition
	public bool useArc = true;                 // Whether to use arc trajectory
	public Transform arcMidpoint;              // Arc midpoint (use showPos if null)
	public float duration = 0.5f;              // Animation duration
	public Ease ease = Ease.InOutQuad;         // Ease type
	public bool destroyAfterMove = false;      // Whether to destroy after move
	public Action onComplete;                  // Animation complete callback
	public Action onStart;                     // Animation start callback
	public Vector3? targetScaleOverride;       // Optional landing scale override (e.g. cascade scale for ToPosition moves); null = default by moveType
	
	// Convenient constructors
	public static CardMoveConfig ToTop(float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		return new CardMoveConfig 
		{ 
			moveType = CardMoveType.ToTop, 
			duration = duration, 
			useArc = useArc,
			onComplete = onComplete 
		};
	}
	
	public static CardMoveConfig ToBottom(float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		return new CardMoveConfig 
		{ 
			moveType = CardMoveType.ToBottom, 
			duration = duration, 
			useArc = useArc,
			onComplete = onComplete 
		};
	}
	
	public static CardMoveConfig ToIndex(int index, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		return new CardMoveConfig 
		{ 
			moveType = CardMoveType.ToIndex, 
			targetIndex = index,
			duration = duration, 
			useArc = useArc,
			onComplete = onComplete 
		};
	}
	
	public static CardMoveConfig ToPosition(Vector3 position, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		return new CardMoveConfig 
		{ 
			moveType = CardMoveType.ToPosition, 
			customTarget = position,
			duration = duration, 
			useArc = useArc,
			onComplete = onComplete 
		};
	}
	
	public static CardMoveConfig ToGrave(float duration = 0.3f, Action onComplete = null)
	{
		return new CardMoveConfig 
		{ 
			moveType = CardMoveType.ToGrave, 
			duration = duration, 
			useArc = false,
			destroyAfterMove = true,
			onComplete = onComplete 
		};
	}
}
