namespace FamilySplit.Common.Routing;

/// <summary>
/// Named rate-limit policy keys. The Auth slice both defines the policy (in its
/// module) and applies it to its endpoint group — the shared constant guarantees
/// a rename can never silently split the two.
/// </summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
}
