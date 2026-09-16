using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the deterministic RNG service (plans/plan-deterministic-rng-seed-2026-09-12.md):
/// same seed = same sequence, channel isolation (Deck consumption must not shift Target),
/// Fisher-Yates permutation preservation, Gaussian determinism, and FNV-1a digest properties.
/// </summary>
public class RngDeterminismTests
{
	[TearDown]
	public void TearDown()
	{
		// Leave a fresh random run behind so later tests never inherit a fixed-seed stream
		Rng.NewRun();
	}

	[Test]
	public void InitCombat_SameSeed_ProducesIdenticalShuffleSequences()
	{
		var first = MakeDeck(32);
		var second = MakeDeck(32);

		Rng.InitCombat(12345);
		Rng.Shuffle(RngChannel.Deck, first);

		Rng.InitCombat(12345);
		Rng.Shuffle(RngChannel.Deck, second);

		CollectionAssert.AreEqual(first, second, "Same seed + same input must produce the same deck order.");
	}

	[Test]
	public void InitCombat_DifferentSeed_ProducesDifferentShuffle()
	{
		var first = MakeDeck(32);
		var second = MakeDeck(32);

		Rng.InitCombat(12345);
		Rng.Shuffle(RngChannel.Deck, first);

		Rng.InitCombat(54321);
		Rng.Shuffle(RngChannel.Deck, second);

		CollectionAssert.AreNotEqual(first, second, "Different seeds are expected to diverge on a 32-card deck.");
	}

	[Test]
	public void DeckChannelConsumption_DoesNotShiftTargetChannel()
	{
		var targetA = MakeDeck(16);
		var targetB = MakeDeck(16);
		var deckNoise = MakeDeck(16);

		Rng.InitCombat(777);
		var expectedTarget = Rng.Shuffle(RngChannel.Target, new List<int>(targetA));

		// Re-seed, burn a different number of Deck draws (simulating a future extra shuffle), then draw Target
		Rng.InitCombat(777);
		for (int i = 0; i < deckNoise.Count; i++)
		{
			Rng.Next(RngChannel.Deck, deckNoise.Count);
		}
		var actualTarget = Rng.Shuffle(RngChannel.Target, targetB);

		CollectionAssert.AreEqual(expectedTarget, actualTarget,
			"Extra consumption on the Deck channel must never change the Target channel sequence.");
	}

	[Test]
	public void Shuffle_PreservesElements()
	{
		var deck = MakeDeck(64);
		var original = new List<int>(deck);

		Rng.InitCombat(42);
		Rng.Shuffle(RngChannel.Deck, deck);
		deck.Sort();

		CollectionAssert.AreEqual(original, deck, "Fisher-Yates must be a permutation, not a re-deal.");
	}

	[Test]
	public void Gaussian_SameSeed_SameValue()
	{
		Rng.InitCombat(99);
		float first = Rng.Gaussian(RngChannel.Deck, 10f, 2f);

		Rng.InitCombat(99);
		float second = Rng.Gaussian(RngChannel.Deck, 10f, 2f);

		Assert.AreEqual(first, second, 0.0001f, "Gaussian must be reproducible under the same seed.");
	}

	[Test]
	public void ComputeCombatSeed_SameRunSeed_SameSession_SameSeed()
	{
		Rng.NewRun();
		Rng.NewRun(); // re-roll run seed to a concrete value
		int runSeed = Rng.RunSeed;
		int sessionA = Rng.ComputeCombatSeed(3);
		int sessionB = Rng.ComputeCombatSeed(3);

		Assert.AreEqual(runSeed, Rng.RunSeed, "ComputeCombatSeed must not re-roll the run seed.");
		Assert.AreEqual(sessionA, sessionB, "Same run seed + same session must derive the same combat seed.");
	}

	[Test]
	public void Digest_SameInputOrder_SameHash_DifferentOrder_DifferentHash()
	{
		uint baseHash = RngDigest.OffsetBasis;
		uint hashA = RngDigest.Fnv1a(RngDigest.Fnv1a(baseHash, "CARD_A"), 1);
		uint hashB = RngDigest.Fnv1a(RngDigest.Fnv1a(baseHash, "CARD_A"), 1);
		uint hashC = RngDigest.Fnv1a(RngDigest.Fnv1a(baseHash, 1), "CARD_A");

		Assert.AreEqual(hashA, hashB, "Same input sequence must hash identically.");
		Assert.AreNotEqual(hashA, hashC, "Digest must be order-sensitive: swapped operands must diverge.");
	}

	[Test]
	public void StableHash_CombinesBothOperands()
	{
		Assert.AreEqual(Rng.StableHash(7, 3), Rng.StableHash(7, 3), "StableHash must be deterministic.");
		Assert.AreNotEqual(Rng.StableHash(7, 3), Rng.StableHash(3, 7), "StableHash must be operand-order sensitive.");
	}

	private static List<int> MakeDeck(int size)
	{
		var deck = new List<int>(size);
		for (int i = 0; i < size; i++)
		{
			deck.Add(i);
		}
		return deck;
	}
}
