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
        Assert.NotNull(repository.Existing!.LogoStoragePath);
        Assert.Equal("logo.png", repository.Existing.LogoFileName);
        Assert.Equal("image/png", repository.Existing.LogoContentType);

        var getResult = await new GetIssuerProfileLogoService(repository, storage).ExecuteAsync(4);
        Assert.Equal(GetIssuerProfileLogoOutcome.Found, getResult.Outcome);
        Assert.Equal("image/png", getResult.ContentType);
        Assert.Equal(ValidPngBytes(), getResult.Content);
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
        var storage = new IssuerProfileLogoStorage(
            new FakeHostEnvironment(CreateTempPath()),
            Options.Create(new IssuerLogoStorageOptions
            {
                RootPath = "issuer-logos",
                MaxFileSizeBytes = 8
            }));

        var service = new UploadIssuerProfileLogoService(
            new FakeIssuerProfileRepository { Existing = CreateIssuerProfile() },
            storage,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(4, "logo.png", "image/png", ValidPngBytes().Concat(new byte[32]).ToArray());

        Assert.Equal(UploadIssuerProfileLogoOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("tamaño máximo", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveIssuerProfileLogo_Clears_Metadata_And_Deletes_File()
    {
        var rootPath = CreateTempPath();
        var storage = CreateStorage(rootPath);
        var repository = new FakeIssuerProfileRepository { Existing = CreateIssuerProfile() };
        var uploadResult = await new UploadIssuerProfileLogoService(repository, storage, new FakeUnitOfWork())
            .ExecuteAsync(4, "logo.png", "image/png", ValidPngBytes());

        Assert.Equal(UploadIssuerProfileLogoOutcome.Updated, uploadResult.Outcome);
        Assert.NotNull(repository.Existing!.LogoStoragePath);

        var removeResult = await new RemoveIssuerProfileLogoService(repository, storage, new FakeUnitOfWork()).ExecuteAsync(4);

        Assert.Equal(RemoveIssuerProfileLogoOutcome.Removed, removeResult.Outcome);
        Assert.Null(repository.Existing.LogoStoragePath);

        var getResult = await new GetIssuerProfileLogoService(repository, storage).ExecuteAsync(4);
        Assert.Equal(GetIssuerProfileLogoOutcome.NotFound, getResult.Outcome);
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

        public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing?.Id == issuerProfileId ? Existing : null);

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
