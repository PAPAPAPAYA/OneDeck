using System.Collections.Generic;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Headless tests for CostResultPresenter cost-failure logging behaviour.
/// </summary>
public class CostResultPresenterTests : HeadlessCombatTestFixture
{
	[TearDown]
	public void CleanupPresenterSingleton()
	{
		if (CostResultPresenter.me != null)
			CostResultPresenter.me = null;
	}

	[Test]
	public void PresentCostFailure_Headless_DoesNotThrow()
	{
		var presenterObj = CreateGameObject("CostResultPresenter");
		var presenter = presenterObj.AddComponent<CostResultPresenter>();

		var card = CreateCard(true, "CostFailCard");
		var container = CreateCostContainer(card);
		CombatManager.revealZone = card;

		var result = new CostCheckResult(false, new List<string> { "Mana不足" });

		Assert.DoesNotThrow(() => presenter.PresentCostFailure(result, card.GetComponent<CardScript>(), container));
	}

	[Test]
	public void PresentCostFailure_Headless_NoPhysicalCard_DoesNotThrow()
	{
		var presenterObj = CreateGameObject("CostResultPresenter");
		var presenter = presenterObj.AddComponent<CostResultPresenter>();

		var card = CreateCard(true, "CostFailCard");
		var container = CreateCostContainer(card);
		CombatManager.revealZone = card;

		var result = new CostCheckResult(false, new List<string> { "Mana不足" });

		// In headless mode GetPhysicalCard returns null; PresentCostFailure should not throw.
		Assert.DoesNotThrow(() => presenter.PresentCostFailure(result, card.GetComponent<CardScript>(), container));
	}
}
