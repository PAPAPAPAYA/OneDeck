using System;
using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class CardManipulationEffect : MonoBehaviour
{
	private CombatManager _cm;
	private GameObject _myCard;
	private CardScript _myCardScript;
	private void OnEnable()
	{
		_cm = CombatManager.Me;
		_myCard = transform.parent.gameObject;
		_myCardScript = _myCard.GetComponent<CardScript>();
	}
	
	public StringSO effectResultString;

	public void StageSelf() // put self on top of the deck
	{
		if (!_cm.combinedDeckZone.Contains(_myCard)) return;
		_cm.combinedDeckZone.Remove(_myCard);
		_cm.combinedDeckZone.Add(_myCard);
		effectResultString.value += "["+_myCardScript.cardName+"] is staged to the top of the deck\n";
	}

	public void StageTag(int amount, EnumStorage.Tag tagToCheck) //todo put random cards with tagToCheck on top of the deck
	{
	}

	public void BurySelf() // put self at the bottom of the deck
	{
		if (!_cm.combinedDeckZone.Contains(transform.parent.gameObject)) return;
		_cm.combinedDeckZone.Remove(transform.parent.gameObject);
		_cm.combinedDeckZone.Insert(0, transform.parent.gameObject);
		effectResultString.value += "["+_myCardScript.cardName+"] is buried to the bottom of the deck\n";
	}
}