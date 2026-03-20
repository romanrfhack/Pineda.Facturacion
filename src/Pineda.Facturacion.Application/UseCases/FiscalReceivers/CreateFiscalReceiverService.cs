using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class CreateFiscalReceiverService
{
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFiscalReceiverService(IFiscalReceiverRepository fiscalReceiverRepository, IUnitOfWork unitOfWork)
    {
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateFiscalReceiverResult> ExecuteAsync(CreateFiscalReceiverCommand command, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(command);
        if (validationError is not null)
        {
            return new CreateFiscalReceiverResult
            {
                Outcome = CreateFiscalReceiverOutcome.ValidationFailed,
                IsSuccess = false,
                ErrorMessage = validationError
            };
        }

        var normalizedRfc = FiscalMasterDataNormalization.NormalizeRfc(command.Rfc);
        var existing = await _fiscalReceiverRepository.GetByRfcAsync(normalizedRfc, cancellationToken);
        if (existing is not null)
        {
            return new CreateFiscalReceiverResult
            {
                Outcome = CreateFiscalReceiverOutcome.Conflict,
                IsSuccess = false,
                ErrorMessage = $"A fiscal receiver with RFC '{normalizedRfc}' already exists."
            };
        }

        var now = DateTime.UtcNow;
        var normalizedLegalName = FiscalMasterDataNormalization.NormalizeRequiredText(command.LegalName);
        var normalizedSearchAlias = FiscalMasterDataNormalization.NormalizeOptionalText(command.SearchAlias);
        var fiscalReceiver = new FiscalReceiver
        {
            Rfc = normalizedRfc,
            LegalName = normalizedLegalName,
            NormalizedLegalName = FiscalMasterDataNormalization.NormalizeSearchableText(normalizedLegalName),
            FiscalRegimeCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.FiscalRegimeCode),
            CfdiUseCodeDefault = FiscalMasterDataNormalization.NormalizeRequiredCode(command.CfdiUseCodeDefault),
            PostalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PostalCode),
            CountryCode = FiscalMasterDataNormalization.NormalizeOptionalText(command.CountryCode)?.ToUpperInvariant(),
            ForeignTaxRegistration = FiscalMasterDataNormalization.NormalizeOptionalText(command.ForeignTaxRegistration),
            Email = FiscalMasterDataNormalization.NormalizeOptionalText(command.Email),
            Phone = FiscalMasterDataNormalization.NormalizeOptionalText(command.Phone),
            SearchAlias = normalizedSearchAlias,
            NormalizedSearchAlias = normalizedSearchAlias is null
                ? null
                : FiscalMasterDataNormalization.NormalizeSearchableText(normalizedSearchAlias),
            IsActive = command.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _fiscalReceiverRepository.AddAsync(fiscalReceiver, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateFiscalReceiverResult
        {
            Outcome = CreateFiscalReceiverOutcome.Created,
            IsSuccess = true,
            FiscalReceiverId = fiscalReceiver.Id
        };
    }

    private static string? Validate(CreateFiscalReceiverCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Rfc)) return "RFC is required.";
        if (string.IsNullOrWhiteSpace(command.LegalName)) return "Legal name is required.";
        if (string.IsNullOrWhiteSpace(command.FiscalRegimeCode)) return "Fiscal regime code is required.";
        if (string.IsNullOrWhiteSpace(command.CfdiUseCodeDefault)) return "Default CFDI use code is required.";
        if (string.IsNullOrWhiteSpace(command.PostalCode)) return "Postal code is required.";
        return null;
    }
}
