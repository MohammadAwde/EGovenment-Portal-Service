using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;
using SmartEGov.Infrastructure.Services;
using Xunit;

namespace SmartEGov.Tests.Services;

/// <summary>
/// Regression coverage for the path-traversal fix: a document's stored FileName/FilePath
/// must never contain directory-traversal segments from an attacker-supplied file name,
/// and the file must always end up physically inside the uploads folder.
/// </summary>
public class DocumentServiceUploadTests : IDisposable
{
    private readonly string _tempWebRoot;

    public DocumentServiceUploadTests()
    {
        _tempWebRoot = Path.Combine(Path.GetTempPath(), "smartegov-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempWebRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempWebRoot))
        {
            Directory.Delete(_tempWebRoot, recursive: true);
        }
    }

    private static IFormFile CreateFormFile(string fileName, string content = "test-content")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var formFile = new Mock<IFormFile>();
        formFile.Setup(f => f.FileName).Returns(fileName);
        formFile.Setup(f => f.Length).Returns(bytes.Length);
        formFile.Setup(f => f.ContentType).Returns("application/pdf");
        formFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, CancellationToken>((target, _) => stream.CopyToAsync(target));
        return formFile.Object;
    }

    [Theory]
    [InlineData("../../../../etc/evil.pdf")]
    [InlineData("..\\..\\..\\Windows\\evil.pdf")]
    [InlineData("../../wwwroot/web.config.pdf")]
    public async Task UploadAsync_SanitizesTraversalAttemptsInFileName(string maliciousFileName)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var documentsRepo = new Mock<IDocumentRepository>();
        unitOfWork.SetupGet(u => u.Documents).Returns(documentsRepo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.WebRootPath).Returns(_tempWebRoot);

        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<DocumentDto>(It.IsAny<Document>()))
            .Returns((Document d) => new DocumentDto { Id = d.Id, FileName = d.FileName, FilePath = d.FilePath });

        var fileValidationService = new Mock<IFileValidationService>();
        fileValidationService
            .Setup(v => v.ValidateFileAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync((true, string.Empty));

        var service = new DocumentService(unitOfWork.Object, environment.Object, mapper.Object, fileValidationService.Object);
        var file = CreateFormFile(maliciousFileName);

        var result = await service.UploadAsync(serviceRequestId: 1, file);

        // The stored file name must not contain any traversal/path separators.
        Assert.DoesNotContain("..", result.FileName);
        Assert.DoesNotContain('/', result.FileName);
        Assert.DoesNotContain('\\', result.FileName);

        // The physical file must land inside the uploads folder, not escape it.
        var uploadsFolder = Path.Combine(_tempWebRoot, "uploads");
        var fullPath = Path.GetFullPath(Path.Combine(_tempWebRoot, result.FilePath!.TrimStart('/')));
        Assert.StartsWith(Path.GetFullPath(uploadsFolder), fullPath);
        Assert.True(File.Exists(fullPath));
    }
}
