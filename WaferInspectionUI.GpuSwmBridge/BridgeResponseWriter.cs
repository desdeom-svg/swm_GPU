using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SWM
{
    public sealed class BridgeResponse
    {
        public List<BridgeRow> Rows { get; set; } = new List<BridgeRow>();
    }

    public sealed class BridgeRow
    {
        public int SliceIndex { get; set; }
        public int Repeat { get; set; }
        public double[] InspectionParam { get; set; } = Array.Empty<double>();
        public List<BridgeImage> Images { get; set; } = new List<BridgeImage>();
    }

    public sealed class BridgeImage
    {
        public int ImageIndex { get; set; }
        public int ReferenceImage1 { get; set; }
        public int ReferenceImage2 { get; set; }
        public double[] RegionParam { get; set; } = Array.Empty<double>();

        // Physical die-grid cell in recipe map coordinates (millimetres).
        // Version 2 consumers use it to render the same spatial map as the
        // production MapImage instead of treating capture rows as geometry.
        public double MapX { get; set; } = double.NaN;
        public double MapY { get; set; } = double.NaN;
        public double MapWidth { get; set; } = double.NaN;
        public double MapHeight { get; set; } = double.NaN;
    }

    public static class BridgeResponseWriter
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SWMR");
        private const int FormatVersion = 2;

        public static void Write(string path, BridgeResponse response)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Response path is required.", nameof(path));

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = fullPath + ".tmp";
            using (var stream = File.Create(temporaryPath))
                Write(stream, response);

            if (File.Exists(fullPath))
                File.Replace(temporaryPath, fullPath, null);
            else
                File.Move(temporaryPath, fullPath);
        }

        public static void Write(Stream stream, BridgeResponse response)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(response.Rows.Count);
                foreach (BridgeRow row in response.Rows)
                {
                    writer.Write(row.SliceIndex);
                    writer.Write(row.Repeat);
                    WriteDoubleArray(writer, row.InspectionParam);
                    writer.Write(row.Images.Count);
                    foreach (BridgeImage image in row.Images)
                    {
                        writer.Write(image.ImageIndex);
                        writer.Write(image.ReferenceImage1);
                        writer.Write(image.ReferenceImage2);
                        WriteDoubleArray(writer, image.RegionParam);
                        writer.Write(image.MapX);
                        writer.Write(image.MapY);
                        writer.Write(image.MapWidth);
                        writer.Write(image.MapHeight);
                    }
                }
            }
        }

        private static void WriteDoubleArray(BinaryWriter writer, double[] values)
        {
            values = values ?? Array.Empty<double>();
            writer.Write(values.Length);
            foreach (double value in values)
                writer.Write(value);
        }
    }
}
