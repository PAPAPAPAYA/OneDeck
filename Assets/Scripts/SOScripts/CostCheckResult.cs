using System.Collections.Generic;

/// <summary>
/// Represents the result of a cost check operation.
/// Used by CostNEffectContainer to communicate cost check outcomes
/// without directly writing to UI text.
/// </summary>
public struct CostCheckResult
{
	/// <summary>
	/// True if all costs were met, false if any cost check failed.
	/// </summary>
	public bool success;

	/// <summary>
	/// List of failure messages collected during cost checking.
	/// Populated only when success is false.
	/// </summary>
	public List<string> failMessages;

	/// <summary>
	/// Convenience constructor.
	/// </summary>
	public CostCheckResult(bool success, List<string> failMessages = null)
	{
		this.success = success;
		this.failMessages = failMessages ?? new List<string>();
	}

	/// <summary>
	/// Returns a successful result with no failure messages.
	/// </summary>
	public static CostCheckResult Success()
	{
		return new CostCheckResult(true);
	}

	/// <summary>
	/// Returns a failed result with the given failure messages.
	/// </summary>
	public static CostCheckResult Failed(List<string> messages)
	{
		return new CostCheckResult(false, messages);
	}
}
