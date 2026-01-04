using System;
using System.Collections.Generic;
using UnityEngine;

public class CardManipulationEffect : MonoBehaviour
{
	private CombatManager _cm;

	private void OnEnable()
	{
		_cm = CombatManager.Me;
	}

	public void StageSelf() // put self on top of the deck
	{
		if (!_cm.combinedDeckZone.Contains(transform.parent.gameObject)) return;
		_cm.combinedDeckZone.Remove(transform.parent.gameObject);
		_cm.combinedDeckZone.Add(transform.parent.gameObject);
	}

	public void StageTag(int amount, EnumStorage.Tag tagToCheck) //todo put random cards with tagToCheck on top of the deck
	{
	}

	public void BurySelf() // put self at the bottom of the deck
	{
		if (!_cm.combinedDeckZone.Contains(transform.parent.gameObject)) return;
		_cm.combinedDeckZone.Remove(transform.parent.gameObject);
		_cm.combinedDeckZone.Insert(0, transform.parent.gameObject);
	}
}