using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Animated numeric-pair text binding (plan-shop-mirror-prefab §3.3): the HP pill's
/// "{hp}/{hpMax}" value with the 0.3 s count-up the old ShopPageHud copy machine played.
/// ShopHudBinder pushes SetTarget on its refresh ticks; a double diff-guard (target pair
/// changed AND shown pair differs) starts the tween, and OnDisable kills it while the
/// page is hidden — a behavior-preserving port of ShopPageHud.InitHpText/Refresh/
/// PlayCountUp/OnDisable (first push snaps, later changes count up).
/// </summary>
public class HudCountBinding : MonoBehaviour
{
	private const float CountUpSeconds = 0.3f;

	[TextArea] public string format = "{0}/{1}";
	public TMP_Text target;

	private Tween _countTween;
	private bool _hasInitialized;
	private int _targetHp = int.MinValue;
	private int _targetHpMax = int.MinValue;
	private int _shownHp;
	private int _shownMax;

	/// <summary>Pushes a new hp/hpMax pair; first push snaps, later changes count up (diff-guarded).</summary>
	public void SetTarget(int hp, int hpMax)
	{
		if (target == null) return;
		if (_hasInitialized && hp == _targetHp && hpMax == _targetHpMax) return;
		_targetHp = hp;
		_targetHpMax = hpMax;
		if (!_hasInitialized)
		{
			// InitHpText semantics: the first visible value lands without a tween.
			_hasInitialized = true;
			_shownHp = hp;
			_shownMax = hpMax;
			Write();
			return;
		}
		if (hp == _shownHp && hpMax == _shownMax) return;
		PlayCountUp(hp, hpMax);
	}

	private void PlayCountUp(int hp, int hpMax)
	{
		KillTween();
		int fromHp = _shownHp;
		int fromMax = _shownMax;
		HudCountBinding self = this;
		_countTween = DOTween.To(() => 0f, v =>
		{
			self._shownHp = Mathf.RoundToInt(Mathf.Lerp(fromHp, hp, v));
			self._shownMax = Mathf.RoundToInt(Mathf.Lerp(fromMax, hpMax, v));
			self.Write();
		}, 1f, CountUpSeconds).OnComplete(() =>
		{
			self._shownHp = hp;
			self._shownMax = hpMax;
			self.Write();
		});
	}

	private void Write()
	{
		if (target != null) target.text = string.Format(format, _shownHp, _shownMax);
	}

	private void OnDisable()
	{
		// The page deactivates on shop exit / mid-travel; a running count tween would keep
		// mutating the text invisibly. The next visible SetTarget re-syncs from scratch.
		KillTween();
	}

	private void KillTween()
	{
		if (_countTween != null && _countTween.IsActive()) _countTween.Kill();
		_countTween = null;
	}
}
