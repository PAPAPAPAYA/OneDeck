using UnityEngine;

/// <summary>
/// Presenter responsible for displaying cost-check failures.
/// Logic layer (CostNEffectContainer) returns CostCheckResult;
/// this presenter decides how and when to render it.
/// </summary>
public class CostResultPresenter : MonoBehaviour
{
	public static CostResultPresenter me;

	private void Awake()
	{
		me = this;
	}

	/// <summary>
	/// Handle a failed cost check. Decides whether to write to CombatLog
	/// based on UI rules (e.g. only show messages for cards in reveal zone).
	/// </summary>
	public void PresentCostFailure(CostCheckResult result, CardScript card, CostNEffectContainer source)
	{
		if (result.success) return;
		if (result.failMessages == null || result.failMessages.Count == 0) return;

		// Only display fail messages if card is in reveal zone
		if (CombatManager.Me == null || CombatManager.Me.revealZone == null) return;
		if (CombatManager.Me.revealZone != source.transform.parent.gameObject) return;

		foreach (var msg in result.failMessages)
		{
			CombatLog.me?.Append(msg);
		}
	}
}
