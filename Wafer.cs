using System.Collections.Generic;
using System.Windows;

namespace SWM
{
    public class Wafer : Base.IComponentObject
    {
        private static Base.ID_AreaType _AreaType = Base.ID_AreaType.Wafer;

        public List<Die> Dies { get; set; } = new List<Die>();

        public List<AutoReviewSystem.Data.Fov> FovMap { get; set; } = new List<AutoReviewSystem.Data.Fov>();

        public Base.ID_AreaType AreaType => Wafer._AreaType;

        public Rect Area { get; set; }

        public Wafer()
        {
        }

        public Wafer(uint Radius) => this.Area = new Rect(new Point((double)-Radius, (double)-Radius), new Point((double)Radius, (double)Radius));
    }
}
