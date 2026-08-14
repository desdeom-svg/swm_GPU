using System.Collections.Generic;
using System.Windows;
using System.Xml.Serialization;

namespace SWM
{
    public class Slice : Base.IComponentObject
    {
        private static Base.ID_AreaType _AreaType = Base.ID_AreaType.Slice;

        public List<Scan> Scans { get; set; } = new List<Scan>();

        public Base.ID_AreaType AreaType => Slice._AreaType;

        public Rect Area { get; set; }

        [XmlIgnore]
        public ScanPlan Parent { get; set; }

        public Slice()
        {
        }

        public Slice(ScanPlan parent, Rect area)
        {
            this.Parent = parent;
            this.Area = area;
        }
    }
}

