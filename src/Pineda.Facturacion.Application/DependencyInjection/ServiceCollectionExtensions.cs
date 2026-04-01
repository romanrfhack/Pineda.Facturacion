using Microsoft.Extensions.DependencyInjection;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
using Pineda.Facturacion.Application.UseCases.Audit;
using Pineda.Facturacion.Application.UseCases.Auth;
using Pineda.Facturacion.Application.UseCases.BillingDocuments;
using Pineda.Facturacion.Application.UseCases.CreateBillingDocument;
using Pineda.Facturacion.Application.UseCases.FiscalReceivers;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrderPreview;
using Pineda.Facturacion.Application.UseCases.IssuerProfiles;
using Pineda.Facturacion.Application.UseCases.Orders;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

namespace Pineda.Facturacion.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ImportLegacyOrderService>();
        services.AddScoped<PreviewLegacyOrderImportService>();
        services.AddScoped<SearchLegacyOrdersService>();
        services.AddScoped<ListAuditEventsService>();
        services.AddScoped<LoginService>();
        services.AddScoped<GetCurrentUserService>();
        services.AddScoped<CreateBillingDocumentService>();
        services.AddScoped<GetBillingDocumentLookupByIdService>();
        services.AddScoped<SearchBillingDocumentsService>();
        services.AddScoped<ListPendingBillingItemsService>();
        services.AddScoped<UpdateBillingDocumentOrderAssociationService>();
        services.AddScoped<RemoveBillingDocumentItemService>();
        services.AddScoped<AssignPendingBillingItemsService>();
        services.AddScoped<CreateAccountsReceivableInvoiceFromFiscalDocumentService>();
        services.AddScoped<GetAccountsReceivableInvoiceByFiscalDocumentIdService>();
        services.AddScoped<CreateAccountsReceivablePaymentService>();
        services.AddScoped<GetAccountsReceivablePaymentByIdService>();
        services.AddScoped<ApplyAccountsReceivablePaymentService>();
        services.AddScoped<PreparePaymentComplementService>();
        services.AddScoped<GetPaymentComplementByPaymentIdService>();
        services.AddScoped<StampPaymentComplementService>();
        services.AddScoped<GetPaymentComplementStampByPaymentComplementIdService>();
        services.AddScoped<CancelPaymentComplementService>();
        services.AddScoped<GetPaymentComplementCancellationByPaymentComplementIdService>();
        services.AddScoped<RefreshPaymentComplementStatusService>();
        services.AddScoped<PrepareFiscalDocumentService>();
        services.AddScoped<SearchIssuedFiscalDocumentsService>();
        services.AddScoped<ListIssuedFiscalDocumentSpecialFieldsService>();
        services.AddScoped<GetFiscalDocumentByIdService>();
        services.AddScoped<GetFiscalStampByFiscalDocumentIdService>();
        services.AddScoped<QueryRemoteFiscalStampService>();
        services.AddScoped<GetFiscalDocumentPdfService>();
        services.AddScoped<GetFiscalDocumentEmailDraftService>();
        services.AddScoped<StampFiscalDocumentService>();
        services.AddScoped<SendFiscalDocumentEmailService>();
        services.AddScoped<GetFiscalCancellationByFiscalDocumentIdService>();
        services.AddScoped<CancelFiscalDocumentService>();
        services.AddScoped<RefreshFiscalDocumentStatusService>();
        services.AddScoped<ListPendingFiscalCancellationAuthorizationsService>();
        services.AddScoped<RespondFiscalCancellationAuthorizationService>();
        services.AddScoped<CreateIssuerProfileService>();
        services.AddScoped<UpdateIssuerProfileService>();
        services.AddScoped<GetActiveIssuerProfileService>();
        services.AddScoped<UploadIssuerProfileLogoService>();
        services.AddScoped<GetIssuerProfileLogoService>();
        services.AddScoped<RemoveIssuerProfileLogoService>();
        services.AddScoped<SearchFiscalReceiversService>();
        services.AddScoped<GetFiscalReceiverByRfcService>();
        services.AddScoped<CreateFiscalReceiverService>();
        services.AddScoped<UpdateFiscalReceiverService>();
        services.AddScoped<PreviewFiscalReceiverImportFromExcelService>();
        services.AddScoped<GetFiscalReceiverImportBatchService>();
        services.AddScoped<ListFiscalReceiverImportRowsService>();
        services.AddScoped<ApplyFiscalReceiverImportBatchService>();
        services.AddScoped<SearchProductFiscalProfilesService>();
        services.AddScoped<GetProductFiscalProfileByInternalCodeService>();
        services.AddScoped<CreateProductFiscalProfileService>();
        services.AddScoped<UpdateProductFiscalProfileService>();
        services.AddScoped<PreviewProductFiscalProfileImportFromExcelService>();
        services.AddScoped<GetProductFiscalProfileImportBatchService>();
        services.AddScoped<ListProductFiscalProfileImportRowsService>();
        services.AddScoped<ApplyProductFiscalProfileImportBatchService>();
        return services;
    }
}
