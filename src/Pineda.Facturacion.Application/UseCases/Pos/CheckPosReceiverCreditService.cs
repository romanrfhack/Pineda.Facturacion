namespace Pineda.Facturacion.Application.UseCases.Pos;

public sealed class CheckPosReceiverCreditService
{
    private readonly GetPosReceiverCreditStatusService _getPosReceiverCreditStatusService;

    public CheckPosReceiverCreditService(GetPosReceiverCreditStatusService getPosReceiverCreditStatusService)
    {
        _getPosReceiverCreditStatusService = getPosReceiverCreditStatusService;
    }

    public async Task<CheckPosReceiverCreditResult> ExecuteAsync(
        CheckPosReceiverCreditCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.SaleAmount <= 0m)
        {
            return ValidationFailure(
                PosCreditValidationErrorCodes.InvalidSaleAmount,
                "Sale amount must be greater than zero.");
        }

        var normalizedCurrencyCode = command.CurrencyCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCurrencyCode)
            || !string.Equals(normalizedCurrencyCode, PosCreditContract.SupportedCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationFailure(
                PosCreditValidationErrorCodes.UnsupportedCurrency,
                $"Currency code '{PosCreditContract.SupportedCurrencyCode}' is the only supported value.");
        }

        var statusResult = await _getPosReceiverCreditStatusService.ExecuteAsync(command.FiscalReceiverId, cancellationToken);
        if (statusResult.Outcome == GetPosReceiverCreditStatusOutcome.NotFound || statusResult.CreditStatus is null)
        {
            return new CheckPosReceiverCreditResult
            {
                Outcome = CheckPosReceiverCreditOutcome.NotFound
            };
        }

        var creditStatus = statusResult.CreditStatus;
        var blockReason = creditStatus.BlockReason;

        if (blockReason is null && command.SaleAmount > creditStatus.AvailableCreditAmount)
        {
            blockReason = PosCreditBlockReasons.InsufficientCredit;
        }

        var approved = blockReason is null;

        return new CheckPosReceiverCreditResult
        {
            Outcome = approved ? CheckPosReceiverCreditOutcome.Approved : CheckPosReceiverCreditOutcome.Blocked,
            Evaluation = new PosReceiverCreditCheckEvaluation
            {
                Approved = approved,
                AvailableCreditAmount = creditStatus.AvailableCreditAmount,
                SaleAmount = command.SaleAmount,
                RemainingCreditAmount = creditStatus.AvailableCreditAmount - command.SaleAmount,
                BlockReason = blockReason
            }
        };
    }

    private static CheckPosReceiverCreditResult ValidationFailure(string errorCode, string errorMessage)
    {
        return new CheckPosReceiverCreditResult
        {
            Outcome = CheckPosReceiverCreditOutcome.ValidationFailed,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
