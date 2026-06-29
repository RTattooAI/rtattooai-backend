using RTattooAI.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace RTattooAI.Infrastructure.Imaging;

public class ImageSharpCropper : IImageCropper
{
    public byte[] CropToPng(byte[] imageBytes, int x, int y, int width, int height)
    {
        using var image = Image.Load(imageBytes);

        var boundedX = Math.Clamp(x, 0, Math.Max(image.Width - 1, 0));
        var boundedY = Math.Clamp(y, 0, Math.Max(image.Height - 1, 0));
        var boundedWidth = Math.Clamp(width, 1, image.Width - boundedX);
        var boundedHeight = Math.Clamp(height, 1, image.Height - boundedY);

        image.Mutate(ctx => ctx.Crop(new Rectangle(boundedX, boundedY, boundedWidth, boundedHeight)));

        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }

    public byte[] ResizeToThumbnailPng(byte[] imageBytes, int maxDimension)
    {
        using var image = Image.Load(imageBytes);

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maxDimension, maxDimension)
        }));

        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}
