using System.Collections.Generic;
using System.Windows;

namespace SWM
{
    public class ScanPlan : Base.IComponentObject
    {
        private static Base.ID_AreaType _AreaType = Base.ID_AreaType.ScanPlan;

        public List<Slice> Slices { get; set; } = new List<Slice>();

        public List<Scan> scanInspection { get; set; } = new List<Scan>();

        public Base.ID_AreaType AreaType => ScanPlan._AreaType;

        public Rect Area { get; set; }

        public ScanPlan()
        {
        }

        public ScanPlan(Rect area) => this.Area = area;
    }
}
