using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class ImportExternalRepBaseDocumentFromXmlService
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IExternalRepBaseDocumentRepository _repository;
    private readonly IFiscalStatusQueryGateway _fiscalStatusQueryGateway;
    private readonly IUnitOfWork _unitOfWork;

    public ImportExternalRepBaseDocumentFromXmlService(
        ICurrentUserAccessor currentUserAccessor,
        IExternalRepBaseDocumentRepository repository,
        IFiscalStatusQueryGateway fiscalStatusQueryGateway,
        IUnitOfWork unitOfWork)
    {
        _currentUserAccessor = currentUserAccessor;
        _repository = repository;
        _fiscalStatusQueryGateway = fiscalStatusQueryGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportExternalRepBaseDocumentFromXmlResult> ExecuteAsync(
        ImportExternalRepBaseDocumentFromXmlCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FileContent.Length == 0)
        {
            return Reject(ExternalRepBaseDocumentImportReasonCode.InvalidXml, "El archivo XML es obligatorio.");
        }

        var xmlContent = DecodeXml(command.FileContent);
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            return Reject(ExternalRepBaseDocumentImportReasonCode.InvalidXml, "No fue posible leer el contenido del XML.");
        }

        if (!TryParseSnapshot(xmlContent, out var snapshot, out var parseError, out var reasonCode))
        {
            return Reject(reasonCode, parseError ?? "No fue posible interpretar el XML del CFDI.");
        }

        var existing = await _repository.GetByUuidAsync(snapshot!.Uuid, cancellationToken);
        if (existing is not null)
        {
            return new ImportExternalRepBaseDocumentFromXmlResult
            {
                Outcome = ImportExternalRepBaseDocumentFromXmlOutcome.Duplicate,
                IsSuccess = false,
                ExternalRepBaseDocumentId = existing.Id,
                ValidationStatus = "Rejected",
                ReasonCode = ExternalRepBaseDocumentImportReasonCode.DuplicateExternalInvoice.ToString(),
                ReasonMessage = "Ya existe una factura externa importada con el mismo UUID.",
                ErrorMessage = "Ya existe una factura externa importada con el mismo UUID.",
                Uuid = existing.Uuid,
                IssuerRfc = existing.IssuerRfc,
                ReceiverRfc = existing.ReceiverRfc,
                PaymentMethodSat = existing.PaymentMethodSat,
                PaymentFormSat = existing.PaymentFormSat,
                CurrencyCode = existing.CurrencyCode,
                Total = existing.Total,
                IsDuplicate = true
            };
        }

        var satEvaluation = await EvaluateSatStatusAsync(snapshot, cancellationToken);
        if (satEvaluation.Outcome == ImportExternalRepBaseDocumentFromXmlOutcome.Rejected)
        {
            return Reject(
                satEvaluation.ReasonCode,
                satEvaluation.ReasonMessage,
                snapshot.Uuid,
                snapshot.IssuerRfc,
                snapshot.ReceiverRfc,
                snapshot.PaymentMethodSat,
                snapshot.PaymentFormSat,
                snapshot.CurrencyCode,
                snapshot.Total);
        }

        var now = DateTime.UtcNow;
        var currentUser = _currentUserAccessor.GetCurrentUser();
        var document = new ExternalRepBaseDocument
        {
            Uuid = snapshot.Uuid,
            CfdiVersion = snapshot.CfdiVersion,
            DocumentType = snapshot.DocumentType,
            Series = snapshot.Series,
            Folio = snapshot.Folio,
            IssuedAtUtc = snapshot.IssuedAtUtc,
            IssuerRfc = snapshot.IssuerRfc,
            IssuerLegalName = snapshot.IssuerLegalName,
            ReceiverRfc = snapshot.ReceiverRfc,
            ReceiverLegalName = snapshot.ReceiverLegalName,
            CurrencyCode = snapshot.CurrencyCode,
            ExchangeRate = snapshot.ExchangeRate,
            Subtotal = snapshot.Subtotal,
            Total = snapshot.Total,
            PaymentMethodSat = snapshot.PaymentMethodSat,
            PaymentFormSat = snapshot.PaymentFormSat,
            ValidationStatus = satEvaluation.ValidationStatus,
            ValidationReasonCode = satEvaluation.ReasonCode.ToString(),
            ValidationReasonMessage = satEvaluation.ReasonMessage,
            SatStatus = satEvaluation.SatStatus,
            LastSatCheckAtUtc = satEvaluation.CheckedAtUtc,
            LastSatExternalStatus = satEvaluation.ExternalStatus,
            LastSatCancellationStatus = satEvaluation.CancellationStatus,
            LastSatProviderCode = satEvaluation.ProviderCode,
            LastSatProviderMessage = satEvaluation.ProviderMessage,
            LastSatRawResponseSummaryJson = satEvaluation.RawResponseSummaryJson,
            SourceFileName = string.IsNullOrWhiteSpace(command.SourceFileName) ? "external-invoice.xml" : command.SourceFileName.Trim(),
            XmlContent = xmlContent,
            XmlHash = ComputeSha256(xmlContent),
            ImportedAtUtc = now,
            ImportedByUserId = currentUser.UserId,
            ImportedByUsername = currentUser.Username,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _repository.AddAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ImportExternalRepBaseDocumentFromXmlResult
        {
            Outcome = satEvaluation.Outcome,
            IsSuccess = satEvaluation.Outcome == ImportExternalRepBaseDocumentFromXmlOutcome.Accepted,
            ExternalRepBaseDocumentId = document.Id,
            ValidationStatus = satEvaluation.ValidationStatus.ToString(),
            ReasonCode = satEvaluation.ReasonCode.ToString(),
            ReasonMessage = satEvaluation.ReasonMessage,
            Uuid = document.Uuid,
            IssuerRfc = document.IssuerRfc,
            ReceiverRfc = document.ReceiverRfc,
            PaymentMethodSat = document.PaymentMethodSat,
            PaymentFormSat = document.PaymentFormSat,
            CurrencyCode = document.CurrencyCode,
            Total = document.Total,
            IsDuplicate = false
        };
    }

    private static ImportExternalRepBaseDocumentFromXmlResult Reject(
        ExternalRepBaseDocumentImportReasonCode reasonCode,
        string reasonMessage,
        string? uuid = null,
        string? issuerRfc = null,
        string? receiverRfc = null,
        string? paymentMethodSat = null,
        string? paymentFormSat = null,
        string? currencyCode = null,
        decimal? total = null)
    {
        return new ImportExternalRepBaseDocumentFromXmlResult
        {
            Outcome = ImportExternalRepBaseDocumentFromXmlOutcome.Rejected,
            IsSuccess = false,
            ValidationStatus = "Rejected",
            ReasonCode = reasonCode.ToString(),
            ReasonMessage = reasonMessage,
            ErrorMessage = reasonMessage,
            Uuid = uuid,
            IssuerRfc = issuerRfc,
            ReceiverRfc = receiverRfc,
            PaymentMethodSat = paymentMethodSat,
            PaymentFormSat = paymentFormSat,
            CurrencyCode = currencyCode,
            Total = total,
            IsDuplicate = false
        };
    }

    private async Task<SatImportEvaluation> EvaluateSatStatusAsync(ParsedExternalRepXmlSnapshot snapshot, CancellationToken cancellationToken)
    {
        FiscalStatusQueryGatewayResult gatewayResult;
        try
        {
            gatewayResult = await _fiscalStatusQueryGateway.QueryStatusAsync(
                new FiscalStatusQueryRequest
                {
                    FiscalDocumentId = 0,
                    Uuid = snapshot.Uuid,
                    IssuerRfc = snapshot.IssuerRfc,
                    ReceiverRfc = snapshot.ReceiverRfc,
                    Total = snapshot.Total
                },
                cancellationToken);
        }
        catch
        {
            gatewayResult = new FiscalStatusQueryGatewayResult
            {
                Outcome = FiscalStatusQueryGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "consultarEstadoSAT",
                CheckedAtUtc = DateTime.UtcNow,
                ErrorMessage = "No fue posible consultar el estado SAT del CFDI externo."
            };
        }

        var normalizedExternalStatus = Normalize(gatewayResult.ExternalStatus);
        var normalizedCancellationStatus = Normalize(gatewayResult.CancellationStatus);
        var checkedAtUtc = gatewayResult.CheckedAtUtc == default ? DateTime.UtcNow : gatewayResult.CheckedAtUtc;

        if (gatewayResult.Outcome == FiscalStatusQueryGatewayOutcome.Refreshed
            && (normalizedExternalStatus is "CANCELADO" or "CANCELLED" or "CANCELED"
                || normalizedCancellationStatus is "CANCELADO CON ACEPTACION" or "CANCELADO SIN ACEPTACION"))
        {
            return new SatImportEvaluation
            {
                Outcome = ImportExternalRepBaseDocumentFromXmlOutcome.Blocked,
                ValidationStatus = ExternalRepBaseDocumentValidationStatus.Blocked,
                ReasonCode = ExternalRepBaseDocumentImportReasonCode.CancelledExternalInvoice,
                ReasonMessage = "El CFDI externo aparece cancelado en SAT y quedó bloqueado localmente.",
                SatStatus = ExternalRepBaseDocumentSatStatus.Cancelled,
                CheckedAtUtc = checkedAtUtc,
                ExternalStatus = gatewayResult.ExternalStatus,
                CancellationStatus = gatewayResult.CancellationStatus,
                ProviderCode = gatewayResult.ProviderCode,
                ProviderMessage = gatewayResult.ProviderMessage,
                RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson
            };
        }

        if (gatewayResult.Outcome == FiscalStatusQueryGatewayOutcome.Refreshed
            && normalizedExternalStatus is "VIGENTE" or "ACTIVE" or "STAMPED")
        {
            return new SatImportEvaluation
            {
                Outcome = ImportExternalRepBaseDocumentFromXmlOutcome.Accepted,
                ValidationStatus = ExternalRepBaseDocumentValidationStatus.Accepted,
                ReasonCode = ExternalRepBaseDocumentImportReasonCode.Accepted,
                ReasonMessage = "La factura externa fue importada y validada correctamente para operación REP futura.",
                SatStatus = ExternalRepBaseDocumentSatStatus.Active,
                CheckedAtUtc = checkedAtUtc,
                ExternalStatus = gatewayResult.ExternalStatus,
                CancellationStatus = gatewayResult.CancellationStatus,
                ProviderCode = gatewayResult.ProviderCode,
                ProviderMessage = gatewayResult.ProviderMessage,
                RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson
            };
        }

        return new SatImportEvaluation
        {
            Outcome = ImportExternalRepBaseDocumentFromXmlOutcome.Blocked,
            ValidationStatus = ExternalRepBaseDocumentValidationStatus.Blocked,
            ReasonCode = ExternalRepBaseDocumentImportReasonCode.ValidationUnavailable,
            ReasonMessage = gatewayResult.Outcome == FiscalStatusQueryGatewayOutcome.Unavailable
                ? "No fue posible confirmar el estado SAT del CFDI externo. La factura quedó importada pero bloqueada."
                : "La validación SAT del CFDI externo no devolvió un estado confiable. La factura quedó importada pero bloqueada.",
            SatStatus = ExternalRepBaseDocumentSatStatus.Unavailable,
            CheckedAtUtc = checkedAtUtc,
            ExternalStatus = gatewayResult.ExternalStatus,
            CancellationStatus = gatewayResult.CancellationStatus,
            ProviderCode = gatewayResult.ProviderCode,
            ProviderMessage = gatewayResult.ProviderMessage ?? gatewayResult.ErrorMessage,
            RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson
        };
    }

    private static bool TryParseSnapshot(
        string xmlContent,
        out ParsedExternalRepXmlSnapshot? snapshot,
        out string? errorMessage,
        out ExternalRepBaseDocumentImportReasonCode reasonCode)
    {
        snapshot = null;
        errorMessage = null;
        reasonCode = ExternalRepBaseDocumentImportReasonCode.InvalidXml;

        XDocument document;
        try
        {
            document = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
        }
        catch (Exception)
        {
            errorMessage = "El archivo XML no es válido o no pudo interpretarse.";
            return false;
        }

        var root = document.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "Comprobante", StringComparison.Ordinal))
        {
            errorMessage = "El XML no corresponde a un CFDI reconocible.";
            return false;
        }

        var documentType = Normalize(GetAttribute(root, "TipoDeComprobante"));
        if (!string.Equals(documentType, "I", StringComparison.Ordinal))
        {
            reasonCode = ExternalRepBaseDocumentImportReasonCode.UnsupportedVoucherType;
            errorMessage = "Solo se admiten CFDI de ingreso para administración REP externa.";
            return false;
        }

        var uuid = Normalize(FindUuid(document));
        if (string.IsNullOrWhiteSpace(uuid))
        {
            reasonCode = ExternalRepBaseDocumentImportReasonCode.MissingUuid;
            errorMessage = "El XML no contiene UUID timbrado.";
            return false;
        }

        var issuer = root.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, "Emisor", StringComparison.Ordinal));
        var receiver = root.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, "Receptor", StringComparison.Ordinal));
        var issuerRfc = Normalize(GetAttribute(issuer, "Rfc"));
        var receiverRfc = Normalize(GetAttribute(receiver, "Rfc"));
        if (string.IsNullOrWhiteSpace(issuerRfc) || string.IsNullOrWhiteSpace(receiverRfc))
        {
            reasonCode = ExternalRepBaseDocumentImportReasonCode.MissingIssuerOrReceiver;
            errorMessage = "El XML no contiene RFC de emisor y receptor requeridos.";
            return false;
        }

        var paymentMethodSat = Normalize(GetAttribute(root, "MetodoPago"));
        if (!string.Equals(paymentMethodSat, "PPD", StringComparison.Ordinal))
        {
            reasonCode = ExternalRepBaseDocumentImportReasonCode.UnsupportedPaymentMethod;
            errorMessage = "La factura externa debe usar MetodoPago PPD para REP.";
            return false;
        }

        var paymentFormSat = Normalize(GetAttribute(root, "FormaPago"));
        if (!string.Equals(paymentFormSat, "99", StringComparison.Ordinal))
        {
            reasonCode = ExternalRepBaseDocumentImportReasonCode.UnsupportedPaymentForm;
            errorMessage = "La factura externa debe usar FormaPago 99 para REP.";
            return false;
        }

        var currencyCode = Normalize(GetAttribute(root, "Moneda")) ?? "MXN";
        if (!string.Equals(currencyCode, "MXN", StringComparison.Ordinal))
        {
            reasonCode = ExternalRepBaseDocumentImportReasonCode.UnsupportedCurrency;
            errorMessage = "La fase actual solo soporta facturas externas en MXN.";
            return false;
        }

        if (!TryParseDecimal(GetAttribute(root, "SubTotal"), out var subtotal)
            || !TryParseDecimal(GetAttribute(root, "Total"), out var total)
            || subtotal < 0m
            || total <= 0m
            || total < subtotal)
        {
            reasonCode = ExternalRepBaseDocumentImportReasonCode.InvalidTotals;
            errorMessage = "El XML no contiene importes válidos para subtotal y total.";
            return false;
        }

        if (!TryParseIssuedAtUtc(GetAttribute(root, "Fecha"), out var issuedAtUtc))
        {
            errorMessage = "El XML no contiene una fecha de emisión válida.";
            return false;
        }

        var exchangeRate = 1m;
        if (TryParseDecimal(GetAttribute(root, "TipoCambio"), out var parsedExchangeRate) && parsedExchangeRate > 0m)
        {
            exchangeRate = parsedExchangeRate;
        }

        snapshot = new ParsedExternalRepXmlSnapshot
        {
            Uuid = uuid,
            CfdiVersion = Normalize(GetAttribute(root, "Version")) ?? string.Empty,
            DocumentType = documentType!,
            Series = Normalize(GetAttribute(root, "Serie")) ?? string.Empty,
            Folio = Normalize(GetAttribute(root, "Folio")) ?? string.Empty,
            IssuedAtUtc = issuedAtUtc,
            IssuerRfc = issuerRfc!,
            IssuerLegalName = Normalize(GetAttribute(issuer, "Nombre")),
            ReceiverRfc = receiverRfc!,
            ReceiverLegalName = Normalize(GetAttribute(receiver, "Nombre")),
            CurrencyCode = currencyCode,
            ExchangeRate = exchangeRate,
            Subtotal = subtotal,
            Total = total,
            PaymentMethodSat = paymentMethodSat!,
            PaymentFormSat = paymentFormSat!
        };
        reasonCode = ExternalRepBaseDocumentImportReasonCode.Accepted;
        return true;
    }

    private static string DecodeXml(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(content, 3, content.Length - 3);
        }

        return Encoding.UTF8.GetString(content);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? FindUuid(XDocument document)
    {
        var timbre = document
            .Descendants()
            .FirstOrDefault(x => string.Equals(x.Name.LocalName, "TimbreFiscalDigital", StringComparison.Ordinal));

        return GetAttribute(timbre, "UUID");
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseIssuedAtUtc(string? value, out DateTime issuedAtUtc)
    {
        issuedAtUtc = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var offset))
        {
            issuedAtUtc = offset.UtcDateTime;
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var localDateTime))
        {
            issuedAtUtc = DateTime.SpecifyKind(localDateTime, DateTimeKind.Local).ToUniversalTime();
            return true;
        }

        return false;
    }

    private static string? GetAttribute(XElement? element, string name)
    {
        return element?.Attributes().FirstOrDefault(x => string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static string? Normalize(string? value)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(value)?.ToUpperInvariant();
    }

    private sealed class ParsedExternalRepXmlSnapshot
    {
        public string Uuid { get; init; } = string.Empty;

        public string CfdiVersion { get; init; } = string.Empty;

        public string DocumentType { get; init; } = string.Empty;

        public string Series { get; init; } = string.Empty;

        public string Folio { get; init; } = string.Empty;

        public DateTime IssuedAtUtc { get; init; }

        public string IssuerRfc { get; init; } = string.Empty;

        public string? IssuerLegalName { get; init; }

        public string ReceiverRfc { get; init; } = string.Empty;

        public string? ReceiverLegalName { get; init; }

        public string CurrencyCode { get; init; } = string.Empty;

        public decimal ExchangeRate { get; init; }

        public decimal Subtotal { get; init; }

        public decimal Total { get; init; }

        public string PaymentMethodSat { get; init; } = string.Empty;

        public string PaymentFormSat { get; init; } = string.Empty;
    }

    private sealed class SatImportEvaluation
    {
        public ImportExternalRepBaseDocumentFromXmlOutcome Outcome { get; init; }

        public ExternalRepBaseDocumentValidationStatus ValidationStatus { get; init; }

        public ExternalRepBaseDocumentImportReasonCode ReasonCode { get; init; }

        public string ReasonMessage { get; init; } = string.Empty;

        public ExternalRepBaseDocumentSatStatus SatStatus { get; init; }

        public DateTime? CheckedAtUtc { get; init; }

        public string? ExternalStatus { get; init; }

        public string? CancellationStatus { get; init; }

        public string? ProviderCode { get; init; }

        public string? ProviderMessage { get; init; }

        public string? RawResponseSummaryJson { get; init; }
    }
}
