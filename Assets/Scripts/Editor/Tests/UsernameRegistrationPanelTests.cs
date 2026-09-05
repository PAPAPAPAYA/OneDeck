using NUnit.Framework;
using UnityEngine;

/// <summary>
/// UsernameRegistrationPanel EditMode tests (plan: plans/plan-username-registration-panel-
/// 2026-09-04.md). HintFor is a pure static - these tests need no network, no UI and no
/// identity file, so no override seams are required.
/// </summary>
public class UsernameRegistrationPanelTests
{
	[Test]
	public void HintFor_MapsKnownRegistrationErrors()
	{
		StringAssert.Contains("已被占用", UsernameRegistrationPanel.HintFor("username_taken"));
		StringAssert.Contains("2-16", UsernameRegistrationPanel.HintFor("invalid_username"));
		StringAssert.Contains("响应异常", UsernameRegistrationPanel.HintFor("bad_response"));
	}

	[Test]
	public void HintFor_FallsBackToNetworkError()
	{
		StringAssert.Contains("网络错误", UsernameRegistrationPanel.HintFor("Some transport error"));
		StringAssert.Contains("网络错误", UsernameRegistrationPanel.HintFor(null));
		StringAssert.Contains("网络错误", UsernameRegistrationPanel.HintFor(""));
	}
}
