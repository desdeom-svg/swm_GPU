using System.Windows;
using System.Xml.Serialization;

namespace SWM
{
    public class IPROI : Base.IComponentObject
    {
        private static Base.ID_AreaType _AreaType = Base.ID_AreaType.IPROI;

        public static int ImageWidth { get; set; }

        public Base.ID_AreaType AreaType => IPROI._AreaType;
        //iproi是否检测属性
        public Base.ID_IPROIType IPROIType { get; set; }

        public GoldenROI GoldROI { get; set; }

        [XmlIgnore]
        public Scan Parent { get; set; }

        public Rect Area { get; set; }

        public Rect ImageArea
        {
            get
            {
                Rect temp = GetImageRect(ImageWidth * 1.0 / Parent.Area.Width, ImageWidth * 1.0 / Parent.Area.Height);
              
                return temp;
            }
        }
        /// <summary>
        /// 每个include的iproi才会有dieid，exclude默认是99999999
        /// </summary>
        public int DieID { get; set; }

        public double XOfScan
        {
            get
            {
                Rect area = this.Area;
                double x1 = area.X;
                area = this.Parent.Area;
                double x2 = area.X;
                return x1 - x2;
            }
        }

        public double YOfScan
        {
            get
            {
                Rect area = this.Parent.Area;
                double height1 = area.Height;
                area = this.Area;
                double height2 = area.Height;
                area = this.Area;
                double y1 = area.Y;
                area = this.Parent.Area;
                double y2 = area.Y;
                double num1 = y1 - y2;
                double num2 = height2 + num1;
                return height1 - num2;
            }
        }

        public double Width => this.Area.Width;

        public double Height => this.Area.Height;

        public Rect GetImageRect(double pixelPerMicronX, double pixelPerMicronY) => new Rect(this.XOfScan * pixelPerMicronX, this.YOfScan * pixelPerMicronY, this.Width * pixelPerMicronX, this.Height * pixelPerMicronY);

        public IPROI()
        {
        }

        public IPROI(Scan parent, Rect area, Base.ID_IPROIType iPROIType, int dieID)
        {
            this.Parent = parent;
            this.Area = area;
            this.IPROIType = iPROIType;
            this.DieID = dieID;
        }
    }
}
