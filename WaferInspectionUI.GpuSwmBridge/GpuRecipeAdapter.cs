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
                Wafer = BuildWafer(input, request), InspectionParameter = BuildInspection(input, request),
                ModeSetting = BuildModeSetting(input),
                FieldType = new FieldView
                {
                    Field = Field.BrightField, Lens = ParseLens(input.Inspection.LensMode),
                    FieldOfViewWidth = request.ImageWidth * request.DetectionMicronPerPixelX,
                    FieldOfViewHeight = request.ImageHeight * request.DetectionMicronPerPixelY,
                    OpticalFilterPositions = new int[0]
                }
            };

            foreach (PadRectInput pad in input.PadRects)
            {
                // 产线 RecipeConverter 直接传递 LimitAreaPara 中的
                // PadRect + Offset 像素坐标给 SWM；GoldenDie 仅用于编辑显示，
                // 不能在规划阶段按倍率再次换算。
                recipe.ProbeMarkRect.Add(new Rectangle(pad.X, pad.Y, pad.Width, pad.Height));
            }

            // The offline image folders are emitted by the production host's
            // ScanSequence.  Preserve the recipe's route type (notably
            // OnlyDie) instead of silently replanning it as MixPathType.
            ((SurfaceAOISetting)recipe.ModeSetting).CalcSettingWithPattern_New(
                recipe,
                0,
                0,
                // The production 2X setup enables virtual dies.  Keep this
                // argument aligned with InspectFlow instead of changing the
                // route planner's production geometry in the bridge.
                true,
                ParsePathType(input.Inspection.RoutePathType));
            return new RecipeAdapterResult { CameraParameters = new CameraParameters { Recipe = recipe, Width = request.ImageWidth, Height = request.ImageHeight, Filter = Field.BrightField } };
        }

        private static AutoReviewSystem.Data.Wafer BuildWafer(RecipeInputData input, BridgeRequest request)
        {
            WaferInput source = input.Wafer;
            var wafer = new AutoReviewSystem.Data.Wafer
            {
                Diameter = source.Diameter, EdgeLoss = source.EdgeLoss, ChrEdgeLoss = source.ChrEdgeLoss,
                DieWidth = source.DieWidth, DieHeight = source.DieHeight, ScribeLaneX = source.ScribeLaneX, ScribeLaneY = source.ScribeLaneY,
                _DieOrigin = new RealPoint(source.DieOriginX, source.DieOriginY),
                _SampleCenterLocation = new RealPoint(
                    request.SampleCenterLocationX,
                    request.SampleCenterLocationY),
                _SampleTestPlan = input.TestPlan.Select(p => new Point(p.X, p.Y)).ToList(),
                CustomedStartX = source.CustomedStartX, CustomedStartY = source.CustomedStartY,
                RowsCount = checked((uint)source.CustomedRowsCount), ColumnsCount = checked((uint)source.CustomedColsCount)
            };
            // Production RecipeConverter keeps the recipe's full pitch precision.
            wafer.DiePitchX = source.DieWidth + source.ScribeLaneX;
            wafer.DiePitchY = source.DieHeight + source.ScribeLaneY;
            if (wafer._SampleTestPlan.Count <= 0 || source.IsCreateDefaultPlan)
                wafer.CreateDefaultPlan();
            wafer.CreateDieMap();
            return wafer;
        }

        private static WSTICParameters BuildInspection(RecipeInputData input, BridgeRequest request)
        {
            InspectionInput source = input.Inspection;
            return new WSTICParameters
            {
                BrightBlackJudge = source.BrightBlackJudge, BackThr = source.BackThr, DeltaBlack = source.DeltaBlack,
                DeltaThreashold = source.DeltaThreshold, CThreashold = source.ColorThreshold, SurfaceOpen = source.SurfaceOpen,
                MinDetectWidth = source.MinWidth, MinDetectHeight = source.MinHeight, MinDetectArea = source.MinArea,
                ThresholdValue = source.Threshold, IsEnablePadErase = source.PadEraseMode != 0, ErodeValue = source.ErodeValue,
                OpenValue = source.OpenValue, IsEnableResultImageSave = request.IsEnableResultImageSave, ShiftJudge = source.ShiftJudge,
                MinDetectRatio = source.MinDetectRatio, ContourRemoval = source.ContourRemoval
            };
        }

        private static SurfaceAOISetting BuildModeSetting(RecipeInputData input)
        {
            InspectionInput source = input.Inspection;
            return new SurfaceAOISetting { AFRangeEnabled = source.AfRangeEnabled, AFRange = source.AfRange, Velocity = source.Velocity, Accel = source.Accel, Decel = source.Decel, Jerk = source.Jerk, AccelDistance = source.AccelDistance, DecelDistance = source.DecelDistance };
        }
        private static Lens ParseLens(string value) { Lens lens; if (!Enum.TryParse(value, true, out lens)) throw new InvalidOperationException("Unsupported inspection lens: " + value); return lens; }
        private static PathType ParsePathType(string value)
        {
            PathType pathType;
            if (!Enum.TryParse(value, true, out pathType))
                throw new InvalidDataException("Unsupported inspection RoutePathType: " + value);
            return pathType;
        }
    }
}
