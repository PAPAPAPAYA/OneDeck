using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides stable per-card position/rotation offsets for deck layout.
/// Keeps the "messy deck" visual decision separate from both the pure position
/// calculator and the physical card representation.
/// </summary>
public class DeckLayoutOffsetProvider
{
	private readonly Dictionary<CardPhysObjScript, Vector3> _positionOffsets = new();
	private readonly Dictionary<CardPhysObjScript, Vector3> _rotationOffsets = new();

	public Vector3 PositionOffsetRange { get; set; }
	public Vector3 RotationOffsetRange { get; set; }

	/// <summary>
	/// Generate and store a new random offset for the given card.
	/// </summary>
	public void AssignOffset(CardPhysObjScript card)
	{
		if (card == null) return;

		_positionOffsets[card] = new Vector3(
			Random.Range(-PositionOffsetRange.x, PositionOffsetRange.x),
			Random.Range(-PositionOffsetRange.y, PositionOffsetRange.y),
			Random.Range(-PositionOffsetRange.z, PositionOffsetRange.z)
		);

		_rotationOffsets[card] = new Vector3(
			Random.Range(-RotationOffsetRange.x, RotationOffsetRange.x),
			Random.Range(-RotationOffsetRange.y, RotationOffsetRange.y),
			Random.Range(-RotationOffsetRange.z, RotationOffsetRange.z)
		);	}

	/// <summary>
	/// Get the cached position offset for a card, generating one if missing.
	/// </summary>
	public Vector3 GetPositionOffset(CardPhysObjScript card)
	{
		if (card == null) return Vector3.zero;
		if (!_positionOffsets.ContainsKey(card))
		{
			AssignOffset(card);
		}
		return _positionOffsets[card];
	}

	/// <summary>
	/// Get the cached rotation offset for a card, generating one if missing.
	/// </summary>
	public Quaternion GetRotationOffset(CardPhysObjScript card)
	{
		if (card == null) return Quaternion.identity;
		if (!_rotationOffsets.ContainsKey(card))
		{
			AssignOffset(card);
		}
		return Quaternion.Euler(_rotationOffsets[card]);
	}

	/// <summary>
	/// Re-randomize offsets for all provided cards.
	/// </summary>
	public void RandomizeAll(IEnumerable<CardPhysObjScript> cards)
	{
		foreach (var card in cards)
		{
			AssignOffset(card);
		}
	}

	/// <summary>
	/// Remove a single card's offset (e.g. when the physical card is destroyed).
	/// </summary>
	public void RemoveOffset(CardPhysObjScript card)
	{
		if (card == null) return;
		_positionOffsets.Remove(card);
		_rotationOffsets.Remove(card);
	}

	/// <summary>
	/// Clear all stored offsets.
	/// </summary>
	public void Clear()
	{
		_positionOffsets.Clear();
		_rotationOffsets.Clear();
	}
}
