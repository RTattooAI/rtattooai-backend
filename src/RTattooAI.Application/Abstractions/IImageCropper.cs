namespace RTattooAI.Application.Abstractions;

public interface IImageCropper
{
    byte[] CropToPng(byte[] imageBytes, int x, int y, int width, int height);

    byte[] ResizeToThumbnailPng(byte[] imageBytes, int maxDimension);
}
