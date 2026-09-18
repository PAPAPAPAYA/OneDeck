using UnityEngine;

// Single input gate for every shop interaction (plan-world-entity-shop-chrome-2026-09-18):
// PhysButton presses, card-body click-to-enlarge, restore clicks. Reference counting —
// always pair Block/Unblock, mirroring CombatManager.BlockInput/UnblockInput. With all
// shop interaction on the one physics pipeline, one gate covers the whole phase; a blocked
// gate swallows both press starts and release activations.
public static class ShopInputGate
{
	private static int _blockCount;

	public static bool Blocked => _blockCount > 0;

	public static void Block()
	{
		_blockCount++;
	}

	public static void Unblock()
	{
		if (_blockCount > 0) _blockCount--;
	}
}
