using UnityEngine;

public class ValueTrackerManager : MonoBehaviour
{
	public static ValueTrackerManager me;

	private void Awake()
	{
		me = this;
	}

	/// <summary>
	/// 在效果执行前统一刷新所有追踪数值
	/// </summary>
	public void UpdateAllTrackers()
	{
		// TODO: 在这里添加各种数值的更新逻辑
	}
}
