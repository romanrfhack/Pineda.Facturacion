using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public class CreateIssuerProfileService
{
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateIssuerProfileService(IIssuerProfileRepository issuerProfileRepository, IUnitOfWork unitOfWork)
    {
        _issuerProfileRepository = issuerProfileRepository;
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
        return null;
    }
}
