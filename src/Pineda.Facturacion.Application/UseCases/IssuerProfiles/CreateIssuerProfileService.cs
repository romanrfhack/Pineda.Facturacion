using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using System.Globalization;

namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public class CreateIssuerProfileService
{
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateIssuerProfileService(
        IIssuerProfileRepository issuerProfileRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IUnitOfWork unitOfWork)
    {
        _issuerProfileRepository = issuerProfileRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateIssuerProfileResult> ExecuteAsync(CreateIssuerProfileCommand command, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(command);
        if (validationError is not null)
        {
            return new CreateIssuerProfileResult
            {
                Outcome = CreateIssuerProfileOutcome.ValidationFailed,
                IsSuccess = false,
                ErrorMessage = validationError
            };
        }

        if (command.IsActive)
        {
            var activeIssuer = await _issuerProfileRepository.GetActiveAsync(cancellationToken);
            if (activeIssuer is not null)
            {
                return new CreateIssuerProfileResult
                {
                    Outcome = CreateIssuerProfileOutcome.Conflict,
                    IsSuccess = false,
                    ErrorMessage = "An active issuer profile already exists."
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
                return new CreateIssuerProfileResult
                {
                    Outcome = CreateIssuerProfileOutcome.Conflict,
                    IsSuccess = false,
                    ErrorMessage = $"Fiscal folio '{configuredSeries}{configuredFolio}' is already used for issuer '{FiscalMasterDataNormalization.NormalizeRfc(command.Rfc)}'."
                };
            }
        }

        var now = DateTime.UtcNow;
        var issuerProfile = new IssuerProfile
        {
            LegalName = FiscalMasterDataNormalization.NormalizeRequiredText(command.LegalName),
            Rfc = FiscalMasterDataNormalization.NormalizeRfc(command.Rfc),
            FiscalRegimeCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.FiscalRegimeCode),
            PostalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PostalCode),
            CfdiVersion = FiscalMasterDataNormalization.NormalizeRequiredCode(command.CfdiVersion),
            CertificateReference = FiscalMasterDataNormalization.NormalizeRequiredText(command.CertificateReference),
            PrivateKeyReference = FiscalMasterDataNormalization.NormalizeRequiredText(command.PrivateKeyReference),
            PrivateKeyPasswordReference = FiscalMasterDataNormalization.NormalizeRequiredText(command.PrivateKeyPasswordReference),
            PacEnvironment = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PacEnvironment),
            FiscalSeries = FiscalMasterDataNormalization.NormalizeOptionalText(command.FiscalSeries),
            NextFiscalFolio = command.NextFiscalFolio,
            IsActive = command.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _issuerProfileRepository.AddAsync(issuerProfile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateIssuerProfileResult
        {
            Outcome = CreateIssuerProfileOutcome.Created,
            IsSuccess = true,
            IssuerProfileId = issuerProfile.Id
        };
    }

    private static string? Validate(CreateIssuerProfileCommand command)
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
