using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SWM
{
    public sealed class OfflineImageRow
    {
        public int SliceIndex { get; set; }
        public int ImageCount { get; set; }
        public int Repeat { get; set; }
        public string DirectoryPath { get; set; }
    }

    public static class OfflineImageLayout
    {
        private static readonly Regex LegacyDirectoryPattern = new Regex(
            @"^s\s*(\d+)_t\s*(\d+)_r\s*(\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Production result images are saved as p_r_<ScanSequence row>\p_r_<row>_<column>_<global>.jpg.
        // _r is part of the production prefix here; it is not the SWM Repeat value.
        private static readonly Regex ProductionDirectoryPattern = new Regex(
            @"^p_r_(\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<OfflineImageRow> Scan(string imageRoot)
        {
            if (!Directory.Exists(imageRoot))
                throw new DirectoryNotFoundException(imageRoot);

            var rows = new List<OfflineImageRow>();
            foreach (string directory in Directory.GetDirectories(imageRoot))
            {
                string name = Path.GetFileName(directory);
                Match legacy = LegacyDirectoryPattern.Match(name);
                Match production = ProductionDirectoryPattern.Match(name);
                if (!legacy.Success && !production.Success)
                    continue;

                var row = new OfflineImageRow { DirectoryPath = directory };
                int actualCount;
                if (legacy.Success)
                {
                    row.SliceIndex = int.Parse(legacy.Groups[1].Value);
                    row.ImageCount = int.Parse(legacy.Groups[2].Value);
                    row.Repeat = int.Parse(legacy.Groups[3].Value);
                    actualCount = Directory.GetFiles(directory, "*.bmp").Length;
                    if (actualCount != row.ImageCount)
                    {
                        throw new InvalidDataException(string.Format(
                            "{0} declares {1} BMP images, but found {2}.",
                            name,
                            row.ImageCount,
                            actualCount));
                    }
                }
                else
                {
                    row.SliceIndex = int.Parse(production.Groups[1].Value);
                    row.ImageCount = Directory.GetFiles(directory, "*.jpg").Length;
                    row.Repeat = 1;
                    if (row.ImageCount <= 0)
                        throw new InvalidDataException("Production row " + name + " contains no JPG images.");
                }
                rows.Add(row);
            }

            rows = rows.OrderBy(row => row.SliceIndex).ToList();
            for (int index = 0; index < rows.Count; index++)
            {
                if (rows[index].SliceIndex != index)
                    throw new InvalidDataException("Offline image rows must start at 0 and be contiguous.");
            }
            return rows;
        }
    }
}
