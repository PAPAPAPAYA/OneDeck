using System.Collections;
using System.Collections.Generic;
using System.Security.Principal;
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

	public List<GameObject> physicalCardsInDeck = new();
	public GameObject physicalCardPrefab;

	[Header("DECK")]
	public GameObject startCardPrefab;
	public float zOffset;
	public Transform physicalCardDeckPos;
	public float physicalCardDeckSize;
	public float lerpTimeMove;

	[Header("GRAVE")]
	public float physicalCardGraveSize;
	public Transform physicalCardGravePos;
	public float lerpTimeSize;
	public List<GameObject> physicalCardsInGrave = new();



	private CombatManager _combatManger;

	private void OnEnable()
	{
		_combatManger = CombatManager.Me;
	}

	public void RevealNextPhysicalCard()
	{
		float baseZ = physicalCardsInGrave.Count > 0? physicalCardsInGrave[0].transform.position.z:physicalCardsInDeck[^1].transform.position.z;
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
		StartCoroutine(LerpCardPos(physicalCardsInDeck[^1], physicalCardGravePos.position, lerpTimeMove));
		StartCoroutine(LerpCardSize(physicalCardsInDeck[^1], new Vector3(physicalCardGraveSize, physicalCardGraveSize, physicalCardGraveSize), lerpTimeSize));
		physicalCardsInDeck.RemoveAt(physicalCardsInDeck.Count - 1);
	}

	// put all physical cards from grave to deck
	public void ReviveAllPhysicalCards()
	{
		if (physicalCardsInGrave.Count <= 0) return; // no card to revive
		UtilityFuncManagerScript.CopyList<GameObject>(physicalCardsInGrave, physicalCardsInDeck, false);
		physicalCardsInGrave.Clear();
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

		// Build a dictionary mapping CardScript to physical card
		Dictionary<CardScript, GameObject> cardScriptToPhysical = new Dictionary<CardScript, GameObject>();
		foreach (var physicalCard in actualCards)
		{
			var physCardScript = physicalCard.GetComponent<CardPhysObjScript>();
			if (physCardScript != null && physCardScript.cardImRepresenting != null)
			{
				cardScriptToPhysical[physCardScript.cardImRepresenting] = physicalCard;
			}
		}

		// Reorder physicalCardsInDeck according to combinedDeckZone
		physicalCardsInDeck.Clear();
		foreach (var logicalCard in _combatManger.combinedDeckZone)
		{
			var cardScript = logicalCard.GetComponent<CardScript>();
			if (cardScriptToPhysical.TryGetValue(cardScript, out var physicalCard))
			{
				physicalCardsInDeck.Add(physicalCard);
			}
		}

		// Put StartCard at the end if it exists
		if (startCard != null)
		{
			physicalCardsInDeck.Add(startCard);
		}
	}

	// reset physical cards pos
	public void ResetPhysicalCardsPosAndSize()
	{
		if (physicalCardsInDeck.Count == 0) return;

		// Move all cards back to deck position with z offset
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			Vector3 targetPos = new(physicalCardDeckPos.position.x, physicalCardDeckPos.position.y, physicalCardDeckPos.position.z - zOffset * i);
			StartCoroutine(LerpCardPos(physicalCardsInDeck[i], targetPos, lerpTimeMove));
			StartCoroutine(LerpCardSize(physicalCardsInDeck[i], new(physicalCardDeckSize, physicalCardDeckSize, physicalCardDeckSize), lerpTimeSize));
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
			newPhysicalCard.transform.localScale = new(physicalCardDeckSize, physicalCardDeckSize, physicalCardDeckSize);
			physicalCardsInDeck.Add(newPhysicalCard);
		}
		// apply z offset
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			physicalCardsInDeck[i].transform.position = new Vector3(transform.position.x, transform.position.y, physicalCardDeckPos.position.z - zOffset * i);
		}
		MakeStartCard();
	}

	IEnumerator LerpCardPos(GameObject card, Vector3 end, float timeToMove)
	{
		float t = 0;
		while (t < 1 && Vector3.Distance(card.transform.position, end) > 0.01f)
		{
			card.transform.position = Vector3.Lerp(card.transform.position, end, t);
			t += Time.deltaTime / timeToMove;
			print("pos t: " + t);
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
			print("size t: " + t);
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
