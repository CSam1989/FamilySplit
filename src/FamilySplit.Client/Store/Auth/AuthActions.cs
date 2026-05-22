using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Auth;

/// <summary>Dispatched once on app startup to resolve who is logged in.</summary>
public record CheckAuthAction;
public record CheckAuthSuccessAction(WhoAmIResponse User);
public record CheckAuthNotAuthenticatedAction;
public record SignOutAction;
