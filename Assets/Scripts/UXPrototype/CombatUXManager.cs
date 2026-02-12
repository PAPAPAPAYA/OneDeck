using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CombatUXManager : MonoBehaviour
{
	#region SINGLETON
	public static CombatUXManager me;
	void Awake()
	{
		me = this;
	}

	#endregion

	private List<GameObject> physicalCards = new List<GameObject>();
	public CardPhysObjScript revealedPhysicalCard;
	public GameObject physicalCardPrefab;
	public GameObject startCardPrefab; // currently not used
	public float zOffset;
	
	
	private CombatManager _combatManger;
	
	private void OnEnable()
	{
		_combatManger = CombatManager.Me;
	}
	
	void Update()
	{
		if (Input.GetMouseButtonDown(0))
		{
			// reveal next card
		}
	}
	
	public void ProcessPhysicalCards()
	{
		if (physicalCards.Count <= 0)
		{
			InstantiateAllPhysicalCards();
		}
		else
		{
			// reveal next
		}
	}
	
	private void InstantiateAllPhysicalCards()
	{
		// for each card in combined deck, instantiate a physical card
		foreach (var card in _combatManger.combinedDeckZone)
		{
			CardScript cardScript = card.GetComponent<CardScript>();
			GameObject newPhysicalCard = Instantiate(physicalCardPrefab, transform);
			CardPhysObjScript newPhysicalCardScript = newPhysicalCard.GetComponent<CardPhysObjScript>();
			newPhysicalCardScript.cardImRepresenting = cardScript;
			newPhysicalCardScript.cardNamePrint.text = card.name;
			newPhysicalCardScript.cardDescPrint.text = cardScript.cardDesc;
			physicalCards.Add(newPhysicalCard);
		}
		// process physical cards
		for (int i = 0; i <physicalCards.Count; i++)
		{
			// apply z offset
			physicalCards[i].transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z - zOffset * i);
		}
		//MakeStartCard();
	}
	
	private void MakeStartCard()
	{
		var newStartCard = Instantiate (startCardPrefab, transform);
		newStartCard.transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z + zOffset);
	}
}
