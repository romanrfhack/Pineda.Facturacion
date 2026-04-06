using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

public class BillingDbContext : DbContext, IUnitOfWork
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options)
        : base(options)
    {
    }

    public DbSet<LegacyImportRecord> LegacyImportRecords => Set<LegacyImportRecord>();

    public DbSet<LegacyImportRevision> LegacyImportRevisions => Set<LegacyImportRevision>();

    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();

    public DbSet<BillingDocument> BillingDocuments => Set<BillingDocument>();

    public DbSet<BillingDocumentItem> BillingDocumentItems => Set<BillingDocumentItem>();

    public DbSet<BillingDocumentItemRemoval> BillingDocumentItemRemovals => Set<BillingDocumentItemRemoval>();

    public DbSet<BillingDocumentPendingItemAssignment> BillingDocumentPendingItemAssignments => Set<BillingDocumentPendingItemAssignment>();

    public DbSet<FiscalDocument> FiscalDocuments => Set<FiscalDocument>();

    public DbSet<FiscalDocumentItem> FiscalDocumentItems => Set<FiscalDocumentItem>();

    public DbSet<FiscalStamp> FiscalStamps => Set<FiscalStamp>();

    public DbSet<FiscalCancellation> FiscalCancellations => Set<FiscalCancellation>();

    public DbSet<AccountsReceivableInvoice> AccountsReceivableInvoices => Set<AccountsReceivableInvoice>();

    public DbSet<AccountsReceivablePayment> AccountsReceivablePayments => Set<AccountsReceivablePayment>();

    public DbSet<AccountsReceivablePaymentApplication> AccountsReceivablePaymentApplications => Set<AccountsReceivablePaymentApplication>();

    public DbSet<CollectionCommitment> CollectionCommitments => Set<CollectionCommitment>();

    public DbSet<CollectionNote> CollectionNotes => Set<CollectionNote>();

    public DbSet<PaymentComplementDocument> PaymentComplementDocuments => Set<PaymentComplementDocument>();

    public DbSet<PaymentComplementRelatedDocument> PaymentComplementRelatedDocuments => Set<PaymentComplementRelatedDocument>();

    public DbSet<PaymentComplementStamp> PaymentComplementStamps => Set<PaymentComplementStamp>();

    public DbSet<PaymentComplementCancellation> PaymentComplementCancellations => Set<PaymentComplementCancellation>();

    public DbSet<InternalRepBaseDocumentState> InternalRepBaseDocumentStates => Set<InternalRepBaseDocumentState>();

    public DbSet<ExternalRepBaseDocument> ExternalRepBaseDocuments => Set<ExternalRepBaseDocument>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<AppRole> AppRoles => Set<AppRole>();

    public DbSet<AppUserRole> AppUserRoles => Set<AppUserRole>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<IssuerProfile> IssuerProfiles => Set<IssuerProfile>();

    public DbSet<FiscalReceiver> FiscalReceivers => Set<FiscalReceiver>();

    public DbSet<FiscalReceiverSpecialFieldDefinition> FiscalReceiverSpecialFieldDefinitions => Set<FiscalReceiverSpecialFieldDefinition>();

    public DbSet<ProductFiscalProfile> ProductFiscalProfiles => Set<ProductFiscalProfile>();

    public DbSet<SatProductServiceCatalogEntry> SatProductServiceCatalogEntries => Set<SatProductServiceCatalogEntry>();

    public DbSet<FiscalDocumentSpecialFieldValue> FiscalDocumentSpecialFieldValues => Set<FiscalDocumentSpecialFieldValue>();

    public DbSet<FiscalReceiverImportBatch> FiscalReceiverImportBatches => Set<FiscalReceiverImportBatch>();

    public DbSet<FiscalReceiverImportRow> FiscalReceiverImportRows => Set<FiscalReceiverImportRow>();

    public DbSet<ProductFiscalProfileImportBatch> ProductFiscalProfileImportBatches => Set<ProductFiscalProfileImportBatch>();

    public DbSet<ProductFiscalProfileImportRow> ProductFiscalProfileImportRows => Set<ProductFiscalProfileImportRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        await SaveChangesAsync(cancellationToken);
    }
}
