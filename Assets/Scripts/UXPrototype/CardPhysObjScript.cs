using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CardPhysObjScript : MonoBehaviour
{
	public CardScript cardImRepresenting;
	private CombatUXManager _combatUXManager;
	[Header("MOTION CONTROL")]
	[SerializeField] private CoroutineSequencer sequencer;
	[Header("LOOK")]
	public SpriteRenderer cardFace;
	public SpriteRenderer cardEdge;
	public TextMeshPro cardNamePrint;
	public TextMeshPro cardDescPrint;

	[Header("COLOR")]
	public Color ownerCardColor;
	public Color ownerCardEdgeColor;
	public Color opponentCardColor;
	public Color opponentCardEdgeColor;

    void OnEnable()
    {
        _combatUXManager = CombatUXManager.me;
    }

    void Update()
	{
		ApplyColor();
	}
	
	private void ApplyColor()
	{
		if (cardImRepresenting.myStatusRef != CombatManager.Me.ownerPlayerStatusRef)
		{
			// this belongs to opponent
			cardEdge.color = opponentCardEdgeColor;
			cardFace.color = opponentCardColor;
		}
		else
		{
			// this belongs to session owner
			cardEdge.color = ownerCardEdgeColor;
			cardFace.color = ownerCardColor;
		}
	}
}
