# -*- coding: utf-8 -*-
"""Patch AttackResolverSourceTests to bind the resolver explicitly via
RefreshAttackResolver (OnEnable does not fire for runtime-added components in EditMode)."""
import io

path = "Assets/Scripts/Editor/Tests/AttackResolverSourceTests.cs"
with io.open(path, "r", encoding="utf-8", newline="") as f:
    text = f.read()
crlf = "\r\n" in text
text = text.replace("\r\n", "\n")

PATCHES = [
    # FriendlyCardTotal_SumsFriendlyAttackOnly
    ('AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);\n\n\t\tvar cs = card.GetComponent<CardScript>();\n\t\tAssert.AreEqual(5,',
     'AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);\n\t\tresolver.RefreshAttackResolver();\n\n\t\tvar cs = card.GetComponent<CardScript>();\n\t\tAssert.AreEqual(5,'),
    # FriendlyCardTotal_IsRelativeToResolverFaction
    ('AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);\n\n\t\tAssert.AreEqual(7,',
     'AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);\n\t\tresolver.RefreshAttackResolver();\n\n\t\tAssert.AreEqual(7,'),
    # FriendlyCardCount
    ('AddTerm(resolver, AttackResolverSource.Source.FriendlyCardCount);\n\n\t\tAssert.AreEqual(2,',
     'AddTerm(resolver, AttackResolverSource.Source.FriendlyCardCount);\n\t\tresolver.RefreshAttackResolver();\n\n\t\tAssert.AreEqual(2,'),
    # GraveyardFriendlyCount
    ('AddTerm(resolver, AttackResolverSource.Source.GraveyardFriendlyCount);\n\n\t\tAssert.AreEqual(1,',
     'AddTerm(resolver, AttackResolverSource.Source.GraveyardFriendlyCount);\n\t\tresolver.RefreshAttackResolver();\n\n\t\tAssert.AreEqual(1,'),
    # FriendlyRiftCount
    ('AddTerm(resolver, AttackResolverSource.Source.FriendlyRiftCount, "RIFT");\n\n\t\tAssert.AreEqual(2,',
     'AddTerm(resolver, AttackResolverSource.Source.FriendlyRiftCount, "RIFT");\n\t\tresolver.RefreshAttackResolver();\n\n\t\tAssert.AreEqual(2,'),
    # EnemyNegativeTotal
    ('AddTerm(resolver, AttackResolverSource.Source.EnemyNegativeTotal, "JU_ON");\n\n\t\tAssert.AreEqual(5, card.GetComponent<CardScript>().GetAttack(), "Enemy JU_ON attack sum");',
     'AddTerm(resolver, AttackResolverSource.Source.EnemyNegativeTotal, "JU_ON");\n\t\tresolver.RefreshAttackResolver();\n\n\t\tAssert.AreEqual(5, card.GetComponent<CardScript>().GetAttack(), "Enemy JU_ON attack sum");'),
    # EnemyNegativeHighest
    ('AddTerm(resolver, AttackResolverSource.Source.EnemyNegativeHighest, "JU_ON");\n\n\t\tAssert.AreEqual(7,',
     'AddTerm(resolver, AttackResolverSource.Source.EnemyNegativeHighest, "JU_ON");\n\t\tresolver.RefreshAttackResolver();\n\n\t\tAssert.AreEqual(7,'),
    # MultiTermResolver
    ('AddTerm(resolver, AttackResolverSource.Source.EnemyNegativeTotal, "JU_ON");\n\n\t\tAssert.AreEqual(5, card.GetComponent<CardScript>().GetAttack(), "1 grave',
     'AddTerm(resolver, AttackResolverSource.Source.EnemyNegativeTotal, "JU_ON");\n\t\tresolver.RefreshAttackResolver();\n\n\t\tAssert.AreEqual(5, card.GetComponent<CardScript>().GetAttack(), "1 grave'),
    # ResolverReadsLiveChanges
    ('AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);\n\n\t\tvar cs = card.GetComponent<CardScript>();\n\t\tAssert.AreEqual(2, cs.GetAttack(), "Initial sum");',
     'AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);\n\t\tresolver.RefreshAttackResolver();\n\n\t\tvar cs = card.GetComponent<CardScript>();\n\t\tAssert.AreEqual(2, cs.GetAttack(), "Initial sum");'),
    # DisablingResolver test -> ClearAttackResolver
    ('AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);\n\n\t\tvar cs = card.GetComponent<CardScript>();\n\t\tAssert.AreEqual(2, cs.GetAttack(), "Resolver active");\n\n\t\tresolver.enabled = false;\n\t\tAssert.AreEqual(1, cs.GetAttack(), "Resolver removed -> base attack");\n\n\t\tresolver.enabled = true;\n\t\tAssert.AreEqual(2, cs.GetAttack(), "Resolver restored");',
     'AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);\n\t\tresolver.RefreshAttackResolver();\n\n\t\tvar cs = card.GetComponent<CardScript>();\n\t\tAssert.AreEqual(2, cs.GetAttack(), "Resolver active");\n\n\t\t// Simulates OnDisable (lifecycle callbacks do not fire for runtime-added components in EditMode).\n\t\tresolver.ClearAttackResolver();\n\t\tAssert.AreEqual(1, cs.GetAttack(), "Resolver removed -> base attack");\n\n\t\tresolver.RefreshAttackResolver();\n\t\tAssert.AreEqual(2, cs.GetAttack(), "Resolver restored");'),
    # Rename the disable test
    ('public void DisablingResolver_RestoresBaseAttack()',
     'public void ClearAttackResolver_RestoresBaseAttack()'),
]

for old, new in PATCHES:
    count = text.count(old)
    assert count == 1, "expected 1 occurrence of %r, found %d" % (old[:60], count)
    text = text.replace(old, new)

if crlf:
    text = text.replace("\n", "\r\n")
with io.open(path, "w", encoding="utf-8", newline="") as f:
    f.write(text)
print("AttackResolverSourceTests patched: %d edits" % len(PATCHES))
