using System;
using System.Drawing;
using System.IO;
using System.Linq;
using AutoReviewSystem.Data;

namespace SWM
{
    public sealed class RecipeAdapterResult { public CameraParameters CameraParameters { get; set; } }

    public static class RecipeAdapter
    {
        public static RecipeAdapterResult Build(BridgeRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Validate();
            RecipeInputData input = RecipeInputLoader.Load(request.RecipePath);
            var recipe = new Recipe(RunMode.SurfaceAOI, InspectionMode.WSTIC)
            {
                Created = DateTime.Now, ImageCount = input.Inspection.ImageCount, Path = request.RecipePath,
                SaveResultImage = false, ResultImagePath = request.RecipePath,
                Wafer = BuildWafer(input), InspectionParameter = BuildInspection(input, request),
                ModeSetting = BuildModeSetting(input),
                FieldType = new FieldView
                {
                    Field = Field.BrightField, Lens = ParseLens(input.Inspection.LensMode),
                    FieldOfViewWidth = request.ImageWidth * request.DetectionMicronPerPixelX,
                    FieldOfViewHeight = request.ImageHeight * request.DetectionMicronPerPixelY,
                    OpticalFilterPositions = new int[0]
                }
            };

            Size goldenSize = ReadGoldenDieSize(request.RecipePath);
            foreach (PadRectInput pad in input.PadRects)
                recipe.ProbeMarkRect.Add(ProjectGoldenPad(pad, goldenSize, input.Wafer, request));

            ((SurfaceAOISetting)recipe.ModeSetting).CalcSettingWithPattern_New(recipe, 0, 0, false, PathType.MixPathType);
            return new RecipeAdapterResult { CameraParameters = new CameraParameters { Recipe = recipe, Width = request.ImageWidth, Height = request.ImageHeight, Filter = Field.BrightField } };
        }

        private static AutoReviewSystem.Data.Wafer BuildWafer(RecipeInputData input)
        {
            WaferInput source = input.Wafer;
            var wafer = new AutoReviewSystem.Data.Wafer
            {
                Diameter = source.Diameter, EdgeLoss = source.EdgeLoss, ChrEdgeLoss = source.ChrEdgeLoss,
                DieWidth = source.DieWidth, DieHeight = source.DieHeight, ScribeLaneX = source.ScribeLaneX, ScribeLaneY = source.ScribeLaneY,
                _DieOrigin = new RealPoint(source.DieOriginX, source.DieOriginY), _SampleCenterLocation = new RealPoint(0, 0),
                _SampleTestPlan = input.TestPlan.Select(p => new Point(p.X, p.Y)).ToList()
            };
            wafer.DiePitchX = Math.Round(source.DieWidth + source.ScribeLaneX, 5);
            wafer.DiePitchY = Math.Round(source.DieHeight + source.ScribeLaneY, 5);
            wafer.Diameter = 10000;
            wafer.CreateDieMap();
            return wafer;
        }

        private static WSTICParameters BuildInspection(RecipeInputData input, BridgeRequest request)
        {
            InspectionInput source = input.Inspection;
            return new WSTICParameters
            {
                BrightBlackJudge = request.BrightBlackJudge, BackThr = request.BackThr, DeltaBlack = request.DeltaBlack,
                DeltaThreashold = source.DeltaThreshold, CThreashold = source.ColorThreshold, SurfaceOpen = source.SurfaceOpen,
                MinDetectWidth = source.MinWidth, MinDetectHeight = source.MinHeight, MinDetectArea = source.MinArea,
                ThresholdValue = source.Threshold, IsEnablePadErase = source.PadEraseMode != 0, ErodeValue = source.ErodeValue,
                OpenValue = source.OpenValue, IsEnableResultImageSave = request.IsEnableResultImageSave
            };
        }

        private static SurfaceAOISetting BuildModeSetting(RecipeInputData input)
        {
            InspectionInput source = input.Inspection;
            return new SurfaceAOISetting { AFRangeEnabled = source.AfRangeEnabled, AFRange = source.AfRange, Velocity = source.Velocity, Accel = source.Accel, Decel = source.Decel, Jerk = source.Jerk, AccelDistance = source.AccelDistance, DecelDistance = source.DecelDistance };
        }
        private static Rectangle ProjectGoldenPad(PadRectInput pad, Size golden, WaferInput wafer, BridgeRequest request)
        {
            if (golden.Width <= 0 || golden.Height <= 0) throw new InvalidDataException("GoldenDie image size is invalid.");
            double scaleX = wafer.DieWidth * 1000.0 / request.DetectionMicronPerPixelX / golden.Width;
            double scaleY = wafer.DieHeight * 1000.0 / request.DetectionMicronPerPixelY / golden.Height;
            return new Rectangle((int)Math.Round(pad.X * scaleX), (int)Math.Round(pad.Y * scaleY), Math.Max(1, (int)Math.Round(pad.Width * scaleX)), Math.Max(1, (int)Math.Round(pad.Height * scaleY)));
        }
        private static Lens ParseLens(string value) { Lens lens; if (!Enum.TryParse(value, true, out lens)) throw new InvalidOperationException("Unsupported inspection lens: " + value); return lens; }
        private static Size ReadGoldenDieSize(string recipePath)
        {
            string directory = Path.Combine(recipePath, "GoldenDie");
            string imagePath = Directory.Exists(directory) ? Directory.GetFiles(directory, "SourceDie*.bmp", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault() : null;
            if (imagePath == null) throw new FileNotFoundException("GoldenDie source image is missing.", directory);
            using (Image image = Image.FromFile(imagePath)) return image.Size;
        }
    }
}
