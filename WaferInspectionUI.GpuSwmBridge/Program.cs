using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using AutoReviewSystem.Data;

namespace SWM
{
    internal static class Program
    {
        private const int RequestError = 2;
        private const int RecipeError = 3;
        private const int SwmError = 4;
        private const int OutputError = 5;

        private static int Main(string[] args)
        {
            if (args.Length != 1)
                return Fail(RequestError, "Usage: WaferInspectionUI.GpuSwmBridge <request.json>");

            BridgeRequest request;
            try
            {
                request = ReadRequest(args[0]);
                request.Validate();
                if (!Directory.Exists(request.RecipePath))
                    throw new DirectoryNotFoundException(request.RecipePath);
                if (!Directory.Exists(request.ImageRoot))
                    throw new DirectoryNotFoundException(request.ImageRoot);
            }
            catch (Exception ex)
            {
                return Fail(RequestError, ex.Message);
            }

            List<OfflineImageRow> layout;
            RecipeAdapterResult adapted;
            try
            {
                layout = OfflineImageLayout.Scan(request.ImageRoot);
                adapted = RecipeAdapter.Build(request);
                OfflineLayoutCompatibility.AlignAndValidate(adapted.CameraParameters, layout);
            }
            catch (Exception ex)
            {
                return Fail(RecipeError, ex.Message);
            }

            BridgeResponse response;
            try
            {
                response = GenerateGpuParameters(adapted.CameraParameters, layout);
            }
            catch (Exception ex)
            {
                return Fail(SwmError, ex.Message);
            }

            try
            {
                BridgeResponseWriter.Write(request.ResponsePath, response);
                Console.WriteLine(
                    "GPU SWM parameters generated: {0} rows, {1} images.",
                    response.Rows.Count,
                    response.Rows.Sum(row => row.Images.Count));
                return 0;
            }
            catch (Exception ex)
            {
                return Fail(OutputError, ex.Message);
            }
        }

        private static BridgeRequest ReadRequest(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Bridge request does not exist.", path);

            var serializer = new DataContractJsonSerializer(typeof(BridgeRequest));
            using (var stream = File.OpenRead(path))
                return (BridgeRequest)serializer.ReadObject(stream);
        }

        private static BridgeResponse GenerateGpuParameters(
            CameraParameters camera,
            IList<OfflineImageRow> layout)
        {
            var parameters = new Parameters();
            byte[] serialized = DataConverter.ToByteArray(camera);
            List<double[]> inspectionRows = parameters.GetParam(serialized);
            if (inspectionRows.Count == 1 && inspectionRows[0].Length == 1)
                throw new InvalidOperationException("GPU SWM GetParam failed with code " + inspectionRows[0][0]);
            if (inspectionRows.Count != layout.Count)
                throw new InvalidDataException("GPU SWM parameter row count does not match the image layout.");

            var response = new BridgeResponse();
            for (int rowIndex = 0; rowIndex < layout.Count; rowIndex++)
            {
                OfflineImageRow layoutRow = layout[rowIndex];
                double[] inspectionParam = inspectionRows[rowIndex];
                int recordLength = GetPositiveInteger(inspectionParam, 15, "record length", rowIndex);
                if (inspectionParam.Length < checked(recordLength * layoutRow.ImageCount))
                    throw new InvalidDataException("GPU SWM parameter row is shorter than its image layout at row " + rowIndex);

                var responseRow = new BridgeRow
                {
                    SliceIndex = layoutRow.SliceIndex,
                    Repeat = layoutRow.Repeat,
                    InspectionParam = inspectionParam
                };
                for (int imageIndex = 0; imageIndex < layoutRow.ImageCount; imageIndex++)
                {
                    int recordStart = checked(imageIndex * recordLength);
                    int roiCount = GetNonNegativeInteger(
                        inspectionParam,
                        checked(recordStart + 20),
                        "ROI count",
                        rowIndex);
                    int reference1 = GetNonNegativeInteger(
                        inspectionParam,
                        checked(recordStart + 21),
                        "reference image 1",
                        rowIndex);
                    int reference2 = GetNonNegativeInteger(
                        inspectionParam,
                        checked(recordStart + 22),
                        "reference image 2",
                        rowIndex);
                    if (reference1 >= layoutRow.ImageCount || reference2 >= layoutRow.ImageCount)
                    {
                        throw new InvalidDataException(
                            "GPU SWM reference image is outside the current row at row " + rowIndex + ", image " + imageIndex);
                    }
                    if (roiCount < 0)
                        throw new InvalidDataException("GPU SWM ROI count is invalid.");

                    responseRow.Images.Add(new BridgeImage
                    {
                        ImageIndex = imageIndex,
                        ReferenceImage1 = reference1,
                        ReferenceImage2 = reference2,
                        // GPU Inspection ABI embeds IPROI/PAD data in InspectionParam.
                        // The CPU-only RegionParam side channel must stay empty on this route.
                        RegionParam = Array.Empty<double>()
                    });
                }
                response.Rows.Add(responseRow);
            }
            return response;
        }

        private static int GetPositiveInteger(double[] values, int index, string name, int rowIndex)
        {
            int value = GetNonNegativeInteger(values, index, name, rowIndex);
            if (value == 0)
                throw new InvalidDataException("GPU SWM " + name + " must be positive at row " + rowIndex);
            return value;
        }

        private static int GetNonNegativeInteger(double[] values, int index, string name, int rowIndex)
        {
            if (index < 0 || index >= values.Length ||
                double.IsNaN(values[index]) || double.IsInfinity(values[index]) ||
                values[index] < 0 || values[index] > int.MaxValue ||
                Math.Truncate(values[index]) != values[index])
            {
                throw new InvalidDataException("GPU SWM " + name + " is invalid at row " + rowIndex);
            }
            return (int)values[index];
        }

        private static int Fail(int code, string message)
        {
            Console.Error.WriteLine(message);
            return code;
        }
    }
}
