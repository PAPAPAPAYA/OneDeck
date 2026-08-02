var sb = new System.Text.StringBuilder();
var all = UnityEngine.Object.FindObjectsOfType<CardPhysObjScript>(true);
CardPhysObjScript start = null;
foreach (var c in all) { if (c.isPhysicalStartCard) { start = c; break; } }
sb.AppendLine("physScriptCount=" + all.Length);
if (start == null) return sb.ToString() + "NO START CARD FOUND";
sb.AppendLine("startName=" + start.name);
sb.AppendLine("activeInHierarchy=" + start.gameObject.activeInHierarchy);
sb.AppendLine("isFaceUp=" + start.isFaceUp);
sb.AppendLine("everRevealed=" + start.everRevealed);
sb.AppendLine("cardImRepresenting=" + (start.cardImRepresenting == null ? "NULL" : start.cardImRepresenting.name));
sb.AppendLine("currentGamePhaseRef=" + (start.currentGamePhaseRef == null ? "NULL" : "SET"));
sb.AppendLine("pos=" + start.transform.position.ToString("F3"));
var col = start.GetComponent<Collider2D>();
sb.AppendLine("collider=" + (col == null ? "NULL" : col.GetType().Name + " enabled=" + col.enabled + " bounds=" + col.bounds.ToString("F3")));
var ux = CombatUXManager.me;
sb.AppendLine("ux=" + (ux == null ? "NULL" : "ok"));
if (ux != null)
{
    var rz = ux.physicalCardInRevealZone;
    sb.AppendLine("revealZoneCard=" + (rz == null ? "NULL" : rz.name));
    sb.AppendLine("startIsRevealZone=" + (rz == start.gameObject));
    sb.AppendLine("deckIndexOfStart=" + ux.physicalCardsInDeck.IndexOf(start.gameObject));
    sb.AppendLine("deckCount=" + ux.physicalCardsInDeck.Count);
    sb.AppendLine("popUpSlotInBlockCount=" + ux.PopUpSlotInInputBlockCount);
}
var cm = CombatManager.Me;
if (cm != null)
{
    sb.AppendLine("isPlayingEffectAnimations=" + cm.isPlayingEffectAnimations);
    sb.AppendLine("IsInputBlocked=" + cm.IsInputBlocked);
    sb.AppendLine("InputBlockCount=" + cm.InputBlockCount);
    sb.AppendLine("autoReveal=" + cm.autoReveal);
}
var t = typeof(CardPhysObjScript);
var fOwner = t.GetField("_currentHoverOwner", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
var owner = fOwner.GetValue(null) as CardPhysObjScript;
sb.AppendLine("hoverOwner=" + (owner == null ? "NULL" : owner.name + " z=" + owner.transform.position.z.ToString("F3")));
var fActive = t.GetField("_hoverActive", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var fPending = t.GetField("_hoverPending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var fPopped = t.GetField("_hoverPoppedUp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
sb.AppendLine("start hoverActive=" + fActive.GetValue(start) + " pending=" + fPending.GetValue(start) + " poppedUp=" + fPopped.GetValue(start));
if (start.cardImRepresenting != null && ux != null)
{
    var phys = ux.GetPhysicalCardFromLogicalCard(start.cardImRepresenting);
    sb.AppendLine("cacheResolve=" + (phys == null ? "NULL(CACHE MISS)" : phys.name + " sameAsStart=" + (phys == start.gameObject)));
}
var gp = EnumStorage.GamePhase.Combat;
sb.AppendLine("phaseRefValue=" + (start.currentGamePhaseRef == null ? "NULL" : start.currentGamePhaseRef.Value().ToString()));
return sb.ToString();
