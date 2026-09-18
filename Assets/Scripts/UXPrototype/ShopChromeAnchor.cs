using UnityEngine;

// Keeps the world-space shop chrome pinned to the viewport: the band stays fixed while
// the shop world scrolls beneath it. Reads Camera.main only — MilkShake owns the camera's
// localPosition and the rig ("Camera Man") is the scroll target, so neither may parent
// the chrome. Position (not localPosition) is written so the root can live anywhere.
public class ShopChromeAnchor : MonoBehaviour
{
	private Camera _cam;

	void LateUpdate()
	{
		if (_cam == null) _cam = Camera.main;
		if (_cam == null) return;
		Vector3 p = _cam.transform.position;
		// Fixed distance in front of the camera: between the card layer and the near plane
		// regardless of where the camera actually sits on z.
		transform.position = new Vector3(p.x, p.y + _cam.orthographicSize - ShopChrome.BandInsetFromTop, p.z + ShopChrome.CameraForwardOffset);
	}
}
