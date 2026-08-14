using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;

namespace SWM
{
    public class SWMCore
    {
        [XmlIgnore]
        public Dictionary<Slice, Rect> ExceptionSliceArea = new Dictionary<Slice, Rect>();

        public Wafer Wafer { get; set; }

        public ScanPlan ScanPlan { get; set; }

        [XmlIgnore]
        public Dictionary<Scan, List<IPROI>> IPROIDictionary { get; private set; }

        public SWMCore()
        {
        }

        public SWMCore(Wafer wafer, ScanPlan scanPlan) => this.Construct(wafer, scanPlan);

        public void Construct(Wafer wafer, ScanPlan scanPlan)
        {
            this.Wafer = wafer;
            this.ScanPlan = scanPlan;
            this.IPROIDictionary = new Dictionary<Scan, List<IPROI>>();

          
            foreach (Slice slice in this.ScanPlan.Slices)
            {
                foreach (Scan scan in slice.Scans)
                    this.IPROIDictionary.Add(scan, new List<IPROI>());
            }
            //get excluded slice area
            this.ExceptionSliceArea = new Dictionary<Slice, Rect>();
            for (int index = this.ScanPlan.Slices.Count - 1; index > 0; --index)
            {
                Rect rect = Rect.Intersect(this.ScanPlan.Slices[index].Area, this.ScanPlan.Slices[index - 1].Area);
                if (!rect.IsEmpty)
                    this.ExceptionSliceArea.Add(this.ScanPlan.Slices[index], rect);
            }
            //get excluded scan area
            List<Dictionary<Scan, Rect>> dictionaryList = new List<Dictionary<Scan, Rect>>();
            foreach (Slice slice in this.ScanPlan.Slices)
            {
                Dictionary<Scan, Rect> dictionary = new Dictionary<Scan, Rect>();
                for (int index = slice.Scans.Count - 1; index > 0; --index)
                {
                    Rect rect = Rect.Intersect(slice.Scans[index].Area, slice.Scans[index - 1].Area);
                    if (!rect.IsEmpty)
                        dictionary.Add(slice.Scans[index], rect);
                }
                dictionaryList.Add(dictionary);
            }
        
            int indexFov = 0;
            bool reverse = false;
            //存储符合要求的InInspectionArea图片，以Index区分位置
            if (this.ScanPlan.scanInspection == null) this.ScanPlan.scanInspection = new List<Scan>();
            //线程安全集合
           ConcurrentStack<Scan> ScanInStack = new ConcurrentStack<Scan>();
            for (int index1 = 0; index1 < this.ScanPlan.Slices.Count; ++index1)
             {

                Slice slice = this.ScanPlan.Slices[index1];
                //判断逆转
                if(slice.Scans.Count>1 && slice.Scans[0].Area.X > slice.Scans[1].Area.X) reverse = true;

                Rect sliceExtArea;
                this.ExceptionSliceArea.TryGetValue(slice, out sliceExtArea);
                for (int index2 = 0; index2 < slice.Scans.Count; ++index2)
                {

                   Scan scan = slice.Scans[index2];
                   scan.Index = indexFov;
                   scan.IndexX = reverse == true ? slice.Scans.Count-1- index2: index2; 
                   scan.IndexY = index1;
                   scan.ReapeatXY = new Point(wafer.FovMap[indexFov].RepeatNum_X, wafer.FovMap[indexFov].RepeatNum_Y);
                   scan.EnjaVir = wafer.FovMap[indexFov].FovPathType == AutoReviewSystem.Data.PathType.OnlyXYEqualPath;

                    //给每个scan属性赋值，是否虚拟die
                    if (indexFov < wafer.FovMap.Count && wafer.FovMap[indexFov].Type == AutoReviewSystem.Data.DiePosition.OnExclusionLimit)
                    { scan.ScanVirDie = true; scan.ScanSIn = true; }

                    //存储非虚拟die的图
                    if(wafer.FovMap[indexFov].FovPathType == AutoReviewSystem.Data.PathType.OnlyXYEqualPath 
                        && wafer.FovMap[indexFov].Type == AutoReviewSystem.Data.DiePosition.InInspectionArea)
                       { ScanInStack.Push(scan); }
                     indexFov++;
                    //每张图属于每个die的
                    foreach (Die dy in this.Wafer.Dies)
                    {
                        Rect area = Rect.Intersect(scan.Area, dy.Area);
                        if (!area.IsEmpty)
                        {
                            List<IPROI> iproiList;
                            this.IPROIDictionary.TryGetValue(scan, out iproiList);
                            iproiList.Add(new IPROI(scan, area, Base.ID_IPROIType.INCLUDE, dy.ID));
                        }
                    }
                    //每张图的行与上一行交集
                    if (!sliceExtArea.IsEmpty)
                    {
                        Rect area = Rect.Intersect(scan.Area, sliceExtArea);
                        if (!area.IsEmpty)
                        {
                            List<IPROI> iproiList;
                            this.IPROIDictionary.TryGetValue(scan, out iproiList);
                            iproiList.Add(new IPROI(scan, area, Base.ID_IPROIType.EXCLUDE, 99999999));
                        }
                    }
                    Rect scanExtArea;
                    //同一行每张图的行与上一张的交集
                    if (dictionaryList[index1].TryGetValue(scan, out scanExtArea))
                    {
                        Rect area = Rect.Intersect(scan.Area, scanExtArea);
                        if (!area.IsEmpty)
                        {
                            List<IPROI> iproiList;
                            this.IPROIDictionary.TryGetValue(scan, out iproiList);
                            iproiList.Add(new IPROI(scan, area, Base.ID_IPROIType.EXCLUDE, 99999999));
                        }
                    }
                }
                reverse = false;
            }

            this.ScanPlan.scanInspection = ScanInStack.ToList();
        }

        public List<Base.IComponentObject> GetRelatedComponents(ref Scan scan, ref Wafer wafer)
        {
            List<Base.IComponentObject> componentObjectList = new List<Base.IComponentObject>();
            foreach (Die dy in wafer.Dies)
            {
                if (dy.Area.Contains(scan.Area))
                    componentObjectList.Add((Base.IComponentObject)dy);
            }
            return componentObjectList;
        }

        public List<Base.IComponentObject> GetRelatedComponents(
          ref Base.IComponentObject targetObject,
          ref List<Base.IComponentObject> investigationObject)
        {
            List<Base.IComponentObject> componentObjectList = new List<Base.IComponentObject>();
            foreach (Base.IComponentObject componentObject in investigationObject)
            {
                if (componentObject.Area.Contains(targetObject.Area))
                    componentObjectList.Add(componentObject);
            }
            return componentObjectList;
        }
    }
}
