using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SWM
{
    public sealed class RecipeInputData
    {
        public WaferInput Wafer { get; set; }
        public InspectionInput Inspection { get; set; }
        public List<PlanPoint> TestPlan { get; set; } = new List<PlanPoint>();
        public List<PadRectInput> PadRects { get; set; } = new List<PadRectInput>();
    }

    public sealed class WaferInput
    {
        public double Diameter { get; set; }
        public double EdgeLoss { get; set; }
        public double ChrEdgeLoss { get; set; }
        public double DieWidth { get; set; }
        public double DieHeight { get; set; }
        public double ScribeLaneX { get; set; }
        public double ScribeLaneY { get; set; }
        public double DieOriginX { get; set; }
        public double DieOriginY { get; set; }
    }

    public sealed class InspectionInput
    {
        public int ImageCount { get; set; }
        public string LensMode { get; set; }
        public double DeltaThreshold { get; set; }
        public double ColorThreshold { get; set; }
        public int SurfaceOpen { get; set; }
        public int ContourRemoval { get; set; }
        public double Threshold { get; set; }
        public long ErodeValue { get; set; }
        public int PadEraseMode { get; set; }
        public long OpenValue { get; set; }
        public double MinArea { get; set; }
        public int MinWidth { get; set; }
        public int MinHeight { get; set; }
        public bool ShiftJudge { get; set; }
        public double MinDetectRatio { get; set; }
        public bool AfRangeEnabled { get; set; }
        public int AfRange { get; set; }
        public double Velocity { get; set; }
        public double Accel { get; set; }
        public double Decel { get; set; }
        public double Jerk { get; set; }
        public double AccelDistance { get; set; }
        public double DecelDistance { get; set; }
    }

    public struct PlanPoint { public int X { get; set; } public int Y { get; set; } }
    public struct PadRectInput { public int X { get; set; } public int Y { get; set; } public int Width { get; set; } public int Height { get; set; } }

    public static class RecipeInputLoader
    {
        public static RecipeInputData Load(string recipePath)
        {
            if (string.IsNullOrWhiteSpace(recipePath))
                throw new ArgumentException("Recipe path is required.", nameof(recipePath));
            if (!Directory.Exists(recipePath))
                throw new DirectoryNotFoundException(recipePath);

            XDocument wafer = LoadXml(recipePath, "WaferPara.xml");
            XDocument inspection = LoadXml(recipePath, "InspectionPara.xml");
            XDocument plan = LoadXml(recipePath, "TestPlan.xml");
            XDocument limitArea = LoadXml(recipePath, "LimitAreaPara.xml");
            return new RecipeInputData
            {
                Wafer = ReadWafer(wafer.Root),
                Inspection = ReadInspection(inspection.Root),
                TestPlan = ReadPlan(plan.Root),
                PadRects = ReadPads(limitArea.Root)
            };
        }

        private static XDocument LoadXml(string directory, string fileName)
        {
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path)) throw new FileNotFoundException("Recipe file is missing.", path);
            return XDocument.Load(path);
        }
        private static WaferInput ReadWafer(XElement root) => new WaferInput
        {
            Diameter = RequiredDouble(root, "Diameter"), EdgeLoss = RequiredDouble(root, "EdgeLoss"),
            ChrEdgeLoss = OptionalDouble(root, "ChrEdgeLoss", 0), DieWidth = RequiredDouble(root, "DieWidth"),
            DieHeight = RequiredDouble(root, "DieHeight"), ScribeLaneX = RequiredDouble(root, "ScribeLaneX"),
            ScribeLaneY = RequiredDouble(root, "ScribeLaneY"), DieOriginX = RequiredDouble(root, "DieOriginX"),
            DieOriginY = RequiredDouble(root, "DieOriginY")
        };
        private static InspectionInput ReadInspection(XElement root) => new InspectionInput
        {
            ImageCount = OptionalInt(root, "ImageCount", 0), LensMode = OptionalText(root, "LensMode", string.Empty),
            DeltaThreshold = RequiredDouble(root, "DeltaThreashold"), ColorThreshold = OptionalDouble(root, "CThreashold", 90),
            SurfaceOpen = OptionalInt(root, "SurfaceOpen", 1), ContourRemoval = OptionalInt(root, "ContourRemoval", 0),
            Threshold = RequiredDouble(root, "ThreasholdValue"), ErodeValue = OptionalLong(root, "ErodeValue", 0),
            PadEraseMode = OptionalInt(root, "IsEnablePadErase", 0), OpenValue = OptionalLong(root, "OpenValue", 0),
            MinArea = RequiredDouble(root, "MinDetectArea"), MinWidth = OptionalInt(root, "MinDetectWidth", 1),
            MinHeight = OptionalInt(root, "MinDetectHeight", 1), ShiftJudge = OptionalBool(root, "ShiftJudge", false),
            MinDetectRatio = OptionalDouble(root, "MinDetectRatio", 0), AfRangeEnabled = OptionalBool(root, "AFRangeEnabled", false),
            AfRange = OptionalInt(root, "AFRange", 0), Velocity = OptionalDouble(root, "Velocity", 0),
            Accel = OptionalDouble(root, "Accel", 0), Decel = OptionalDouble(root, "Decel", 0), Jerk = OptionalDouble(root, "Jerk", 0),
            AccelDistance = OptionalDouble(root, "AccelDistance", 0), DecelDistance = OptionalDouble(root, "DecelDistance", 0)
        };
        private static List<PlanPoint> ReadPlan(XElement root) => root.Elements("Point").Select(p => new PlanPoint { X = RequiredInt(p, "X"), Y = RequiredInt(p, "Y") }).ToList();
        private static List<PadRectInput> ReadPads(XElement root)
        {
            XElement list = root.Element("ProbeMarkRectList");
            return list == null ? new List<PadRectInput>() : list.Elements("ProbeMarkRect").Select(p => new PadRectInput
            {
                X = RequiredInt(p, "PadRectX") + OptionalInt(p, "OffsetX", 0), Y = RequiredInt(p, "PadRectY") + OptionalInt(p, "OffsetY", 0),
                Width = RequiredInt(p, "PadRectWidth"), Height = RequiredInt(p, "PadRectHeight")
            }).ToList();
        }
        private static string RequiredText(XElement root, string name)
        {
            XElement value = root.Element(name);
            if (value == null || string.IsNullOrWhiteSpace(value.Value)) throw new InvalidDataException("Required recipe value is missing: " + name);
            return value.Value.Trim();
        }
        private static string OptionalText(XElement root, string name, string fallback)
        {
            XElement value = root.Element(name);
            return value == null || string.IsNullOrWhiteSpace(value.Value) ? fallback : value.Value.Trim();
        }
        private static double RequiredDouble(XElement root, string name) => ParseDouble(RequiredText(root, name), name);
        private static double OptionalDouble(XElement root, string name, double fallback) { string value = OptionalText(root, name, null); return value == null ? fallback : ParseDouble(value, name); }
        private static int RequiredInt(XElement root, string name) => ParseInt(RequiredText(root, name), name);
        private static int OptionalInt(XElement root, string name, int fallback) { string value = OptionalText(root, name, null); return value == null ? fallback : ParseInt(value, name); }
        private static long OptionalLong(XElement root, string name, long fallback) { string value = OptionalText(root, name, null); long result; return value == null ? fallback : long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : throw new InvalidDataException("Invalid integer recipe value: " + name); }
        private static bool OptionalBool(XElement root, string name, bool fallback) { string value = OptionalText(root, name, null); bool result; return value == null ? fallback : bool.TryParse(value, out result) ? result : throw new InvalidDataException("Invalid boolean recipe value: " + name); }
        private static double ParseDouble(string value, string name) { double result; if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) throw new InvalidDataException("Invalid numeric recipe value: " + name); return result; }
        private static int ParseInt(string value, string name) { int result; if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)) throw new InvalidDataException("Invalid integer recipe value: " + name); return result; }
    }
}
