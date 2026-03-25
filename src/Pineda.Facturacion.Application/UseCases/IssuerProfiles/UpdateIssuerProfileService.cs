using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using System.Globalization;

namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public class UpdateIssuerProfileService
{
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateIssuerProfileService(
        IIssuerProfileRepository issuerProfileRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IUnitOfWork unitOfWork)
    {
        _issuerProfileRepository = issuerProfileRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateIssuerProfileResult> ExecuteAsync(UpdateIssuerProfileCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Id <= 0)
        {
            return ValidationFailure("Issuer profile id is required.");
        }

        var validationError = Validate(command);
        if (validationError is not null)
        {
            return ValidationFailure(validationError);
        }

        var issuerProfile = await _issuerProfileRepository.GetByIdAsync(command.Id, cancellationToken);
        if (issuerProfile is null)
        {
            return new UpdateIssuerProfileResult
            {
                Outcome = UpdateIssuerProfileOutcome.NotFound,
                IsSuccess = false,
                ErrorMessage = $"Issuer profile '{command.Id}' was not found."
            };
        }

        if (command.IsActive)
        {
            var activeIssuer = await _issuerProfileRepository.GetActiveAsync(cancellationToken);
            if (activeIssuer is not null && activeIssuer.Id != command.Id)
            {
                return new UpdateIssuerProfileResult
                {
                    Outcome = UpdateIssuerProfileOutcome.Conflict,
                    IsSuccess = false,
                    ErrorMessage = "Another active issuer profile already exists."
                };
            }
        }

        if (command.NextFiscalFolio.HasValue)
        {
            var configuredSeries = FiscalMasterDataNormalization.NormalizeOptionalText(command.FiscalSeries) ?? string.Empty;
            var configuredFolio = command.NextFiscalFolio.Value.ToString(CultureInfo.InvariantCulture);
            var folioAlreadyExists = await _fiscalDocumentRepository.ExistsByIssuerSeriesAndFolioAsync(
                command.Rfc,
                configuredSeries,
                configuredFolio,
                cancellationToken: cancellationToken);

            if (folioAlreadyExists)
            {
                return new UpdateIssuerProfileResult
                {
                    Outcome = UpdateIssuerProfileOutcome.Conflict,
                    IsSuccess = false,
                    ErrorMessage = $"Fiscal folio '{configuredSeries}{configuredFolio}' is already used for issuer '{FiscalMasterDataNormalization.NormalizeRfc(command.Rfc)}'."
                };
            }
        }

        issuerProfile.LegalName = FiscalMasterDataNormalization.NormalizeRequiredText(command.LegalName);
        issuerProfile.Rfc = FiscalMasterDataNormalization.NormalizeRfc(command.Rfc);
        issuerProfile.FiscalRegimeCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.FiscalRegimeCode);
        issuerProfile.PostalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PostalCode);
        issuerProfile.CfdiVersion = FiscalMasterDataNormalization.NormalizeRequiredCode(command.CfdiVersion);
        issuerProfile.CertificateReference = FiscalMasterDataNormalization.NormalizeRequiredText(command.CertificateReference);
        issuerProfile.PrivateKeyReference = FiscalMasterDataNormalization.NormalizeRequiredText(command.PrivateKeyReference);
        issuerProfile.PrivateKeyPasswordReference = FiscalMasterDataNormalization.NormalizeRequiredText(command.PrivateKeyPasswordReference);
        issuerProfile.PacEnvironment = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PacEnvironment);
        issuerProfile.FiscalSeries = FiscalMasterDataNormalization.NormalizeOptionalText(command.FiscalSeries);
        issuerProfile.NextFiscalFolio = command.NextFiscalFolio;
        issuerProfile.IsActive = command.IsActive;
        issuerProfile.UpdatedAtUtc = DateTime.UtcNow;

        await _issuerProfileRepository.UpdateAsync(issuerProfile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateIssuerProfileResult
        {
            Outcome = UpdateIssuerProfileOutcome.Updated,
            IsSuccess = true,
            IssuerProfileId = issuerProfile.Id
        };
    }

    private static UpdateIssuerProfileResult ValidationFailure(string errorMessage)
    {
        return new UpdateIssuerProfileResult
        {
            Outcome = UpdateIssuerProfileOutcome.ValidationFailed,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }

    private static string? Validate(UpdateIssuerProfileCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.LegalName)) return "Legal name is required.";
        if (string.IsNullOrWhiteSpace(command.Rfc)) return "RFC is required.";
        if (string.IsNullOrWhiteSpace(command.FiscalRegimeCode)) return "Fiscal regime code is required.";
        if (string.IsNullOrWhiteSpace(command.PostalCode)) return "Postal code is required.";
        if (string.IsNullOrWhiteSpace(command.CfdiVersion)) return "CFDI version is required.";
        if (string.IsNullOrWhiteSpace(command.CertificateReference)) return "Certificate reference is required.";
        if (string.IsNullOrWhiteSpace(command.PrivateKeyReference)) return "Private key reference is required.";
        if (string.IsNullOrWhiteSpace(command.PrivateKeyPasswordReference)) return "Private key password reference is required.";
        if (string.IsNullOrWhiteSpace(command.PacEnvironment)) return "PAC environment is required.";
        if (!command.NextFiscalFolio.HasValue) return "Next fiscal folio is required.";
        if (command.NextFiscalFolio <= 0) return "Next fiscal folio must be a positive integer.";
        return null;
    }
}
