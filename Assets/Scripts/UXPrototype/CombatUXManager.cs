using System.Collections;
using System.Collections.Generic;
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

	public List<GameObject> physicalCardsInDeck = new();
	public GameObject physicalCardPrefab;
	
	// Dictionary mapping CardScript to physical card (built from all physical cards in deck and grave)
	private Dictionary<CardScript, GameObject> _cardScriptToPhysicalCache = new();

	[Header("DECK")]
	public GameObject startCardPrefab;
	public float zOffset;
	public Transform physicalCardDeckPos;
	public Vector3 physicalCardDeckSize;
	public float lerpTimeMove;

	[Header("GRAVE")]
	public Vector3 physicalCardGraveSize;
	public Transform physicalCardGravePos;
	public float lerpTimeSize;
	public List<GameObject> physicalCardsInGrave = new();



	private CombatManager _combatManger;

	private void OnEnable()
	{
		_combatManger = CombatManager.Me;
	}

	public void SendLastPhysicalCardToGrave()
	{
		float baseZ = physicalCardsInGrave.Count > 0 ? physicalCardsInGrave[0].transform.position.z : physicalCardsInDeck[^1].transform.position.z;
		physicalCardsInGrave.Add(physicalCardsInDeck[^1]);
		physicalCardGravePos.position = new
		(
			physicalCardGravePos.position.x,
			physicalCardGravePos.position.y,
			baseZ - physicalCardsInGrave.Count * zOffset
		);
		physicalCardsInDeck[^1].transform.position = new
		(
			physicalCardsInDeck[^1].transform.position.x,
			physicalCardsInDeck[^1].transform.position.y,
			physicalCardGravePos.position.z
		);
		MovePhysicalCard(physicalCardsInDeck[^1], physicalCardGravePos.position);
		ScalePhysicalCard(physicalCardsInDeck[^1], physicalCardGraveSize);
		//StartALerpCardPos(physicalCardsInDeck[^1], physicalCardGravePos.position);
		//StartALerpCardSize(physicalCardsInDeck[^1], physicalCardGraveSize);
		// StartCoroutine(LerpCardPos(physicalCardsInDeck[^1], physicalCardGravePos.position, lerpTimeMove));
		// StartCoroutine(LerpCardSize(physicalCardsInDeck[^1], physicalCardGraveSize, lerpTimeSize));
		physicalCardsInDeck.RemoveAt(physicalCardsInDeck.Count - 1);
	}

	// put all physical cards from grave to deck
	public void ReviveAllPhysicalCards()
	{
		if (physicalCardsInGrave.Count <= 0) return; // no card to revive
		UtilityFuncManagerScript.CopyList<GameObject>(physicalCardsInGrave, physicalCardsInDeck, false);
		physicalCardsInGrave.Clear();
	}

	/// <summary>
	/// Build dictionary mapping CardScript to physical card from all physical cards (deck + grave)
	/// </summary>
	public void BuildCardScriptToPhysicalDictionary()
	{
		_cardScriptToPhysicalCache.Clear();
		
		// Add physical cards from deck
		foreach (var physicalCard in physicalCardsInDeck)
		{
			var physCardScript = physicalCard.GetComponent<CardPhysObjScript>();
			if (physCardScript != null && physCardScript.cardImRepresenting != null)
			{
				_cardScriptToPhysicalCache[physCardScript.cardImRepresenting] = physicalCard;
			}
		}
		
		// Add physical cards from grave
		foreach (var physicalCard in physicalCardsInGrave)
		{
			var physCardScript = physicalCard.GetComponent<CardPhysObjScript>();
			if (physCardScript != null && physCardScript.cardImRepresenting != null)
			{
				_cardScriptToPhysicalCache[physCardScript.cardImRepresenting] = physicalCard;
			}
		}
	}
	
	/// <summary>
	/// Get physical card from logical card (CardScript). Returns null if not found.
	/// </summary>
	public GameObject GetPhysicalCardFromLogicalCard(CardScript logicalCard)
	{
		if (_cardScriptToPhysicalCache.TryGetValue(logicalCard, out var physicalCard))
		{
			return physicalCard;
		}
		return null;
	}

	// copy combined deck's order
	public void CopyCombinedDeckOrder()
	{
		if (physicalCardsInDeck.Count == 0) return;

		// Find and remove StartCard (it has no CardPhysObjScript or cardImRepresenting is null)
		GameObject startCard = null;
		List<GameObject> actualCards = new();
		foreach (var physicalCard in physicalCardsInDeck)
		{
			var physCardScript = physicalCard.GetComponent<CardPhysObjScript>();
			if (physCardScript == null || physCardScript.cardImRepresenting == null)
			{
				startCard = physicalCard;
			}
			else
			{
				actualCards.Add(physicalCard);
			}
		}

		// Build dictionary from current physical cards
		BuildCardScriptToPhysicalDictionary();

		// Reorder physicalCardsInDeck according to combinedDeckZone
		physicalCardsInDeck.Clear();
		foreach (var logicalCard in _combatManger.combinedDeckZone)
		{
			var cardScript = logicalCard.GetComponent<CardScript>();
			if (_cardScriptToPhysicalCache.TryGetValue(cardScript, out var physicalCard))
			{
				physicalCardsInDeck.Add(physicalCard);
			}
		}

		// Put StartCard at the end if it exists
		if (startCard != null)
		{
			physicalCardsInDeck.Add(startCard);
		}
		ResetPhysicalCardsPosAndSize();
	}

	// reset physical cards pos
	public void ResetPhysicalCardsPosAndSize()
	{
		if (physicalCardsInDeck.Count == 0) return;

		// Move all cards back to deck position with z offset
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			Vector3 targetPos = new(physicalCardDeckPos.position.x, physicalCardDeckPos.position.y, physicalCardDeckPos.position.z - zOffset * i);
			MovePhysicalCard(physicalCardsInDeck[i], targetPos);
			ScalePhysicalCard(physicalCardsInDeck[i],physicalCardDeckSize);
			// StartCoroutine(LerpCardPos(physicalCardsInDeck[i], targetPos, lerpTimeMove));
			// StartCoroutine(LerpCardSize(physicalCardsInDeck[i], physicalCardDeckSize, lerpTimeSize));
		}
	}

	public void InstantiateAllPhysicalCards()
	{
		// if physical deck is already made, return
		if (physicalCardsInDeck.Count > 0) return;
		// for each card in combined deck, instantiate a physical card
		foreach (var card in _combatManger.combinedDeckZone)
		{
			CardScript cardScript = card.GetComponent<CardScript>();
			GameObject newPhysicalCard = Instantiate(physicalCardPrefab);
			CardPhysObjScript newPhysicalCardScript = newPhysicalCard.GetComponent<CardPhysObjScript>();
			newPhysicalCardScript.cardImRepresenting = cardScript;
			newPhysicalCardScript.cardNamePrint.text = card.name;
			newPhysicalCardScript.cardDescPrint.text = cardScript.cardDesc;
			newPhysicalCard.transform.localScale = physicalCardDeckSize;
			physicalCardsInDeck.Add(newPhysicalCard);
		}
		// apply z offset
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			physicalCardsInDeck[i].transform.position = new Vector3(transform.position.x, transform.position.y, physicalCardDeckPos.position.z - zOffset * i);
		}
		MakeStartCard();
	}

	public void StartALerpCardPos(GameObject card, Vector3 end)
	{
		StartCoroutine(LerpCardPos(card, end, lerpTimeMove));
	}
	
	public void StartALerpCardSize(GameObject card, Vector3 targetSize)
	{
		StartCoroutine(LerpCardSize(card, targetSize, lerpTimeSize));
	}

	// for now
	public void MovePhysicalCard(GameObject card, Vector3 end)
	{
		card.transform.position = end;
	}

	public void ScalePhysicalCard(GameObject card, Vector3 targetSize)
	{
		card.transform.localScale = targetSize;
	}

	public void MovePhysicalCardFromGraveToDeck(GameObject card)
	{
		GameObject physicalCard;
		
		// Check if the input is already a physical card or a logical card
		var cardScript = card.GetComponent<CardScript>();
		if (cardScript == null)
		{
			// Input is already a physical card (no CardScript attached)
			physicalCard = card;
		}
		else
		{
			// Input is a logical card with CardScript, need to find corresponding physical card
			BuildCardScriptToPhysicalDictionary();
			physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
			
			if (physicalCard == null)
			{
				Debug.LogWarning($"MovePhysicalCardFromGraveToDeck: Could not find physical card for {card.name}");
				return;
			}
		}
		
		MovePhysicalCard(physicalCard, physicalCardDeckPos.position);
		ScalePhysicalCard(physicalCard, physicalCardDeckSize);
		physicalCardsInDeck.Add(physicalCard);
		physicalCardsInGrave.Remove(physicalCard);
		CopyCombinedDeckOrder();
	}

	IEnumerator LerpCardPos(GameObject card, Vector3 end, float timeToMove)
	{
		float t = 0;
		while (t < 1 && Vector3.Distance(card.transform.position, end) > 0.01f)
		{
			card.transform.position = Vector3.Lerp(card.transform.position, end, t);
			t += Time.deltaTime / timeToMove;
			//print("pos t: " + t);
			yield return null;
		}
		card.transform.position = end;
	}

	IEnumerator LerpCardSize(GameObject card, Vector3 targetSize, float timeToShrink)
	{
		float t = 0;
		while (t < 1 && Vector3.Distance(card.transform.localScale, targetSize) > 0.01f)
		{
			card.transform.localScale = Vector3.Lerp(card.transform.localScale, targetSize, t);
			t += Time.deltaTime / timeToShrink;
			yield return null;
		}
		card.transform.localScale = targetSize;
	}

	private void MakeStartCard()
	{
		var newStartCard = Instantiate(startCardPrefab);
		newStartCard.transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z - zOffset * physicalCardsInDeck.Count);
		physicalCardsInDeck.Add(newStartCard);
	}
}
