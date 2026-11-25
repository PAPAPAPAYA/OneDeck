using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// EditMode tests for the hover-tooltip card-reference block (CardRefTooltipBlocks +
/// CardTagTooltip second label): the bare-name vs tag-phrase resolution rule, the
/// CardTooltipDatabase singleton data (RIFT believer + JU_ON curse entries), goldens
/// against the real 4.0 card pool (15 cards bare-name the believer token, 22 the
/// curse token, 2 name both), the prefab wiring guards (serialized references to the
/// RIFT prefab / RiftCardTypeID SO, the JU_ON prefab, and the inline
/// cardTypeID: JU_ON wiring string), and the real two-label layout (ApplyContent in
/// a preview scene). The tooltip instance is built by reflection inside a preview
/// scene so the open GameScene stays clean.
/// </summary>
public class CardRefTooltipBlocksTests
{
	private const string BelieverName = "信徒";
	private const string CurseName = "诅咒";
	private const string RelicHivePath = "Assets/Prefabs/Cards/4.0/2_Rare/RELIC_HIVE.prefab";
	private const string RiftReaperPath = "Assets/Prefabs/Cards/4.0/2_Rare/RIFT_REAPER.prefab";
	private const string RiftShepherdPath = "Assets/Prefabs/Cards/4.0/0_Common/RIFT_SHEPHERD.prefab";
	private const string MassSacrificePath = "Assets/Prefabs/Cards/4.0/2_Rare/MASS_SACRIFICE.prefab";
	private const string RiftAcolytePath = "Assets/Prefabs/Cards/4.0/0_Common/RIFT_ACOLYTE.prefab";
	private const string RelicRiftOverridePath = "Assets/Prefabs/Cards/4.0/2_Rare/RELIC_RIFT_OVERRIDE.prefab";
	private const string CurseEaterPath = "Assets/Prefabs/Cards/4.0/1_Uncommon/CURSE_EATER.prefab";
	private const string CurseGardenerPath = "Assets/Prefabs/Cards/4.0/1_Uncommon/CURSE_GARDENER.prefab";
	private const string SwarmCurserPath = "Assets/Prefabs/Cards/4.0/2_Rare/SWARM_CURSER.prefab";

	/// <summary>Pool cards whose raw cardDesc bare-names the believer token (verified set).</summary>
	private static readonly string[] RiftRefHitIds =
	{
		"MASS_SACRIFICE", "RELIC_HIVE", "RELIC_RIFT_OVERRIDE", "REVIVE_SUMMONER",
		"RIFT_ACOLYTE", "RIFT_GUIDE_4.0", "RIFT_HATCHERY", "RIFT_INSECT_4.0",
		"RIFT_MEDIUM", "RIFT_PRIEST", "RIFT_REAPER", "RIFT_REVIVER",
		"RIFT_STRIKER", "SWARM_CURSER", "SWARM_QUEEN"
	};

	/// <summary>Pool cards whose raw cardDesc bare-names the curse token (verified set).</summary>
	private static readonly string[] JuOnRefHitIds =
	{
		"CURSE_EATER", "CURSE_GARDENER", "CURSE_REVIVER", "CURSE_SUMMONER",
		"CURSE_THIRST_BEAST_4.0", "CURSE_THIRST_SHAMAN_4.0", "DETERIORATION_4.0",
		"DOOM_HERALD", "GRAVE_HEXER", "HEXBLADE", "HEXER", "RELIC_ATTACK_HEX",
		"RELIC_BLOOD_PACT", "RELIC_CURSE_GRAVE", "RELIC_CURSE_HASTE",
		"RELIC_CURSE_REVIVAL", "RELIC_GRAVE_CURSE", "RELIC_RIFT_OVERRIDE",
		"RELIC_TALLY", "SACRIFICIAL_SPIRIT", "SWARM_CURSER", "WOKEN_HEX"
	};

	private static readonly BindingFlags InstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
	private static readonly BindingFlags StaticFlags = BindingFlags.NonPublic | BindingFlags.Static;
	private static readonly Type ContentStructType = typeof(CardTagTooltip).GetNestedType("TooltipContent", BindingFlags.NonPublic);

	private Scene _previewScene;
	private GameObject _tooltipGo;
	private CardTagTooltip _tooltip;
	private RectTransform _panel;
	private VerticalLayoutGroup _layout;
	private TextMeshProUGUI _text;
	private TextMeshProUGUI _cardRefsText;
	private readonly List<UnityEngine.Object> _transient = new List<UnityEngine.Object>();

	[OneTimeSetUp]
	public void CreateTooltipInPreviewScene()
	{
		_previewScene = EditorSceneManager.NewPreviewScene();
		_tooltipGo = new GameObject("CardTagTooltipTests");
		SceneManager.MoveGameObjectToScene(_tooltipGo, _previewScene);
		_tooltip = _tooltipGo.AddComponent<CardTagTooltip>();
		typeof(CardTagTooltip).GetMethod("BuildUI", InstanceFlags).Invoke(_tooltip, null);
		_panel = (RectTransform)typeof(CardTagTooltip).GetField("_panel", InstanceFlags).GetValue(_tooltip);
		_layout = (VerticalLayoutGroup)typeof(CardTagTooltip).GetField("_layout", InstanceFlags).GetValue(_tooltip);
		_text = (TextMeshProUGUI)typeof(CardTagTooltip).GetField("_text", InstanceFlags).GetValue(_tooltip);
		_cardRefsText = (TextMeshProUGUI)typeof(CardTagTooltip).GetField("_cardRefsText", InstanceFlags).GetValue(_tooltip);
		Assert.IsNotNull(_panel, "_panel");
		Assert.IsNotNull(_layout, "_layout");
		Assert.IsNotNull(_text, "_text");
		Assert.IsNotNull(_cardRefsText, "_cardRefsText");
		Assert.AreEqual("CardRefs", _cardRefsText.gameObject.name, "second label name");
	}

	[OneTimeTearDown]
	public void DestroyPreviewSceneAndTransient()
	{
		if (_tooltipGo != null) UnityEngine.Object.DestroyImmediate(_tooltipGo);
		_tooltipGo = null;
		if (_previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(_previewScene);
		foreach (UnityEngine.Object so in _transient)
		{
			if (so != null) UnityEngine.Object.DestroyImmediate(so);
		}
		_transient.Clear();
	}

	// ---------- helpers ----------

	private static GameObject LoadPrefab(string path)
	{
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		Assert.IsNotNull(prefab, "prefab missing: " + path);
		return prefab;
	}

	private static CardScript LoadCardScript(string path)
	{
		return LoadPrefab(path).GetComponent<CardScript>();
	}

	private static List<GameObject> LoadCardPool()
	{
		var pool = new List<GameObject>();
		string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Cards/4.0" });
		foreach (string guid in guids)
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (path.Contains("/-1_Test/")) continue;
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (prefab == null || prefab.GetComponent<CardScript>() == null) continue;
			pool.Add(prefab);
		}
		return pool;
	}

	private static CardScript RiftScript()
	{
		return CardTooltipDatabaseSO.Me.entries[0].cardPrefab.GetComponent<CardScript>();
	}

	private static string GoldenCardRefText()
	{
		CardScript rift = RiftScript();
		return "<b>" + rift.GetDisplayName() + "</b>\n" + rift.cardDesc;
	}

	private static CardScript JuOnScript()
	{
		return CardTooltipDatabaseSO.Me.entries[1].cardPrefab.GetComponent<CardScript>();
	}

	private static string GoldenCurseCardRefText()
	{
		CardScript juon = JuOnScript();
		return "<b>" + juon.GetDisplayName() + "</b>\n" + juon.cardDesc;
	}

	/// <summary>
	/// Test-side composition mirror of the production card-reference text (Resolve is
	/// guarded by the exact-hit-set golds; this checks only the block composition).
	/// </summary>
	private static string ExpectedCardRefText(CardScript card)
	{
		var sb = new StringBuilder();
		foreach (CardTooltipDatabaseSO.Entry entry in CardRefTooltipBlocks.Resolve(card, CardTooltipDatabaseSO.Me))
		{
			if (sb.Length > 0) sb.Append("\n\n");
			sb.Append("<b>").Append(CardRefTooltipBlocks.GetTitle(CardTooltipDatabaseSO.Me, entry)).Append("</b>");
			string body = CardRefTooltipBlocks.GetBody(entry);
			if (!string.IsNullOrEmpty(body)) sb.Append("\n").Append(body);
		}
		return sb.ToString();
	}

	/// <summary>
	/// True when any string serialized property on any component of the prefab equals the
	/// given value - the inline "cardTypeID: JU_ON" wiring form that object-reference
	/// scans cannot see (CURSE_EATER wires its curse consumption this way).
	/// </summary>
	private static bool PrefabHasInlineCardTypeIdValue(GameObject prefabRoot, string value)
	{
		foreach (Component component in prefabRoot.GetComponentsInChildren<Component>(true))
		{
			if (component == null) continue;
			var serialized = new SerializedObject(component);
			var iterator = serialized.GetIterator();
			while (iterator.Next(true))
			{
				if (iterator.propertyType != SerializedPropertyType.String) continue;
				if (iterator.stringValue == value) return true;
			}
		}
		return false;
	}

	private static MethodInfo BuildTooltipContentMethod()
	{
		return typeof(CardTagTooltip).GetMethod("BuildTooltipContent", StaticFlags, null, new[] { typeof(CardScript) }, null);
	}

	private static object BuildContent(CardScript card)
	{
		return BuildTooltipContentMethod().Invoke(null, new object[] { card });
	}

	private static string ContentField(object content, string fieldName)
	{
		return (string)ContentStructType.GetField(fieldName).GetValue(content);
	}

	private object MakeContent(string mainText, string cardRefText)
	{
		object content = Activator.CreateInstance(ContentStructType);
		ContentStructType.GetField("MainText").SetValue(content, mainText);
		ContentStructType.GetField("CardRefText").SetValue(content, cardRefText);
		return content;
	}

	private void ApplyContent(object content)
	{
		typeof(CardTagTooltip).GetMethod("ApplyContent", InstanceFlags).Invoke(_tooltip, new[] { content });
	}

	/// <summary>
	/// Test-side mirror of the production type/tag block format: bold "[name]" title,
	/// newline, description, blank line between blocks; card-type block first, then the
	/// card's myTags in order.
	/// </summary>
	private static string BuildExpectedMainText(CardScript card)
	{
		var sb = new StringBuilder();
		CardTypeTooltipDatabaseSO typeDb = CardTypeTooltipDatabaseSO.Me;
		Assert.IsNotNull(typeDb, "CardTypeTooltipDatabaseSO.Me");
		string typeName = typeDb.GetDisplayName(card.cardType);
		if (!string.IsNullOrEmpty(typeName))
		{
			sb.Append("<b>[").Append(typeName).Append("]</b>");
			StringSO typeDescription = typeDb.GetDescription(card.cardType);
			if (typeDescription != null && !string.IsNullOrEmpty(typeDescription.value))
			{
				sb.Append("\n").Append(typeDescription.value);
			}
		}
		TagTooltipDatabaseSO tagDb = TagTooltipDatabaseSO.Me;
		if (card.myTags != null)
		{
			for (int i = 0; i < card.myTags.Count; i++)
			{
				EnumStorage.Tag tag = card.myTags[i];
				if (tag == EnumStorage.Tag.None) continue;
				if (sb.Length > 0) sb.Append("\n\n");
				sb.Append("<b>[").Append(TagTooltipDatabaseSO.GetTagDisplayName(tag)).Append("]</b>");
				StringSO tagDescription = tagDb != null ? tagDb.GetDescription(tag) : null;
				if (tagDescription != null && !string.IsNullOrEmpty(tagDescription.value))
				{
					sb.Append("\n").Append(tagDescription.value);
				}
			}
		}
		return sb.ToString();
	}

	/// <summary>
	/// True when ANY serialized object reference on ANY component of the prefab (root
	/// and children) points at one of the targets (directly, or at any object inside
	/// the target GameObject's hierarchy). References that stay inside the scanned
	/// prefab itself are ignored.
	/// </summary>
	private static bool PrefabReferencesAnyTarget(GameObject prefabRoot, UnityEngine.Object[] targets)
	{
		foreach (Component component in prefabRoot.GetComponentsInChildren<Component>(true))
		{
			if (component == null) continue;
			var serialized = new SerializedObject(component);
			var iterator = serialized.GetIterator();
			while (iterator.Next(true))
			{
				if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
				UnityEngine.Object value = iterator.objectReferenceValue;
				if (value == null) continue;
				// Ignore references that stay inside the scanned prefab itself.
				GameObject valueRoot = null;
				if (value is GameObject valueGo) valueRoot = valueGo.transform.root.gameObject;
				else if (value is Component valueComp) valueRoot = valueComp.transform.root.gameObject;
				if (valueRoot == prefabRoot) continue;
				foreach (UnityEngine.Object target in targets)
				{
					if (value == target) return true;
					if (valueRoot != null && valueRoot == target) return true;
				}
			}
		}
		return false;
	}

	private UnityEngine.Object[] WiringTargets()
	{
		GameObject riftPrefab = CardTooltipDatabaseSO.Me.entries[0].cardPrefab;
		ScriptableObject riftTypeId = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/SORefs/CombatRefs/RiftCardTypeID.asset");
		Assert.IsNotNull(riftPrefab, "RIFT prefab from the database entry");
		Assert.IsNotNull(riftTypeId, "RiftCardTypeID asset");
		return new UnityEngine.Object[] { riftPrefab, riftTypeId };
	}

	// ---------- 1-3: the pure resolution rule ----------

	[Test]
	public void DescReferencesCard_BareName_Matches()
	{
		Assert.IsTrue(CardRefTooltipBlocks.DescReferencesCard("生成<b>1</b>信徒", BelieverName), "plain bare name");
		Assert.IsTrue(CardRefTooltipBlocks.DescReferencesCard("被动:友方信徒效果变为:复活<b>1</b>敌方诅咒;放逐自身", BelieverName), "bare name without brackets");
		Assert.IsTrue(CardRefTooltipBlocks.DescReferencesCard("放逐所有信徒,每放逐<b>1</b>,强化<b>1</b>友方实体", BelieverName), "bare name inside a clause");
		Assert.IsTrue(CardRefTooltipBlocks.DescReferencesCard("强化<b>1</b>敌方诅咒", CurseName), "curse bare name inside the 敌方诅咒 phrase");
	}

	[Test]
	public void DescReferencesCard_TagPhraseOrMarkup_NotAMatch()
	{
		Assert.IsFalse(CardRefTooltipBlocks.DescReferencesCard("攻击;每回合一次,复活<b>1</b>张tag为[<tag:Believer>]的友方卡", BelieverName),
			"[<tag:Believer>] is a tag phrase, not a card reference");
		Assert.IsFalse(CardRefTooltipBlocks.DescReferencesCard("复活1张tag为[信徒]的友方卡", BelieverName),
			"[信徒] is a bracketed tag phrase");
		Assert.IsFalse(CardRefTooltipBlocks.DescReferencesCard("攻击", BelieverName), "no reference at all");
		Assert.IsFalse(CardRefTooltipBlocks.DescReferencesCard("复活1张tag为[诅咒]的友方卡", CurseName), "[诅咒] is a bracketed tag phrase");
	}

	[Test]
	public void DescReferencesCard_NullOrEmpty_ReturnsFalse()
	{
		Assert.IsFalse(CardRefTooltipBlocks.DescReferencesCard(null, BelieverName));
		Assert.IsFalse(CardRefTooltipBlocks.DescReferencesCard(string.Empty, BelieverName));
		Assert.IsFalse(CardRefTooltipBlocks.DescReferencesCard("生成信徒", null));
		Assert.IsFalse(CardRefTooltipBlocks.DescReferencesCard("生成信徒", string.Empty));
	}

	// ---------- 4-6: database + text builders ----------

	[Test]
	public void Database_Singleton_LoadsRiftAndCurseEntries()
	{
		CardTooltipDatabaseSO db = CardTooltipDatabaseSO.Me;
		Assert.IsNotNull(db, "CardTooltipDatabase asset must load from Resources");
		Assert.AreEqual(2, db.entries.Count, "two entries: the RIFT believer token and the JU_ON curse token");
		CardTooltipDatabaseSO.Entry riftEntry = db.entries[0];
		Assert.IsNotNull(riftEntry.cardPrefab, "believer entry cardPrefab");
		CardScript rift = riftEntry.cardPrefab.GetComponent<CardScript>();
		Assert.IsNotNull(rift, "believer entry prefab root CardScript");
		Assert.AreEqual("RIFT", rift.cardTypeID);
		Assert.AreEqual(BelieverName, rift.GetDisplayName());
		Assert.IsNotEmpty(rift.cardDesc, "the referenced card must have effect text");
		Assert.IsNull(riftEntry.descriptionOverride, "no override configured");
		CardTooltipDatabaseSO.Entry curseEntry = db.entries[1];
		Assert.IsNotNull(curseEntry.cardPrefab, "curse entry cardPrefab");
		CardScript juon = curseEntry.cardPrefab.GetComponent<CardScript>();
		Assert.IsNotNull(juon, "curse entry prefab root CardScript");
		Assert.AreEqual("JU_ON", juon.cardTypeID);
		Assert.AreEqual(CurseName, juon.GetDisplayName());
		Assert.AreEqual(EnumStorage.CardType.Token, juon.cardType, "the curse token is a Token card");
		Assert.IsNotEmpty(juon.cardDesc, "the referenced card must have effect text");
		Assert.IsNull(curseEntry.descriptionOverride, "no override configured");
	}

	[Test]
	public void BuildCardRefText_HitCards_MatchReferencedCardDesc()
	{
		string golden = GoldenCardRefText();
		string curseGolden = GoldenCurseCardRefText();
		Assert.AreEqual(golden, CardRefTooltipBlocks.BuildCardRefText(LoadCardScript(RelicHivePath)), "RELIC_HIVE");
		Assert.AreEqual(golden, CardRefTooltipBlocks.BuildCardRefText(LoadCardScript(RiftReaperPath)), "RIFT_REAPER");
		Assert.AreEqual(curseGolden, CardRefTooltipBlocks.BuildCardRefText(LoadCardScript(CurseGardenerPath)), "CURSE_GARDENER");
		Assert.AreEqual(curseGolden, CardRefTooltipBlocks.BuildCardRefText(LoadCardScript(CurseEaterPath)), "CURSE_EATER");
		Assert.IsEmpty(CardRefTooltipBlocks.BuildCardRefText(LoadCardScript(RiftShepherdPath)), "RIFT_SHEPHERD is a tag reference only");
		Assert.IsEmpty(CardRefTooltipBlocks.BuildCardRefText(RiftScript()), "the RIFT token itself is not a hit");
		Assert.IsEmpty(CardRefTooltipBlocks.BuildCardRefText(JuOnScript()), "the JU_ON token itself is not a hit");
	}

	[Test]
	public void BuildCardRefText_TransientDatabase_StyleOverrides()
	{
		CardScript rift = RiftScript();
		CardScript hive = LoadCardScript(RelicHivePath);

		// titleFormat wraps the bare display name.
		var formatDb = ScriptableObject.CreateInstance<CardTooltipDatabaseSO>();
		_transient.Add(formatDb);
		formatDb.entries.Add(new CardTooltipDatabaseSO.Entry { cardPrefab = rift.gameObject });
		var titleFormat = ScriptableObject.CreateInstance<StringSO>();
		_transient.Add(titleFormat);
		titleFormat.value = "[{0}]"; // set AFTER CreateInstance: StringSO.OnEnable wipes when reset = true
		formatDb.cardBlock.titleFormat = titleFormat;
		Assert.AreEqual("<b>[信徒]</b>\n" + rift.cardDesc, CardRefTooltipBlocks.BuildCardRefText(hive, formatDb), "titleFormat");
		Assert.AreEqual(BelieverName, CardRefTooltipBlocks.GetTitle(null, formatDb.entries[0]), "null db still yields the bare name");

		// bodyColor wraps the body text.
		var colorDb = ScriptableObject.CreateInstance<CardTooltipDatabaseSO>();
		_transient.Add(colorDb);
		colorDb.entries.Add(new CardTooltipDatabaseSO.Entry { cardPrefab = rift.gameObject });
		var bodyColor = ScriptableObject.CreateInstance<ColorSO>();
		_transient.Add(bodyColor);
		bodyColor.value = new Color(1f, 0f, 0f, 1f);
		colorDb.cardBlock.bodyColor = bodyColor;
		Assert.AreEqual("<b>" + BelieverName + "</b>\n" + bodyColor.OpenTag + rift.cardDesc + "</color>",
			CardRefTooltipBlocks.BuildCardRefText(hive, colorDb), "bodyColor");

		// descriptionOverride replaces the body.
		colorDb.entries[0].descriptionOverride = ScriptableObject.CreateInstance<StringSO>();
		_transient.Add(colorDb.entries[0].descriptionOverride);
		colorDb.entries[0].descriptionOverride.value = "override";
		Assert.AreEqual("<b>" + BelieverName + "</b>\n" + bodyColor.OpenTag + "override</color>",
			CardRefTooltipBlocks.BuildCardRefText(hive, colorDb), "descriptionOverride");

		// Null guards: Resolve with null card / null db, and an entry with null cardPrefab.
		Assert.IsEmpty(CardRefTooltipBlocks.Resolve(null, colorDb), "null card");
		Assert.IsEmpty(CardRefTooltipBlocks.Resolve(hive, null), "null db");
		var brokenDb = ScriptableObject.CreateInstance<CardTooltipDatabaseSO>();
		_transient.Add(brokenDb);
		brokenDb.entries.Add(new CardTooltipDatabaseSO.Entry { cardPrefab = null });
		Assert.IsEmpty(CardRefTooltipBlocks.Resolve(hive, brokenDb), "null cardPrefab entry skipped");
		Assert.IsEmpty(CardRefTooltipBlocks.BuildCardRefText(hive, brokenDb), "null cardPrefab entry yields no block");
	}

	// ---------- 7-9: gold + wiring over the real 4.0 pool ----------

	[Test]
	public void Resolve_CardPool_ExactHitSet()
	{
		var expected = new SortedSet<string>(RiftRefHitIds.Concat(JuOnRefHitIds));
		var actual = new SortedSet<string>();
		foreach (GameObject prefab in LoadCardPool())
		{
			var card = prefab.GetComponent<CardScript>();
			if (CardRefTooltipBlocks.Resolve(card, CardTooltipDatabaseSO.Me).Count > 0)
			{
				actual.Add(card.cardTypeID);
			}
		}
		List<string> missing = expected.Except(actual).ToList();
		List<string> unexpected = actual.Except(expected).ToList();
		Assert.IsTrue(missing.Count == 0 && unexpected.Count == 0,
			"hit set mismatch.\nmissing (" + missing.Count + "): " + string.Join(", ", missing)
			+ "\nunexpected (" + unexpected.Count + "): " + string.Join(", ", unexpected));
	}

	[Test]
	public void RIFT_SHEPHERD_TagReferenceOnly_NotAHit()
	{
		CardScript shepherd = LoadCardScript(RiftShepherdPath);
		Assert.AreEqual("RIFT_SHEPHERD", shepherd.cardTypeID);
		Assert.IsEmpty(CardRefTooltipBlocks.Resolve(shepherd, CardTooltipDatabaseSO.Me),
			"RIFT_SHEPHERD references the Believer TAG (<tag:Believer> phrase), not the believer card by bare name");
	}

	[Test]
	public void WiringGuard_OnlyHitCardsReferenceRiftToken()
	{
		UnityEngine.Object[] targets = WiringTargets();
		var hitIds = new HashSet<string>(RiftRefHitIds);
		foreach (GameObject prefab in LoadCardPool())
		{
			if (!PrefabReferencesAnyTarget(prefab, targets)) continue;
			string id = prefab.GetComponent<CardScript>().cardTypeID;
			Assert.IsTrue(hitIds.Contains(id),
				"card " + id + " serializes a reference to the RIFT token objects but is not a card-reference hit");
		}
	}

	[Test]
	public void WiringGuard_DetectsBothReferenceKinds()
	{
		UnityEngine.Object[] targets = WiringTargets();
		Assert.IsTrue(PrefabReferencesAnyTarget(LoadPrefab(MassSacrificePath), targets),
			"MASS_SACRIFICE must be detected via the direct riftToken serialized field");
		Assert.IsTrue(PrefabReferencesAnyTarget(LoadPrefab(RiftAcolytePath), targets),
			"RIFT_ACOLYTE must be detected via the UnityEvent m_ObjectArgument persistent call");
		Assert.IsFalse(PrefabReferencesAnyTarget(LoadPrefab(RelicRiftOverridePath), targets),
			"RELIC_RIFT_OVERRIDE describes the token but wires no token reference");
	}

	[Test]
	public void Resolve_CardPool_CurseHitSet_MatchesJuOnEntry()
	{
		CardTooltipDatabaseSO db = CardTooltipDatabaseSO.Me;
		string juonTypeId = JuOnScript().cardTypeID;
		Assert.AreEqual("JU_ON", juonTypeId, "database entry 1 must be the JU_ON curse token");
		var expected = new SortedSet<string>(JuOnRefHitIds);
		var actual = new SortedSet<string>();
		foreach (GameObject prefab in LoadCardPool())
		{
			var card = prefab.GetComponent<CardScript>();
			foreach (CardTooltipDatabaseSO.Entry entry in CardRefTooltipBlocks.Resolve(card, db))
			{
				if (entry.cardPrefab.GetComponent<CardScript>().cardTypeID == juonTypeId)
				{
					actual.Add(card.cardTypeID);
				}
			}
		}
		List<string> missing = expected.Except(actual).ToList();
		List<string> unexpected = actual.Except(expected).ToList();
		Assert.IsTrue(missing.Count == 0 && unexpected.Count == 0,
			"curse hit set mismatch.\nmissing (" + missing.Count + "): " + string.Join(", ", missing)
				+ "\nunexpected (" + unexpected.Count + "): " + string.Join(", ", unexpected));
	}

	[Test]
	public void WiringGuard_OnlyHitCardsReferenceJuOnPrefab()
	{
		GameObject juonPrefab = CardTooltipDatabaseSO.Me.entries[1].cardPrefab;
		Assert.IsNotNull(juonPrefab, "JU_ON prefab from the database entry");
		// CurseCardTypeID.asset is deliberately NOT a wiring target here: a default-valued
		// cursedCardTypeID field pointing at it is serialized on most pool cards, so the
		// asset's presence proves nothing about curse wiring.
		var targets = new UnityEngine.Object[] { juonPrefab };
		var hitIds = new HashSet<string>(JuOnRefHitIds);
		foreach (GameObject prefab in LoadCardPool())
		{
			if (!PrefabReferencesAnyTarget(prefab, targets)) continue;
			string id = prefab.GetComponent<CardScript>().cardTypeID;
			Assert.IsTrue(hitIds.Contains(id),
				"card " + id + " serializes a reference to the JU_ON token prefab but is not a card-reference hit");
		}
	}

	[Test]
	public void WiringGuard_CurseInlineCardTypeIdStringIsHit()
	{
		var hitIds = new HashSet<string>(JuOnRefHitIds);
		Assert.IsTrue(PrefabHasInlineCardTypeIdValue(LoadPrefab(CurseEaterPath), "JU_ON"),
			"CURSE_EATER must carry the inline cardTypeID: JU_ON wiring string");
		Assert.IsFalse(PrefabHasInlineCardTypeIdValue(LoadPrefab(MassSacrificePath), "JU_ON"),
			"a non-curse card must not carry the inline JU_ON string");
		foreach (GameObject prefab in LoadCardPool())
		{
			if (!PrefabHasInlineCardTypeIdValue(prefab, "JU_ON")) continue;
			string id = prefab.GetComponent<CardScript>().cardTypeID;
			Assert.IsTrue(hitIds.Contains(id),
				"card " + id + " carries the inline cardTypeID: JU_ON string but is not a card-reference hit");
		}
	}

	// ---------- 10-11: TooltipContent composition ----------

	[Test]
	public void TooltipContent_CardRefTextMatchesComposition_AllPool()
	{
		var hitIds = new HashSet<string>(RiftRefHitIds.Concat(JuOnRefHitIds));
		foreach (GameObject prefab in LoadCardPool())
		{
			var card = prefab.GetComponent<CardScript>();
			string actual = ContentField(BuildContent(card), "CardRefText");
			Assert.AreEqual(ExpectedCardRefText(card), actual, card.cardTypeID + " card-ref text composition");
			if (hitIds.Contains(card.cardTypeID))
			{
				Assert.IsNotEmpty(actual, card.cardTypeID + " must show a card-reference block");
			}
			else
			{
				Assert.IsEmpty(actual, card.cardTypeID + " must keep its tooltip unchanged");
			}
		}
	}

	[Test]
	public void TooltipContent_DualReferenceCards_GetBothBlocksBelieverFirst()
	{
		string dual = GoldenCardRefText() + "\n\n" + GoldenCurseCardRefText();
		Assert.AreEqual(dual, CardRefTooltipBlocks.BuildCardRefText(LoadCardScript(SwarmCurserPath)),
			"SWARM_CURSER (诅咒唱诗班) names both tokens; the believer block (database order) comes first");
		Assert.AreEqual(dual, CardRefTooltipBlocks.BuildCardRefText(LoadCardScript(RelicRiftOverridePath)),
			"RELIC_RIFT_OVERRIDE (伪经) names both tokens; the believer block (database order) comes first");
	}

	[Test]
	public void TooltipContent_RELIC_HIVE_MainTextGolden_RiftTokenNoTooltip()
	{
		CardScript hive = LoadCardScript(RelicHivePath);
		object hiveContent = BuildContent(hive);
		Assert.AreEqual(BuildExpectedMainText(hive), ContentField(hiveContent, "MainText"), "RELIC_HIVE main text");
		Assert.IsNotEmpty(ContentField(hiveContent, "CardRefText"), "RELIC_HIVE card-ref text");

		CardScript rift = RiftScript();
		object tokenContent = BuildContent(rift);
		Assert.IsEmpty(ContentField(tokenContent, "MainText"),
			"the RIFT token (cardType Token, no tags) has no type block and no tag blocks");
		Assert.IsEmpty(ContentField(tokenContent, "CardRefText"), "the RIFT token does not reference itself");
	}

	// ---------- 13-17: real two-label layout ----------

	[Test]
	public void ApplyContent_BothTexts_ShowsBothLabelsAndPositiveGap()
	{
		const string main = "<b>[现象]</b>\n不可攻击";
		const string cardRefs = "<b>信徒</b>\n复活 1 友方,去除自身";
		ApplyContent(MakeContent(main, cardRefs));
		Assert.IsTrue(_text.gameObject.activeSelf, "main label active");
		Assert.AreEqual(main, _text.text, "main label text");
		Assert.IsTrue(_cardRefsText.gameObject.activeSelf, "cardRefs label active");
		Assert.AreEqual(cardRefs, _cardRefsText.text, "cardRefs label text");
		Assert.Greater(_layout.spacing, 0f, "gap = one blank line of the main label (+ spacingBefore)");
	}

	[Test]
	public void ApplyContent_MainOnly_CardRefsInactiveAndZeroGap()
	{
		const string main = "<b>[现象]</b>\n不可攻击";
		ApplyContent(MakeContent(main, string.Empty));
		Assert.IsTrue(_text.gameObject.activeSelf, "main label active");
		Assert.AreEqual(main, _text.text, "main label text");
		Assert.IsFalse(_cardRefsText.gameObject.activeSelf, "cardRefs label inactive without card-ref text");
		Assert.AreEqual(0f, _layout.spacing, "no extra gap without the card-ref block");
	}

	[Test]
	public void ApplyContent_CardRefsOnly_MainInactiveAndZeroGap()
	{
		const string cardRefs = "<b>信徒</b>\n复活 1 友方,去除自身";
		ApplyContent(MakeContent(string.Empty, cardRefs));
		Assert.IsFalse(_text.gameObject.activeSelf, "main label inactive without main text");
		Assert.IsTrue(_cardRefsText.gameObject.activeSelf, "cardRefs label active");
		Assert.AreEqual(cardRefs, _cardRefsText.text, "cardRefs label text");
		Assert.AreEqual(0f, _layout.spacing, "no gap when the main label is absent");
	}

	/// <summary>
	/// Regression (GetPreferredValues(string) measures in place): the second ApplyContent of
	/// the SAME content must re-measure the gap probe and then reset the label to the real
	/// text via SetText — the text setter would early-out on an unchanged string and leave
	/// the probe string in the internal arrays, one line too tall.
	/// </summary>
	[Test]
	public void Layout_SameContentAppliedTwice_MainLabelStillMeasuredFromRealText()
	{
		const string main = "<b>[现象]</b>\n不可攻击\n\n<b>[信徒]</b>\n生成或增益[信徒]的效果";
		const string cardRefs = "<b>信徒</b>\n复活 1 友方,去除自身";
		ApplyContent(MakeContent(main, cardRefs));
		_panel.gameObject.SetActive(true);
		try
		{
			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
			float h1 = _text.preferredHeight;
			Assert.Greater(h1, 0f, "main label must have a positive preferred height (measured " + h1 + ")");
			_text.SetLayoutDirty();
			ApplyContent(MakeContent(main, cardRefs));
			float h2 = _text.preferredHeight;
			Assert.AreEqual(h1, h2, 0.01f,
				"second ApplyContent of the same content must measure from the real text (h1=" + h1 + " h2=" + h2 + ")");
		}
		finally
		{
			_panel.gameObject.SetActive(false);
		}
	}

	[Test]
	public void ApplyContent_RectCheck_CardRefsBelowMainLabelSharedLeftEdge()
	{
		ApplyContent(MakeContent("<b>[现象]</b>\n不可攻击", "<b>信徒</b>\n复活 1 友方,去除自身"));
		_panel.gameObject.SetActive(true);
		try
		{
			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
			_text.ForceMeshUpdate();
			_cardRefsText.ForceMeshUpdate();

			var textCorners = new Vector3[4];
			_text.rectTransform.GetWorldCorners(textCorners);
			var refsCorners = new Vector3[4];
			_cardRefsText.rectTransform.GetWorldCorners(refsCorners);
			Vector2 textSize = _text.rectTransform.rect.size;
			Vector2 refsSize = _cardRefsText.rectTransform.rect.size;
			float textBottom = Mathf.Min(Mathf.Min(textCorners[0].y, textCorners[1].y), Mathf.Min(textCorners[2].y, textCorners[3].y));
			float refsTop = Mathf.Max(Mathf.Max(refsCorners[0].y, refsCorners[1].y), Mathf.Max(refsCorners[2].y, refsCorners[3].y));
			Assert.Greater(textSize.x * textSize.y, 0f, "main label must get a non-zero rect (measured " + textSize + ")");
			Assert.Greater(refsSize.x * refsSize.y, 0f, "cardRefs label must get a non-zero rect (measured " + refsSize + ")");
			Assert.Less(refsTop, textBottom,
				"cardRefs must sit below the main label (measured refsTop=" + refsTop + " textBottom=" + textBottom + ")");
			Assert.AreEqual(textCorners[0].x, refsCorners[0].x, 0.5f,
				"cardRefs must share the main label's left edge (measured textLeft=" + textCorners[0].x + " refsLeft=" + refsCorners[0].x + ")");
		}
		finally
		{
			_panel.gameObject.SetActive(false);
		}
	}
}
