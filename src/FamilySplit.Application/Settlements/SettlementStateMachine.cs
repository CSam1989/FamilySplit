using FamilySplit.Domain.Enums;

namespace FamilySplit.Application.Settlements;

/// <summary>Pure state-transition guards for the Settlement approval flow.</summary>
public static class SettlementStateMachine
{
    /// <summary>Returns true when a Proposed settlement can be marked as PayerSent.</summary>
    public static bool CanConfirmSent(SettlementStatus status) => status == SettlementStatus.Proposed;

    /// <summary>Returns true when a PayerSent settlement can be marked as Completed.</summary>
    public static bool CanConfirmReceived(SettlementStatus status) => status == SettlementStatus.PayerSent;
}
