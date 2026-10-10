using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using TestWriteRead;
using UnityEngine;

/// <summary>
/// World-space opponent-select page builder (plan plan-opponent-select-page-2026-10-06
/// §4.3, 2026-10-10 revision): instantiates one OpponentSelectPanel v2 per ghost
/// candidate at the layout-plan measured positions (side-by-side, single candidate
/// centered), fills each panel from its candidate (key cards per
/// <see cref="OpponentKeyCardSelector"/>, username / hpMax on the enemy plate,
/// bountyPlaceholder on the select button) and pairs the page's input block. The panel
/// visual identity lives entirely in the prefab — this class only pushes strings and
/// actions (binder doctrine). Picks are reported through <see cref="onPicked"/>; the
/// transition wiring (ConsumeCandidate + leg2) is S3.
/// </summary>
public class OpponentSelectPage : MonoBehaviour
{
	// Layout plan §2 measured page-local positions (wu).
	public const float PanelPitchX = 4.90f;
	public const float PanelY = 0.36f;

	private const string RootName = "Opponent Select Page";

	public static OpponentSelectPage Instance { get; private set; }

	/// <summary>Index into Candidates — wired by the S3 transition (Consume + leg2).</summary>
	public Action<int> onPicked;

	public readonly List<OpponentSelectPanel> PanelViews = new List<OpponentSelectPanel>();
	public readonly List<OpponentDeckEntry> Candidates = new List<OpponentDeckEntry>();
	/// <summary>Per panel: the instantiated key-card faces (null where a slot stayed empty) — S3 takeoff origins.</summary>
	public readonly List<GameObject[]> KeyCardFaces = new List<GameObject[]>();

	/// <summary>The picked panel's enemy plate root — the S3 leg2 flight object.</summary>
	public Transform PickedPlate { get; private set; }
	/// <summary>The picked panel's key-card faces — the S3 spawn-at-takeoff origins.</summary>
	public GameObject[] PickedFaces { get; private set; }

	/// <summary>
	/// Leg2 preparation (driver, at pick): hide everything that does not fly — the
	/// unpicked panels vanish and the picked panel keeps only its enemy plate and key-card
	/// faces (they translate to the combat homes while the camera travels).
	/// </summary>
	public void PrepareLeg2Flights(int pickedIndex)
	{
		for (int i = 0; i < PanelViews.Count; i++)
		{
			if (i != pickedIndex)
			{
				if (PanelViews[i] != null) PanelViews[i].gameObject.SetActive(false);
				continue;
			}
			OpponentSelectPanel view = PanelViews[i];
			if (view == null) continue;
			if (view.panelBg != null) view.panelBg.gameObject.SetActive(false);
			if (view.selectButton != null) view.selectButton.gameObject.SetActive(false);
			PickedPlate = view.plateNameBinding != null ? view.plateNameBinding.transform : null;
			PickedFaces = i < KeyCardFaces.Count ? KeyCardFaces[i] : null;
		}
	}

	/// <summary>
	/// §4.5 bypass decision, single source for the S3 driver branch: the select page runs
	/// only when both masters allow it, no debug deck is forced, and the cache holds at
	/// least one candidate. The peek happens ONCE here — the same list must flow into
	/// <see cref="Bootstrap"/> so the page shows exactly what the decision saw
	/// (PeekCandidates randomizes per call).
	/// </summary>
	public static bool TryPeekForSession(int sessionNum, out List<OpponentDeckEntry> candidates)
	{
		candidates = null;
		PhaseTransitionConfigSO cfg = PhaseTransitionConfigSO.Me;
		if (cfg == null || !cfg.enabled || !cfg.selectionPageEnabled) return false;
		if (!PhaseTransitionDriver.Available) return false;  // batch / overrideCombatSeed / NullVisuals
		if (DeckSaver.Me != null && DeckSaver.Me.useDebugEnemyDeck) return false;
		candidates = OpponentDeckCache.PeekCandidates(sessionNum, cfg.selectionCandidateCount);
		return candidates != null && candidates.Count > 0;
	}

	/// <summary>
	/// Build (or rebuild) the page from an already-peeked candidate list. pageY is the
	/// page-local origin in world space (S3 passes the selection-page Y; editor probes
	/// pass 0). Activates and input-blocks the page before returning.
	/// </summary>
	public static OpponentSelectPage Bootstrap(List<OpponentDeckEntry> candidates, float pageY)
	{
		if (candidates == null || candidates.Count == 0) return null;
		PhaseTransitionConfigSO cfg = PhaseTransitionConfigSO.Me;
		if (cfg == null || cfg.opponentSelectPanelPrefab == null)
		{
			TestManager.LogWarning("[OpponentSelectPage] config or panel prefab missing — select page skipped");
			return null;
		}

		TeardownStatic();
		GameObject rootGo = new GameObject(RootName);
		rootGo.SetActive(false);
		Instance = rootGo.AddComponent<OpponentSelectPage>();
		rootGo.transform.position = new Vector3(0f, pageY, 0f);
		Instance.Build(candidates, cfg);
		Instance.Show();
		return Instance;
	}

	/// <summary>Static teardown: unblocks input and destroys the page (idempotent).</summary>
	public static void TeardownStatic()
	{
		if (Instance == null) return;
		Instance.Hide();
		Instance.onPicked = null;
		UnityEngine.Object.Destroy(Instance.gameObject);
		Instance = null;
	}

	public void Show()
	{
		gameObject.SetActive(true);
		if (CombatManager.Me != null) CombatManager.Me.BlockInput(this);
	}

	public void Hide()
	{
		if (CombatManager.Me != null) CombatManager.Me.UnblockInput(this);
		gameObject.SetActive(false);
	}

	private void Build(List<OpponentDeckEntry> candidates, PhaseTransitionConfigSO cfg)
	{
		int count = candidates.Count;
		for (int i = 0; i < count; i++)
		{
			// Layout plan §2: candidate 0 left, candidate 1 right, single candidate centered.
			float x = count == 1 ? 0f : (i == 0 ? -PanelPitchX : PanelPitchX);
			GameObject panelGo = UnityEngine.Object.Instantiate(cfg.opponentSelectPanelPrefab, transform);
			panelGo.name = "CandidatePanel" + i;
			panelGo.transform.localPosition = new Vector3(x, PanelY, 0f);

			OpponentSelectPanel view = panelGo.GetComponent<OpponentSelectPanel>();
			Fill(view, candidates[i], cfg);

			int index = i;  // closure over the loop variable
			if (view.selectButton != null) view.selectButton.SetWorldAction(() => Picked(index));

			PanelViews.Add(view);
			Candidates.Add(candidates[i]);
		}
	}

	private void Fill(OpponentSelectPanel view, OpponentDeckEntry candidate, PhaseTransitionConfigSO cfg)
	{
		// Plate: username (??? while unnamed) + the ghost's saved hpMax, entered at full.
		string username = string.IsNullOrEmpty(candidate.username) ? "???" : candidate.username;
		if (view.plateNameBinding != null) view.plateNameBinding.Apply(username, string.Empty);
		int hpMax = candidate.hpMax > 0 ? candidate.hpMax : 20;
		if (view.plateCountBindings != null)
		{
			foreach (HudCountBinding binding in view.plateCountBindings)
				binding.SetTarget(hpMax, Mathf.Max(1, hpMax));
		}

		// Key cards: rule-selected display faces replace the baked placeholders; a slot
		// without a card hides both (short-deck ruling: empty slots stay closed).
		List<CardScript> keys = OpponentKeyCardSelector.Select(ResolveDeckCards(candidate));
		var faces = new GameObject[OpponentKeyCardSelector.KeyCardCount];
		for (int slot = 0; slot < faces.Length; slot++)
		{
			Transform slotTransform = view.cardSlots != null && slot < view.cardSlots.Length ? view.cardSlots[slot] : null;
			if (slotTransform == null) continue;
			GameObject placeholder = view.placeholderCards != null && slot < view.placeholderCards.Length
				? view.placeholderCards[slot]
				: null;
			if (slot >= keys.Count)
			{
				if (placeholder != null) placeholder.SetActive(false);
				continue;
			}
			faces[slot] = CreateKeyCardFace(keys[slot], slotTransform, placeholder);
		}
		KeyCardFaces.Add(faces);

		// Select button: display-only bounty (P3) — the action reports the pick to S3.
		if (view.selectLabel != null) view.selectLabel.text = "选择 +$" + cfg.bountyPlaceholder;
	}

	/// <summary>
	/// One display face from the ghost's real card: PhysicalCardParent instance riding the
	/// authored placeholder transform (it carries the slot centering), prints pushed the
	/// same way CombatUXManager's spawn does, shown face-up immediately.
	/// </summary>
	private GameObject CreateKeyCardFace(CardScript cardScript, Transform slot, GameObject placeholder)
	{
		CombatUXManager ux = CombatUXManager.me;
		GameObject prefab = ux != null ? ux.physicalCardPrefab : null;
		if (prefab == null)
		{
			TestManager.LogWarning("[OpponentSelectPage] physicalCardPrefab missing — key card slot left empty");
			return null;
		}
		GameObject face = UnityEngine.Object.Instantiate(prefab, slot);
		face.name = "KeyCardFace_" + cardScript.GetDisplayName();
		if (placeholder != null)
		{
			face.transform.localPosition = placeholder.transform.localPosition;
			face.transform.localRotation = placeholder.transform.localRotation;
			face.transform.localScale = placeholder.transform.localScale;
			placeholder.SetActive(false);
		}

		CardPhysObjScript phys = face.GetComponent<CardPhysObjScript>();
		if (phys != null)
		{
			phys.cardImRepresenting = cardScript;
			if (phys.cardNamePrint != null) phys.cardNamePrint.text = cardScript.GetDisplayName();
			if (phys.cardDescPrint != null) phys.cardDescPrint.text = cardScript.GetCardDescForDisplay();
			phys.RefreshAttackDisplay();
			phys.SetFaceUp(true, false);
		}
		return face;
	}

	/// <summary>Ghost cardTypeIDs → resolved CardScripts (unresolvable ids are skipped; the pick-time populate guards the whole deck).</summary>
	private static List<CardScript> ResolveDeckCards(OpponentDeckEntry candidate)
	{
		var cards = new List<CardScript>();
		if (candidate == null || candidate.cardTypeIDs == null) return cards;
		DeckSaver saver = DeckSaver.Me;
		foreach (string typeID in candidate.cardTypeIDs)
		{
			if (string.IsNullOrEmpty(typeID)) continue;
			GameObject prefab = saver != null ? saver.GetCardPrefabByTypeID(typeID) : null;
			if (prefab == null) continue;
			CardScript cardScript = prefab.GetComponent<CardScript>();
			if (cardScript != null) cards.Add(cardScript);
		}
		return cards;
	}

	private void Picked(int index)
	{
		if (onPicked != null) onPicked(index);
	}
}
