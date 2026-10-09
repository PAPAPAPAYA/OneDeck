using TMPro;
using UnityEngine;

/// <summary>
/// Dumb reference hub for one authored opponent-select candidate panel
/// (plans/plan-opponent-select-panel-prefab-2026-10-08.md, rulings P1/P2/P3 2026-10-08).
/// Visual identity lives entirely in OpponentSelectPanel.prefab data; this class only
/// carries the serialized references the select-page pump (main plan
/// plan-opponent-select-page-2026-10-06.md S2) fills per candidate: the enemy plate's
/// bindings, the select PhysButton + its label, the three key-card slot anchors and
/// their baked placeholder card handles. No candidate parsing, no cache access, no
/// tweens — binder doctrine (ShopHudBinder precedent).
/// Fill contract (page-side duty): hide the placeholder handles, instantiate the ghost
/// deck's real card faces under the slots, push plateNameBinding.Apply(username, ""),
/// SetTarget(hp, hpMax) on every count binding, and format the bounty into selectLabel.
/// The plate is static-authored at the combat-plate size on purpose — the morph pick
/// flight is pure translation, so no fill-time resizing happens on the plate itself.
/// </summary>
public class OpponentSelectPanel : MonoBehaviour
{
	[Header("Panel")]
	public SpriteRenderer panelBg;

	[Header("Enemy plate")]
	public HudTextBinding plateNameBinding;
	public HudCountBinding[] plateCountBindings;

	[Header("Select button")]
	public PhysButton selectButton;
	public TMP_Text selectLabel;

	[Header("Key cards")]
	public Transform[] cardSlots;
	public GameObject[] placeholderCards;
}
