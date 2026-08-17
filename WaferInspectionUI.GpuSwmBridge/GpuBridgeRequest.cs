using System;
using System.IO;

namespace SWM
{
    // This request intentionally belongs to the GPU bridge.  Its serialized
    // settings become the GPU InspectionParam header in Parameters.GetParam.
    public sealed class BridgeRequest
    {
        public string RecipePath { get; set; }
        public string ImageRoot { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public double DetectionMicronPerPixelX { get; set; }
        public double DetectionMicronPerPixelY { get; set; }
        public double RegionAlignmentOffsetX { get; set; }
        public double RegionAlignmentOffsetY { get; set; }
        public string ResponsePath { get; set; }

        public int BrightBlackJudge { get; set; }
        public int BackThr { get; set; }
        public int DeltaBlack { get; set; }
        public bool IsEnableResultImageSave { get; set; }

        public void Validate()
        {
            RequireText(RecipePath, "RecipePath");
            RequireText(ImageRoot, "ImageRoot");
            RequireText(ResponsePath, "ResponsePath");
            if (ImageWidth <= 0 || ImageHeight <= 0)
                throw new InvalidDataException("ImageWidth and ImageHeight must be greater than zero.");
            if (!IsPositiveFinite(DetectionMicronPerPixelX) ||
                !IsPositiveFinite(DetectionMicronPerPixelY))
            {
                throw new InvalidDataException(
                    "DetectionMicronPerPixelX and DetectionMicronPerPixelY must be explicitly configured.");
            }
            if (!IsFinite(RegionAlignmentOffsetX) || !IsFinite(RegionAlignmentOffsetY))
                throw new InvalidDataException("RegionAlignmentOffset values must be finite.");
            if (BrightBlackJudge < 0 || BrightBlackJudge > 2 || BackThr < 0 || DeltaBlack < 0)
                throw new InvalidDataException("GPU inspection settings are invalid.");
        }

        private static void RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException(name + " is required.");
        }

        private static bool IsPositiveFinite(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
