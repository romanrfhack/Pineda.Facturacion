using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Storage;
using Pineda.Facturacion.Application.UseCases.IssuerProfiles;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.Documents;
using Pineda.Facturacion.Infrastructure.Options;

namespace Pineda.Facturacion.UnitTests;

public class IssuerProfileLogoServicesTests
{
    [Fact]
    public async Task UploadIssuerProfileLogo_Saves_Metadata_And_Binary_Content()
    {
        var rootPath = CreateTempPath();
        var repository = new FakeIssuerProfileRepository
        {
            Existing = new IssuerProfile
            {
                Id = 4,
                LegalName = "Emisor Demo",
                Rfc = "AAA010101AAA",
                FiscalRegimeCode = "601",
                PostalCode = "01000",
                CfdiVersion = "4.0",
                CertificateReference = "CERT",
                PrivateKeyReference = "KEY",
                PrivateKeyPasswordReference = "PWD",
                PacEnvironment = "Sandbox",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }
        };
        var storage = CreateStorage(rootPath);
        var service = new UploadIssuerProfileLogoService(repository, storage, new FakeUnitOfWork());

        var result = await service.ExecuteAsync(4, "logo.png", "image/png", ValidPngBytes());

        Assert.Equal(UploadIssuerProfileLogoOutcome.Updated, result.Outcome);
        Assert.Null(repository.Existing!.LogoStoragePath);
        Assert.Equal(ValidPngBytes(), repository.Existing.LogoData);
        Assert.Equal(ValidPngBytes().Length, repository.Existing.LogoSizeBytes);
        Assert.Equal("logo.png", repository.Existing.LogoFileName);
        Assert.Equal("image/png", repository.Existing.LogoContentType);
        Assert.False(Directory.Exists(Path.Combine(rootPath, "issuer-logos", "4")));

        var getResult = await new GetIssuerProfileLogoService(repository, storage).ExecuteAsync(4);
        Assert.Equal(GetIssuerProfileLogoOutcome.Found, getResult.Outcome);
        Assert.Equal("image/png", getResult.ContentType);
        Assert.Equal(ValidPngBytes(), getResult.Content);
    }

    [Fact]
    public async Task UploadIssuerProfileLogo_Accepts_Jpeg_Content()
    {
        var repository = new FakeIssuerProfileRepository { Existing = CreateIssuerProfile() };
        var service = new UploadIssuerProfileLogoService(
            repository,
            CreateStorage(CreateTempPath()),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(4, "..\\unsafe\\logo.jpg", "image/jpeg", ValidJpegBytes());

        Assert.Equal(UploadIssuerProfileLogoOutcome.Updated, result.Outcome);
        Assert.Equal(ValidJpegBytes(), repository.Existing!.LogoData);
        Assert.Equal("logo.jpg", repository.Existing.LogoFileName);
        Assert.Equal("image/jpeg", repository.Existing.LogoContentType);
    }

    [Fact]
    public async Task UploadIssuerProfileLogo_Rejects_Empty_File()
    {
        var service = new UploadIssuerProfileLogoService(
            new FakeIssuerProfileRepository { Existing = CreateIssuerProfile() },
            CreateStorage(CreateTempPath()),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(4, "logo.png", "image/png", []);

        Assert.Equal(UploadIssuerProfileLogoOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("vacío", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadIssuerProfileLogo_Rejects_Mismatched_Content_Type()
    {
        var service = new UploadIssuerProfileLogoService(
            new FakeIssuerProfileRepository { Existing = CreateIssuerProfile() },
            CreateStorage(CreateTempPath()),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(4, "logo.png", "text/plain", ValidPngBytes());

        Assert.Equal(UploadIssuerProfileLogoOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("formato declarado", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadIssuerProfileLogo_Rejects_Invalid_Format()
    {
        var service = new UploadIssuerProfileLogoService(
            new FakeIssuerProfileRepository { Existing = CreateIssuerProfile() },
            CreateStorage(CreateTempPath()),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(4, "logo.txt", "text/plain", "not-an-image"u8.ToArray());

        Assert.Equal(UploadIssuerProfileLogoOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("PNG", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadIssuerProfileLogo_Rejects_Webp_Format()
    {
        var service = new UploadIssuerProfileLogoService(
            new FakeIssuerProfileRepository { Existing = CreateIssuerProfile() },
            CreateStorage(CreateTempPath()),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(
            4,
            "logo.webp",
            "image/webp",
            [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50]);

        Assert.Equal(UploadIssuerProfileLogoOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("PNG", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadIssuerProfileLogo_Rejects_Images_That_Exceed_Max_Size()
    {
        var service = new UploadIssuerProfileLogoService(
            new FakeIssuerProfileRepository { Existing = CreateIssuerProfile() },
            CreateStorage(CreateTempPath()),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(4, "logo.png", "image/png", ValidPngBytes().Concat(new byte[1_048_577]).ToArray());

        Assert.Equal(UploadIssuerProfileLogoOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("tamaño máximo", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadIssuerProfileLogo_Replaces_Previous_Logo_Data()
    {
        var repository = new FakeIssuerProfileRepository { Existing = CreateIssuerProfile() };
        var service = new UploadIssuerProfileLogoService(repository, CreateStorage(CreateTempPath()), new FakeUnitOfWork());

        var first = await service.ExecuteAsync(4, "logo.png", "image/png", ValidPngBytes());
        var second = await service.ExecuteAsync(4, "logo.jpg", "image/jpeg", ValidJpegBytes());

        Assert.Equal(UploadIssuerProfileLogoOutcome.Updated, first.Outcome);
        Assert.Equal(UploadIssuerProfileLogoOutcome.Updated, second.Outcome);
        Assert.Equal(ValidJpegBytes(), repository.Existing!.LogoData);
        Assert.Equal(ValidJpegBytes().Length, repository.Existing.LogoSizeBytes);
        Assert.Equal("logo.jpg", repository.Existing.LogoFileName);
        Assert.Equal("image/jpeg", repository.Existing.LogoContentType);
    }

    [Fact]
    public async Task GetIssuerProfileLogo_Falls_Back_To_Legacy_StoragePath_When_Blob_Is_Missing()
    {
        var storage = CreateStorage(CreateTempPath());
        var storageResult = await storage.SaveAsync(4, "legacy.png", "image/png", ValidPngBytes());
        var issuerProfile = CreateIssuerProfile();
        issuerProfile.LogoStoragePath = storageResult.StoragePath;
        issuerProfile.LogoFileName = storageResult.FileName;
        issuerProfile.LogoContentType = storageResult.ContentType;
        var repository = new FakeIssuerProfileRepository
        {
            Existing = issuerProfile
        };

        var getResult = await new GetIssuerProfileLogoService(repository, storage).ExecuteAsync(4);

        Assert.Equal(GetIssuerProfileLogoOutcome.Found, getResult.Outcome);
        Assert.Equal(ValidPngBytes(), getResult.Content);
        Assert.Equal("legacy.png", getResult.FileName);
    }

    [Fact]
    public async Task GetIssuerProfileLogo_Returns_NotFound_When_Only_Legacy_Path_Is_Missing()
    {
        var issuerProfile = CreateIssuerProfile();
        issuerProfile.LogoStoragePath = "4/missing.png";
        issuerProfile.LogoFileName = "missing.png";
        issuerProfile.LogoContentType = "image/png";
        var repository = new FakeIssuerProfileRepository
        {
            Existing = issuerProfile
        };

        var getResult = await new GetIssuerProfileLogoService(repository, CreateStorage(CreateTempPath())).ExecuteAsync(4);

        Assert.Equal(GetIssuerProfileLogoOutcome.NotFound, getResult.Outcome);
    }

    [Fact]
    public async Task RemoveIssuerProfileLogo_Clears_Metadata_And_Deletes_File()
    {
        var storage = CreateStorage(CreateTempPath());
        var storageResult = await storage.SaveAsync(4, "legacy.png", "image/png", ValidPngBytes());
        var issuerProfile = CreateIssuerProfile();
        issuerProfile.LogoStoragePath = storageResult.StoragePath;
        issuerProfile.LogoData = ValidPngBytes();
        issuerProfile.LogoSizeBytes = ValidPngBytes().Length;
        issuerProfile.LogoFileName = storageResult.FileName;
        issuerProfile.LogoContentType = storageResult.ContentType;
        issuerProfile.LogoUpdatedAtUtc = DateTime.UtcNow;
        var repository = new FakeIssuerProfileRepository
        {
            Existing = issuerProfile
        };

        var removeResult = await new RemoveIssuerProfileLogoService(repository, storage, new FakeUnitOfWork()).ExecuteAsync(4);

        Assert.Equal(RemoveIssuerProfileLogoOutcome.Removed, removeResult.Outcome);
        Assert.Null(repository.Existing.LogoStoragePath);
        Assert.Null(repository.Existing.LogoData);
        Assert.Null(repository.Existing.LogoSizeBytes);

        var getResult = await new GetIssuerProfileLogoService(repository, storage).ExecuteAsync(4);
        Assert.Equal(GetIssuerProfileLogoOutcome.NotFound, getResult.Outcome);
    }

    [Fact]
    public async Task UpdateIssuerProfile_Preserves_Existing_Logo_Blob()
    {
        var repository = new FakeIssuerProfileRepository { Existing = CreateIssuerProfile() };
        repository.Existing.LogoData = ValidPngBytes();
        repository.Existing.LogoSizeBytes = ValidPngBytes().Length;
        repository.Existing.LogoFileName = "logo.png";
        repository.Existing.LogoContentType = "image/png";
        repository.Existing.LogoUpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        var originalLogoUpdatedAtUtc = repository.Existing.LogoUpdatedAtUtc;
        var service = new UpdateIssuerProfileService(repository, new FakeFiscalDocumentRepository(), new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new UpdateIssuerProfileCommand
        {
            Id = 4,
            LegalName = "Emisor Actualizado",
            Rfc = "AAA010101AAA",
            FiscalRegimeCode = "601",
            PostalCode = "01000",
            CfdiVersion = "4.0",
            CertificateReference = "CERT",
            PrivateKeyReference = "KEY",
            PrivateKeyPasswordReference = "PWD",
            PacEnvironment = "Sandbox",
            FiscalSeries = "A",
            NextFiscalFolio = 31788,
            IsActive = true
        });

        Assert.Equal(UpdateIssuerProfileOutcome.Updated, result.Outcome);
        Assert.Equal("Emisor Actualizado", repository.Existing.LegalName);
        Assert.Equal(ValidPngBytes(), repository.Existing.LogoData);
        Assert.Equal(ValidPngBytes().Length, repository.Existing.LogoSizeBytes);
        Assert.Equal("logo.png", repository.Existing.LogoFileName);
        Assert.Equal("image/png", repository.Existing.LogoContentType);
        Assert.Equal(originalLogoUpdatedAtUtc, repository.Existing.LogoUpdatedAtUtc);
    }

    private static IIssuerProfileLogoStorage CreateStorage(string rootPath)
    {
        return new IssuerProfileLogoStorage(
            new FakeHostEnvironment(rootPath),
            Options.Create(new IssuerLogoStorageOptions
            {
                RootPath = "issuer-logos",
                MaxFileSizeBytes = 1_048_576
            }));
    }

    private static IssuerProfile CreateIssuerProfile()
    {
        return new IssuerProfile
        {
            Id = 4,
            LegalName = "Emisor Demo",
            Rfc = "AAA010101AAA",
            FiscalRegimeCode = "601",
            PostalCode = "01000",
            CfdiVersion = "4.0",
            CertificateReference = "CERT",
            PrivateKeyReference = "KEY",
            PrivateKeyPasswordReference = "PWD",
            PacEnvironment = "Sandbox",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static byte[] ValidPngBytes()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01
        ];
    }

    private static byte[] ValidJpegBytes()
    {
        return [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    }

    private static string CreateTempPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "pineda-facturacion-logo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeIssuerProfileRepository : IIssuerProfileRepository
    {
        public IssuerProfile? Existing { get; set; }

        public Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Existing);

        public Task<IssuerProfile?> GetTrackedActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Existing);

        public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing?.Id == issuerProfileId ? Existing : null);

        public Task<bool> TryAdvanceNextFiscalFolioAsync(long issuerProfileId, int expectedNextFiscalFolio, int newNextFiscalFolio, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default)
        {
            Existing = issuerProfile;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalDocument?>(null);

        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalDocument?>(null);

        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalDocument?>(null);

        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(
            string issuerRfc,
            string series,
            string folio,
            long? excludeFiscalDocumentId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Pineda.Facturacion.Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
