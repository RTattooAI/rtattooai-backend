using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Tests.TestData;
using RTattooAI.Application.Validation;
using Xunit;

namespace RTattooAI.Application.Tests.Validation;

public class ImageValidatorTests
{
    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    public void EnsureAllowedUpload_Throws_WhenContentTypeIsNotAnAllowedImage(string contentType)
    {
        Assert.Throws<InvalidImageException>(() => ImageValidator.EnsureAllowedUpload(contentType, 1024));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ImageValidator.MaxImageBytes + 1)]
    public void EnsureAllowedUpload_Throws_WhenSizeIsOutOfRange(long size)
    {
        Assert.Throws<InvalidImageException>(() => ImageValidator.EnsureAllowedUpload("image/jpeg", size));
    }

    [Fact]
    public void EnsureValidContent_Throws_WhenBytesAreNotAnImage()
    {
        // Caso de ataque: archivo arbitrario enviado con Content-Type image/jpeg.
        Assert.Throws<InvalidImageException>(() => ImageValidator.EnsureValidContent(TestImages.NotAnImage()));
    }

    [Fact]
    public void EnsureValidContent_Throws_WhenContentIsEmpty()
    {
        Assert.Throws<InvalidImageException>(() => ImageValidator.EnsureValidContent([]));
    }

    [Fact]
    public void EnsureValidContent_DetectsPng_WithItsRealDimensions()
    {
        var result = ImageValidator.EnsureValidContent(TestImages.Png());

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal("png", result.Extension);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Fact]
    public void EnsureValidContent_DetectsJpeg_WithItsRealDimensions()
    {
        var result = ImageValidator.EnsureValidContent(TestImages.Jpeg());

        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal("jpg", result.Extension);
        Assert.Equal(8, result.Width);
        Assert.Equal(8, result.Height);
    }

    [Fact]
    public void EnsureValidContent_Throws_WhenPixelBudgetIsExceeded()
    {
        // PNG cuya cabecera declara 30000 x 30000 (900 MP): bomba de descompresión.
        var png = TestImages.Png();
        WriteBigEndian(png, 16, 30000);
        WriteBigEndian(png, 20, 30000);

        var ex = Assert.Throws<InvalidImageException>(() => ImageValidator.EnsureValidContent(png));
        Assert.Contains("resoluci", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureAllowedUpload_Accepts_Webp()
    {
        // WEBP esta en el RF-01 y es lo que produce la captura desde camara en varios navegadores.
        ImageValidator.EnsureAllowedUpload("image/webp", 1024);
    }

    [Fact]
    public void EnsureValidContent_DetectsWebp_WithItsRealDimensions()
    {
        var result = ImageValidator.EnsureValidContent(TestImages.Webp());

        Assert.Equal("image/webp", result.ContentType);
        Assert.Equal("webp", result.Extension);
        Assert.Equal(4, result.Width);
        Assert.Equal(4, result.Height);
    }

    [Fact]
    public void EnsureValidContent_UsesTheRealType_NotTheDeclaredOne()
    {
        // Un PNG enviado como .jpg se identifica como PNG: el tipo lo deciden los bytes.
        var result = ImageValidator.EnsureValidContent(TestImages.Png());

        Assert.NotEqual("image/jpeg", result.ContentType);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
