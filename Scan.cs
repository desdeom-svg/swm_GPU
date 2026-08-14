using System.Windows;
using System.Xml.Serialization;

namespace SWM
{
    public class Scan : Base.IComponentObject
    {
        private Base.ID_AreaType _AreaType = Base.ID_AreaType.Scan;

        public Base.ID_AreaType AreaType => this._AreaType;

        public Rect Area { get; set; }

        [XmlIgnore]
        public Slice Parent { get; set; }

        //全局索引
        public int Index { get; set; } = -1;

        public int IndexX { get; set; } = -1;

        public int IndexY { get; set; } = -1;

        public Point ReapeatXY { get; set; } = new Point(-1, -1);

        //可进行跨行虚拟
        public bool EnjaVir { get; set; } = false;

        //scan是否包含虚拟die
        public bool ScanVirDie { get; set; } = false;

        //scan是否进行二次阈值检测
        public bool ScanSIn { get; set; } = false;

        public Scan()
        {
        }

        public Scan(Slice parent, Rect area)
        {
            this.Parent = parent;
            this.Area = area;
        }
    }
}
