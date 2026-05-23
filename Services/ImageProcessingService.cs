using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public static class ImageProcessingService
    {
        public static byte[] EnhanceForVision(byte[] imageBytes, long jpegQuality = 88L)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return Array.Empty<byte>();

            using var input = new MemoryStream(imageBytes);
            using var source = new Bitmap(input);
            using var output = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(output))
            {
                var matrix = new ColorMatrix(new[]
                {
                    new[] { 1.28f, 0f,    0f,    0f, 0f },
                    new[] { 0f,    1.28f, 0f,    0f, 0f },
                    new[] { 0f,    0f,    1.28f, 0f, 0f },
                    new[] { 0f,    0f,    0f,    1f, 0f },
                    new[] { -0.08f, -0.08f, -0.08f, 0f, 1f }
                });
                using var attrs = new ImageAttributes();
                attrs.SetColorMatrix(matrix);
                g.DrawImage(source, new Rectangle(0, 0, output.Width, output.Height),
                    0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
            }

            using var ms = new MemoryStream();
            var encoder = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
            if (encoder == null)
            {
                output.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }

            using var encParams = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(Encoder.Quality, Math.Clamp(jpegQuality, 60L, 100L));
            output.Save(ms, encoder, encParams);
            return ms.ToArray();
        }
    }
}
