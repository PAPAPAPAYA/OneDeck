using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// EditMode prefab audit for the attack-attribute transitional invariants (phase-3 review #4):
/// 1. No active card may bind legacy Power-granting methods — AttackEffect does not count
///    Power layers, so a mixed card silently loses damage.
/// 2. No active card carries PowerReactionEffect / StatusEffectAmplifierEffect components.
/// 3. Phase-3 rewired cards use the attack-attribute methods / resolver components.
/// </summary>
public class AttackAttributeConsistencyTests
{
	private const string ActiveCardFolder = "Assets/Prefabs/Cards/3.0 no cost (current)";

	private static readonly string[] LegacyPowerGrantingMethods =
	{
		"GiveStatusEffect",
		"GiveAllFriendlyStatusEffect",
		"GiveStatusEffectToLastXCards",
		"GiveStatusEffectToXFriendly",
		"ApplyStatusEffectCore",
	};

	private static string ResolverGuid
	{
		get { return AssetDatabase.AssetPathToGUID("Assets/Scripts/Effects/AttackResolverSource.cs"); }
	}

	private static string[] ActivePrefabPaths()
	{
		return AssetDatabase.FindAssets("t:Prefab", new[] { ActiveCardFolder })
			.Select(AssetDatabase.GUIDToAssetPath)
			.Where(p => p.EndsWith(".prefab") && !p.Contains("_DONT INCLUDE"))
			.OrderBy(p => p)
			.ToArray();
	}

	private static string PrefabText(string cardId)
	{
		var path = ActivePrefabPaths().FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == cardId);
		Assert.IsNotNull(path, "Prefab not found in active pool: " + cardId);
		return File.ReadAllText(path);
	}

	/// <summary>
	/// Reads a prefab by card ID from the WHOLE card folder tree, including _DONT INCLUDE
	/// (token prefabs like JU_ON live there and are still part of the card pool).
	/// </summary>
	private static string AnyPrefabText(string cardId)
	{
		var path = AssetDatabase.FindAssets("t:Prefab", new[] { ActiveCardFolder })
			.Select(AssetDatabase.GUIDToAssetPath)
			.FirstOrDefault(p => p.EndsWith(".prefab") && Path.GetFileNameWithoutExtension(p) == cardId);
		Assert.IsNotNull(path, "Prefab not found anywhere under the card folder: " + cardId);
		return File.ReadAllText(path);
	}

	[Test]
	public void NoActiveCardBindsLegacyPowerGrantingMethods()
	{
		var offenders = new List<string>();
		foreach (var path in ActivePrefabPaths())
		{
			var text = File.ReadAllText(path);
			foreach (var method in LegacyPowerGrantingMethods)
			{
				if (text.Contains("m_MethodName: " + method))
				{
					offenders.Add(Path.GetFileName(path) + " -> " + method);
				}
			}
		}
		Assert.IsEmpty(offenders,
			"No active card may bind legacy Power-granting methods (mixed Power + attack silently loses damage):\n" +
			string.Join("\n", offenders));
	}

	[Test]
	public void NoActiveCardHasPowerReactionOrAmplifierComponents()
	{
		var offenders = ActivePrefabPaths()
			.Where(p =>
			{
				var text = File.ReadAllText(p);
				return text.Contains("PowerReactionEffect") || text.Contains("StatusEffectAmplifierEffect");
			})
			.Select(Path.GetFileName)
			.ToArray();
		Assert.IsEmpty(offenders, "Obsolete Power reaction/amplifier components must not exist:\n" + string.Join("\n", offenders));
	}

	[Test]
	public void NoActiveAttackEffectCardStillBindsLegacyDynamicDamageMethods()
	{
		// Dynamic attack cards must settle through AttackEffect.Attack* (attack attribute),
		// not through the legacy IntSO/count damage channels (which bypass the attack display).
		var legacyMethods = new[]
		{
			"DecreaseTheirHp_BasedOnIntSO",
			"DecreaseTheirHp_BasedOnFriendlyCardCountInDeck",
			"DecreaseTheirHp_BasedOnOpponentBuriedCount",
			"DecreaseTheirHpTimes_BasedOnIntSO",
			"DecreaseTheirHpTimes_BasedOnOpponentBuriedCount",
			"DecreaseTheirHpTimesX",
		};
		var offenders = new List<string>();
		foreach (var path in ActivePrefabPaths())
		{
			var text = File.ReadAllText(path);
			foreach (var method in legacyMethods)
			{
				if (text.Contains("m_MethodName: " + method))
				{
					offenders.Add(Path.GetFileName(path) + " -> " + method);
				}
			}
		}
		Assert.IsEmpty(offenders, "No active card may bind legacy damage-channel methods:\n" + string.Join("\n", offenders));
	}

	[Test]
	public void Phase3RewiredCardsUseAttackAttributeMethods()
	{
		Assert.IsTrue(PrefabText("THE_FOOL").Contains("m_MethodName: StageCardWithMaxAttack"),
			"THE_FOOL must stage the card with the highest attack");

		var powerTransfer = PrefabText("POWER_TRANSFER");
		Assert.IsTrue(powerTransfer.Contains("m_MethodName: ConsumeEnemyCardWithMaxAttack"),
			"POWER_TRANSFER must consume from the enemy card with the highest attack");
		Assert.IsTrue(powerTransfer.Contains("m_MethodName: GiveFriendlyCardWithMinAttack"),
			"POWER_TRANSFER must give to the friendly card with the lowest attack");

		Assert.IsTrue(PrefabText("BONE_COMBINATION").Contains("m_MethodName: AttackTimesBasedOnOpponentBuriedCount"),
			"BONE_COMBINATION must attack once per enemy buried count");
		Assert.IsTrue(PrefabText("BODY_CANON").Contains("m_MethodName: AttackTimesBasedOnIntSO"),
			"BODY_CANON must attack once per grave-friendly count");
	}

	[Test]
	public void JuOnUsesAttackSelfWithoutLegacyPowerDamage()
	{
		var juOn = AnyPrefabText("JU_ON");
		Assert.IsTrue(juOn.Contains("m_MethodName: AttackSelf"), "JU_ON self-damage must resolve through AttackSelf");
		Assert.IsFalse(juOn.Contains("m_MethodName: DecreaseMyHp"), "JU_ON must not use the legacy HP channel");
		Assert.IsTrue(juOn.Contains("baseDmg: {fileID: 0}"), "JU_ON must not carry a power-based baseDmg");
	}

	[Test]
	public void DynamicAttackCardsCarryTheResolverComponent()
	{
		Assert.IsTrue(ResolverGuid != string.Empty, "AttackResolverSource must have an imported GUID");
		Assert.IsTrue(PrefabText("ALL_FOR_ONE").Contains(ResolverGuid), "ALL_FOR_ONE must carry AttackResolverSource");
		Assert.IsTrue(PrefabText("FLESH_COMBINATION").Contains(ResolverGuid), "FLESH_COMBINATION must carry AttackResolverSource");
		Assert.IsTrue(PrefabText("ALMIGHTY").Contains(ResolverGuid), "ALMIGHTY must carry AttackResolverSource");
	}

	[Test]
	public void PureStatCardsNoLongerDealRevealDamage()
	{
		foreach (var id in new[] { "ALL_FOR_ONE", "FLESH_COMBINATION" })
		{
			var text = PrefabText(id);
			Assert.IsFalse(text.Contains("DecreaseTheirHp"), id + " must not deal damage anymore (pure attack = Y stat card)");
		}
	}
}
