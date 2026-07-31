using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using SmartEGov.Application.Interfaces;
using SmartEGov.Infrastructure.Services;
using Xunit;

namespace SmartEGov.Tests.Services;

public class DocumentServiceValidateFileTests
{
    private static DocumentService CreateService(
        out Mock<IUnitOfWork> unitOfWork,
        out Mock<IWebHostEnvironment> environment)
    {
        unitOfWork = new Mock<IUnitOfWork>();
        environment = new Mock<IWebHostEnvironment>();
        var mapper = new Mock<IMapper>();
        var fileValidationService = new Mock<IFileValidationService>();

        return new DocumentService(unitOfWork.Object, environment.Object, mapper.Object, fileValidationService.Object);
    }

    private static IFormFile CreateFormFile(string fileName, long length = 1024, string contentType = "application/pdf")
    {
        var formFile = new Mock<IFormFile>();
        formFile.Setup(f => f.FileName).Returns(fileName);
        formFile.Setup(f => f.Length).Returns(length);
        formFile.Setup(f => f.ContentType).Returns(contentType);
        return formFile.Object;
    }

    [Fact]
    public void ValidateFile_RejectsEmptyFile()
    {
        var service = CreateService(out _, out _);
        var file = CreateFormFile("resume.pdf", length: 0);

        var (isValid, error) = service.ValidateFile(file, maxSizeBytes: 10 * 1024 * 1024, allowedExtensions: new[] { ".pdf" });

        Assert.False(isValid);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateFile_RejectsFileOverMaxSize()
    {
        var service = CreateService(out _, out _);
        var file = CreateFormFile("large.pdf", length: 20 * 1024 * 1024);

        var (isValid, error) = service.ValidateFile(file, maxSizeBytes: 10 * 1024 * 1024, allowedExtensions: new[] { ".pdf" });

        Assert.False(isValid);
        Assert.Contains("exceeds", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("script.js")]
    [InlineData("archive.zip")]
    public void ValidateFile_RejectsDisallowedExtensions(string fileName)
    {
        var service = CreateService(out _, out _);
        var file = CreateFormFile(fileName);

        var (isValid, error) = service.ValidateFile(file, maxSizeBytes: 10 * 1024 * 1024, allowedExtensions: new[] { ".pdf", ".jpg", ".png" });

        Assert.False(isValid);
        Assert.Contains("not allowed", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("id-card.pdf")]
    [InlineData("photo.jpg")]
    [InlineData("photo.PNG")]
    public void ValidateFile_AcceptsAllowedExtensions(string fileName)
    {
        var service = CreateService(out _, out _);
        var file = CreateFormFile(fileName);

        var (isValid, error) = service.ValidateFile(file, maxSizeBytes: 10 * 1024 * 1024, allowedExtensions: new[] { ".pdf", ".jpg", ".png" });

        Assert.True(isValid);
        Assert.Null(error);
    }
}
