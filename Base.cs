using System.Collections.Generic;
using System.Windows;

namespace SWM
{
    public static class Base
    {
        public static Rect UnionArea(ref List<Base.IComponentObject> components)
        {
            if (components.Count <= 1)
                return components[0].Area;
            Rect rect = new Rect(components[0].Area.X, components[0].Area.Y, components[0].Area.Width, components[0].Area.Height);
            for (int index = 1; index < components.Count; ++index)
                rect.Union(components[index].Area);
            return rect;
        }

        public enum ID_AreaType
        {
            Unknown,
            Die,
            Die_Pad,
            Die_Pattern,
            Die_Bump,
            Wafer,
            Scan,
            Slice,
            ScanPlan,
            IPROI,
        }

        public enum ID_IPROIType
        {
            INCLUDE,
            EXCLUDE,
        }

        public enum DiePosition
        {
            OutsideWafer,
            OnWaferEdge,
            InExclusionEdge,
            OnExclusionLimit,
            InInspectionArea,
        }

        public interface IComponentObject
        {
            Base.ID_AreaType AreaType { get; }

            Rect Area { get; }
        }
    }
}


