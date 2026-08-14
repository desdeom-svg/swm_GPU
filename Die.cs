using System.Collections.Generic;
using System.Windows;
using System.Xml.Serialization;

namespace SWM
{
    public class Die : Base.IComponentObject
    {
        private static Base.ID_AreaType _AreaType = Base.ID_AreaType.Die;

        [XmlIgnore]
        public static List<Base.IComponentObject> DieComponent { get; set; }

        public Base.ID_AreaType AreaType => Die._AreaType;

        public int ID { get; set; } = -1;

        [XmlIgnore]
        public Wafer Parent { get; set; }

        public Rect Area { get; set; }

        public Die()
        {
        }

        public Die(Wafer parent, Rect area)
        {
            this.Parent = parent;
            this.Area = area;
        }
    }
}
