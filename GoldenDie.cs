using AutoReviewSystem.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SWM
{
    //更改DATA里wafer类中属性修饰符：public   Die[,] Map;   Point DieOriginIndex;
    public class GoldenDie
    {
        //DIE的宽(pixel)
        public static int DieWidthP { get; set; }
        //DIE的高(pixel)
        public static int DieHeightP { get; set; }
        //DIE的pad区域在GoldenDie上的位置(可以多个，输入的是相对于Die的左上角)
        public static List<Rect> PadRoi { get; set; }
        //判断每个GD上的ROI区域是否含有pad区域，及包含区域在GD图上位置
        public bool Contain(Rect IProiGD, ref List<Rect> IP_PadRoi_GD)
        {
            bool judge = false;
            IP_PadRoi_GD = new List<Rect>();
            foreach (Rect roi in PadRoi)
            {
                Rect rect = Rect.Intersect(roi, IProiGD);
                if (!rect.IsEmpty)
                {
                    rect.Y = DieHeightP - rect.Y;
                    IP_PadRoi_GD.Add(rect);
                    judge = true;
                }
            }
            return judge;
        }
        //转换成图像坐标系上的值=
        public void GetIPad(GoldenROI GROI, List<Rect> IP_PadRoi_GD, ref List<Rect> IP_PadRoi_Image)
        {
            if (IP_PadRoi_GD.Count == 0) return;
            IP_PadRoi_Image = new List<Rect>();

            foreach (Rect roi in IP_PadRoi_GD)
            {
                double deltaX = Math.Abs(roi.X - GROI.XRel);
                double deltaY = Math.Abs(roi.Y - GROI.YRel);
                IP_PadRoi_Image.Add(new Rect(GROI.Iroi.ImageArea.X + deltaX, GROI.Iroi.ImageArea.Y + deltaY, roi.Width, roi.Height));
            }
            return;
        }
    }
}
