using AutoReviewSystem.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SWM
{
    public class GoldenROI
    {
        [Description("Size of Pixel [μm]")]
        public static RealSize PixelSize { get; set; }

        public static RealPoint DieOrigin { get; set; }

        public static RealPoint DiePitch { get; set; }

        public static RealPoint DieWH { get; set; }

        public Point DieIndex { get; set; }

        [Description("X-Position of the Upper-Left Corner of ImageROI in Sample Coordinates [μm]")]
        public double X
        {
            get
            {
                return ScanPosition.X * 1000.0 + Iroi.ImageArea.X * PixelSize.Width;
            }
        }

        [Description("Y-Position of the Upper-Left Corner of ImageROI in Sample Coordinates [μm]")]
        public double Y
        {
            get
            {
                return ScanPosition.Y * 1000.0 - Iroi.ImageArea.Y * PixelSize.Height;
            }
        }

        [XmlIgnore]
        [Description("X Intra Die Defect Position Relative to the Lower-Left Corner of the Die [pixel]")]
        public int XRel
        {
            get
            {
               
                double num1 = DiePitch.X * 1000.000;
                double num2 = this.X - (DieOrigin.X * 1000.000);
                int num3 = (int)Math.Floor(num2 / num1);
                double num4 = num2 - num3 * num1;
                double num5 = num4 < PixelSize.Width ? 0 : num4;

                double num6 = num5 > (DieWH.X * 1000.0 + PixelSize.Width)? Math.Abs(num5 - num1) : num5;

                return (int)Math.Floor(num6 / PixelSize.Width);
            }
        }

        [XmlIgnore]
        [Description("Y Intra Die Defect Position Relative to the Lower-Left Corner of the Die [pixel]")]
        public int YRel
        {
            get
            {
                double num1 = DiePitch.Y * 1000.0;
                double num2 = this.Y - (DieOrigin.Y * 1000.0);
                int num3 = (int)Math.Floor(num2 / num1);
                double num4 = num2 - (double)num3 * num1;
                double num5 = num4 < PixelSize.Height ? 0 : num4;
                double num6 = num5 > (DieWH.Y * 1000.0+ PixelSize.Height) ? Math.Abs(num5 - num1) : num5;
                return (int)Math.Floor(num6 / PixelSize.Height);
            }
        }

        public IPROI Iroi { get; set; }

        public RealPoint ScanPosition { get; set; }

    }
}
