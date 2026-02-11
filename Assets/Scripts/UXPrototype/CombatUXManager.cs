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

	public CardPhysObjScript revealedPhysicalCard;
	public GameObject physicalCardPrefab;
	
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
	
	public void InstantiateAllPhysicalCards()
	{
		foreach (var card in _combatManger.combinedDeckZone)
		{
			CardScript cardScript = card.GetComponent<CardScript>();
			GameObject newPhysicalCard = Instantiate(physicalCardPrefab, transform);
			CardPhysObjScript newPhysicalCardScript = newPhysicalCard.GetComponent<CardPhysObjScript>();
			newPhysicalCardScript.cardNamePrint.text = card.name;
			newPhysicalCardScript.cardDescPrint.text = cardScript.cardDesc;
		}
	}
}
