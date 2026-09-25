using UnityEngine;

/// <summary>
/// Interaction slot of a prefab HUD widget (plan-shop-hud-prefab-widgets §3.3): points
/// at a PhysButton and names the action. ShopHudBinder.Collect wires button →
/// Execute(key); the binding holds no behavior itself, so prefab variants only override
/// key/button refs.
/// </summary>
public class HudActionBinding : MonoBehaviour
{
	public enum ActionKey
	{
		LeaveShop,
		Options,
	}

	public ActionKey key = ActionKey.LeaveShop;
	public PhysButton button;
}
