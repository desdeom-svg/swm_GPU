using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SWM;

namespace SWM.RegressionTests
{
    internal static class Program
    {
        private static readonly MethodInfo RemoveIndexRefMethod =
            typeof(Parameters).GetMethod(
                "RemoveIndexRef",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static int Main()
        {
            if (RemoveIndexRefMethod == null)
            {
                Console.Error.WriteLine("FAIL: RemoveIndexRef method was not found.");
                return 1;
            }

            try
            {
                KeepsActiveTriggerThatCoversDownstreamImage();
                RemovesLeafTriggerAndTransfersCoverage();
                LoadsSerializedRecipeBytesWithoutReserialization();
                UsesRecipePadCoordinatesWithoutGoldenDieRescaling();
                CarriesProductionSampleCenterIntoSwmPlanning();
                Console.WriteLine("PASS: RemoveIndexRef regression tests.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL: {ex.Message}");
                return 1;
            }
        }

        private static void KeepsActiveTriggerThatCoversDownstreamImage()
        {
            var activeTriggers = new Dictionary<int, Dictionary<int, int>>
            {
                [65] = new Dictionary<int, int> { [60] = 70 },
                [75] = new Dictionary<int, int> { [65] = 70 }
            };
            var inspected = new bool[100];
            var modes = new int[100];

            // Trigger 65 already covers its ref2 (image 70).
            modes[65] = 2;

            bool removed = InvokeRemoveIndexRef(
                75,
                65,
                1,
                ref activeTriggers,
                ref inspected,
                ref modes);

            Assert(!removed, "Trigger 65 must not report removal.");
            Assert(
                activeTriggers.ContainsKey(65),
                "Trigger 65 was removed even though it still covers image 70.");
            Assert(
                modes[75] == 0,
                "Trigger 75 must not take over Trigger 65 while 65 has dependents.");
        }

        private static void RemovesLeafTriggerAndTransfersCoverage()
        {
            var activeTriggers = new Dictionary<int, Dictionary<int, int>>
            {
                [65] = new Dictionary<int, int> { [60] = 70 },
                [75] = new Dictionary<int, int> { [65] = 70 }
            };
            var inspected = new bool[100];
            var modes = new int[100];

            bool removed = InvokeRemoveIndexRef(
                75,
                65,
                1,
                ref activeTriggers,
                ref inspected,
                ref modes);

            Assert(removed, "A leaf Trigger must report removal.");
            Assert(
                !activeTriggers.ContainsKey(65),
                "Leaf Trigger 65 must be removed after Trigger 75 takes it over.");
            Assert(inspected[65], "Transferred image 65 must be marked covered.");
            Assert(modes[75] == 1, "Trigger 75 must enable ref1 coverage.");
        }

        private static void LoadsSerializedRecipeBytesWithoutReserialization()
        {
            string path = Path.GetTempFileName();
            try
            {
                byte[] expected = { 0, 1, 2, 255, 127 };
                File.WriteAllBytes(path, expected);

                byte[] actual = SerializedCameraParameters.Load(path);

                Assert(actual.Length == expected.Length, "Serialized recipe length changed.");
                for (int index = 0; index < expected.Length; index++)
                    Assert(actual[index] == expected[index], "Serialized recipe bytes changed.");
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static void UsesRecipePadCoordinatesWithoutGoldenDieRescaling()
        {
            const string recipePath = @"D:\Projects\opencvProject\WaferInspectionPatternCpu\WaferInspectionUI\bin\Debug\net8.0-windows\recipe\0127-2x标片";
            var request = new BridgeRequest
            {
                RecipePath = recipePath,
                ImageRoot = recipePath,
                ResponsePath = Path.Combine(Path.GetTempPath(), "gpu-swm-pad-regression.swmr"),
                ImageWidth = 4096,
                ImageHeight = 4096,
                DetectionMicronPerPixelX = 2.25,
                DetectionMicronPerPixelY = 2.25,
                // Deliberately differ from the recipe to prove the bridge
                // uses the same Recipe-backed WSTIC values as production.
                BrightBlackJudge = 2,
                BackThr = 17,
                DeltaBlack = 23
            };

            RecipeAdapterResult result = RecipeAdapter.Build(request);
            var pads = result.CameraParameters.Recipe.ProbeMarkRect;

            Assert(pads.Count == 4, "0127-2x标片必须保留全部 4 个 PAD 框。");
            AssertRect(pads[0], 485, 3, 371, 230, "PAD 0");
            AssertRect(pads[1], 6, 4, 481, 833, "PAD 1");
            AssertRect(pads[2], 488, 579, 383, 261, "PAD 2");
            AssertRect(pads[3], 489, 391, 330, 61, "PAD 3");

            var wafer = result.CameraParameters.Recipe.Wafer;
            Assert(wafer.RowsCount == 1 && wafer.ColumnsCount == 1,
                "晶圆的自定义行列数必须从 WaferPara.xml 传入生产规划。");
            Assert(Math.Abs(wafer.DiePitchX - 1.9999904) < 1e-10 && Math.Abs(wafer.DiePitchY - 1.9999954) < 1e-10,
                "Die Pitch 必须保留 Recipe 原始精度，不能在 Bridge 中截断到 5 位小数。actual=" +
                wafer.DiePitchX + "," + wafer.DiePitchY);

            var inspection = result.CameraParameters.Recipe.InspectionParameter as AutoReviewSystem.Data.WSTICParameters;
            Assert(inspection != null, "0127-2x标片必须生成 WSTIC 参数。");
            Assert(inspection.BrightBlackJudge == 0 && inspection.BackThr == 0 && inspection.DeltaBlack == 0,
                "WSTIC GPU 参数必须来自 InspectionPara.xml，不能由测试 UI 的临时值覆盖。");
        }

        private static void CarriesProductionSampleCenterIntoSwmPlanning()
        {
            const string recipePath = @"D:\Projects\opencvProject\WaferInspectionPatternCpu\WaferInspectionUI\bin\Debug\net8.0-windows\recipe\0127-2x标片";
            var request = new BridgeRequest
            {
                RecipePath = recipePath,
                ImageRoot = recipePath,
                ResponsePath = Path.Combine(Path.GetTempPath(), "gpu-swm-sample-center-regression.swmr"),
                ImageWidth = 4096,
                ImageHeight = 4096,
                DetectionMicronPerPixelX = 2.2466300549176239,
                DetectionMicronPerPixelY = 2.2466300549176239,
                SampleCenterLocationX = -0.7945,
                SampleCenterLocationY = -1.2014
            };

            RecipeAdapterResult result = RecipeAdapter.Build(request);
            var actual = result.CameraParameters.Recipe.Wafer._SampleCenterLocation;
            Assert(Math.Abs(actual.X + 0.7945) < 1e-10 && Math.Abs(actual.Y + 1.2014) < 1e-10,
                "GPU SWM 规划不能把产线 SampleCenter 强制改为 (0,0)。actual=" + actual.X + "," + actual.Y);
        }

        private static void AssertRect(System.Drawing.Rectangle actual, int x, int y, int width, int height, string name)
        {
            Assert(actual.X == x && actual.Y == y && actual.Width == width && actual.Height == height,
                name + " 坐标被 GoldenDie 倍率换算改变：actual=" + actual +
                ", expected={X=" + x + ",Y=" + y + ",Width=" + width + ",Height=" + height + "}。");
        }

        private static bool InvokeRemoveIndexRef(
            int index,
            int indexRef,
            int refFlag,
            ref Dictionary<int, Dictionary<int, int>> activeTriggers,
            ref bool[] inspected,
            ref int[] modes)
        {
            object[] arguments =
            {
                index,
                indexRef,
                refFlag,
                activeTriggers,
                inspected,
                modes
            };

            bool result = (bool)RemoveIndexRefMethod.Invoke(
                new Parameters(),
                arguments);

            activeTriggers =
                (Dictionary<int, Dictionary<int, int>>)arguments[3];
            inspected = (bool[])arguments[4];
            modes = (int[])arguments[5];

            return result;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
