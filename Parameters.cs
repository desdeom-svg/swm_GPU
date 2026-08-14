/*--------------------------------------------------------------------------------
// Copyright (C) 2024 Suzhou HYC Technology Co.,LTD
// All rights reserved.
//
// ================================================
// 文件名称 : WaferInspection SWM-GPU
// ================================================
// 创 建 者 : LIU Mengru
// 创建日期 : 2024.10.01
// 功能描述 : SWM-GPU检测
// 使用说明 : 功能开发请务必严格遵守doc内接口说明
//
--------------------------------------------------------------------------------*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoReviewSystem.Data;
using System.Drawing;
using System.Windows;
using OpenCvSharp;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Threading;

namespace SWM
{
    public class Parameters
    {
        public static CameraParameters cameraParameters;

        private static SurfaceAOISetting modeSetting;

        private static SWMCore tt;

        public static Dictionary<int, Dictionary<int, int>> TriggerInspetions = new Dictionary<int, Dictionary<int, int>>();
        /// <summary>
        /// fov width
        /// </summary>
        private double fovX
        {
            get
            {
                return cameraParameters.Recipe.FieldType.FieldOfViewWidth * 0.001;
            }
        }
        /// <summary>
        /// fov height
        /// </summary>
        private double fovSize
        {
            get
            {
                return cameraParameters.Recipe.FieldType.FieldOfViewHeight * 0.001;
            }
        }
        /// <summary>
        /// Die width(pixel)
        /// </summary>
        private double dieWidth
        {
            get
            {
                return cameraParameters.Recipe.Wafer.DieWidth / (1.0 * fovX / cameraParameters.Width);
            }
        }
        /// <summary>
        /// Die height(pixel)
        /// </summary>
        private double dieHeight
        {
            get
            {
                return cameraParameters.Recipe.Wafer.DieHeight / (1.0 * fovSize / cameraParameters.Height);
            }
        }
        /// <summary>
        /// pixel size X
        /// </summary>
        private double pixSizeX
        {
            get
            {
                return 1000 * (1.0 * fovX / cameraParameters.Width);
            }
        }
        /// <summary>
        /// pixel size Y
        /// </summary>
        private double pixSizeY
        {
            get
            {
                return 1000 * (1.0 * fovSize / cameraParameters.Height);
            }
        }
        /// <summary>
        /// 检测参数接收
        /// </summary>
        private WSTICParameters inspectionParameter
        {
            get
            {
                return cameraParameters.Recipe.InspectionParameter as WSTICParameters;
            }
        }
        /// <summary>
        /// 接受上位机的probe框参数（单个DIE内有probe的pad的ROI)
        /// </summary>
        private List<Rectangle> PadRois
        {
            get
            {
                List<Rectangle> padRois = new List<Rectangle>();
                foreach (Rectangle roi in Parameters.cameraParameters.Recipe.ProbeMarkRect)
                {
                    if ((roi.X + roi.Width) > dieWidth || (roi.Y + roi.Height) > dieHeight || roi.Width == 0 || roi.Height == 0) continue;

                    padRois.Add(roi);
                }
                return padRois;
            }
        }

        /// <summary>
        /// 静态参数初始化
        /// </summary>
        /// <returns></returns>
        private int IniPara(byte[] data)
        {
            try
            {
                //清空
                Parameters.cameraParameters = null;
                Parameters.modeSetting = null;
                Parameters.tt = null;
                Parameters.TriggerInspetions = null;
                Parameters.TriggerInspetions = new Dictionary<int, Dictionary<int, int>>();
                GoldenDie.DieHeightP = 0;
                GoldenDie.DieWidthP = 0;
                GoldenDie.PadRoi = null;

                //赋值
                //参数接受转换：该模式使用单PC和双PC
                DateTime startime0 = DateTime.Now;
                Parameters.cameraParameters = DataConverter.ToObject<CameraParameters>(data);
               
                DateTime endtime0 = DateTime.Now;
                Console.WriteLine("反序列耗时：{0}ms", (endtime0 - startime0).TotalMilliseconds);
                //参数初始化和转换：
                if (IPROI.ImageWidth == 0) IPROI.ImageWidth = Parameters.cameraParameters.Width;

                GoldenROI.PixelSize = new RealSize(cameraParameters.Recipe.FieldType.FieldOfViewWidth / cameraParameters.Width,
                                     cameraParameters.Recipe.FieldType.FieldOfViewHeight / cameraParameters.Height);
                GoldenROI.DieOrigin = cameraParameters.Recipe.Wafer._DieOrigin;
                GoldenROI.DiePitch = new RealPoint(cameraParameters.Recipe.Wafer.DiePitchX, cameraParameters.Recipe.Wafer.DiePitchY);
                GoldenROI.DieWH = new RealPoint(cameraParameters.Recipe.Wafer.DieWidth, cameraParameters.Recipe.Wafer.DieHeight);
                //接收recipe参数并保存，目前只SurfaceAOI
                if (cameraParameters.Recipe == null) return -7;
                if (cameraParameters.Recipe.RunMode != RunMode.SurfaceAOI) return -9;
                if (cameraParameters.Recipe.InspectionParameterType != InspectionMode.WSTIC) return -9;

                DateTime startime1 = DateTime.Now;
                Parameters.tt = GetMapInfo();
                DateTime endtime1 = DateTime.Now;
                Console.WriteLine("Recipe初始化计算：{0}ms", (endtime1 - startime1).TotalMilliseconds);

                GoldenDie.DieHeightP = (int)dieHeight;
                GoldenDie.DieWidthP = (int)dieWidth;
                GoldenDie.PadRoi = null;
                GoldenDie.PadRoi = new List<System.Windows.Rect>();
                foreach (Rectangle roi in PadRois)
                    GoldenDie.PadRoi.Add(new System.Windows.Rect(roi.X, roi.Y, roi.Width, roi.Height));

                Console.WriteLine("pad num：{0},{1}", Parameters.cameraParameters.Recipe.ProbeMarkRect.Count, PadRois.Count);
                Parameters.modeSetting = cameraParameters.Recipe.ModeSetting as SurfaceAOISetting;

                return 1;
            }
            catch (Exception e)
            {
                return -8;
            }
        }
        /// <summary>
        /// sort by rect.x 
        /// </summary>
        /// <param name="area1"></param>
        /// <param name="area2"></param>
        /// <returns></returns>
        private double GetDistan(System.Windows.Rect area1 ,System.Windows.Rect area2)
        {
            System.Windows.Point p1 = new System.Windows.Point(area1.X + area1.Width * 0.5, area1.Y + area1.Height * 0.5);
            System.Windows.Point p2 = new System.Windows.Point(area2.X + area2.Width * 0.5, area2.Y + area2.Height * 0.5);
            return Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y));
        }
        /// <summary>
        /// 综合：虚拟DIE和色差
        /// </summary>
        /// <param name="IndexSlice">行索引</param>
        /// <param name="IndexScan">列索引</param>
        /// <param name="ImageIndexWafer">该图片前面行的图片总数</param>
        /// <param name="JudgeShift">使用跨行索引，默认开偏移判定</param>
        /// <param name="IndexRef1">参考索引1,全局</param>
        /// <param name="IndexRef2">参考索引2，全局</param>
        /// <returns></returns>
        private int IndexRefs(int IndexSlice, int IndexScan,int ImageIndexWafer, bool JudgeShift,ref int IndexRef1, ref int IndexRef2)
        {
           IndexRef1 = 0;
           IndexRef2 = 0;

            //一行不足3个die，且当前图片Y和X对齐规划
            if (tt.ScanPlan.Slices[IndexSlice].Scans.Count <= 2 * modeSetting.ScanSequence[0].Repeat)
            {
                //当前图片满足Y和X对齐规划
                if (cameraParameters.Recipe.Wafer.FovMap[IndexScan + ImageIndexWafer].FovPathType == AutoReviewSystem.Data.PathType.OnlyXYEqualPath)
                {
                   if (tt.ScanPlan.scanInspection.Count < (tt.ScanPlan.scanInspection[0].ReapeatXY.X * tt.ScanPlan.scanInspection[0].ReapeatXY.Y))
                    {
                        return -11;
                    }
                    //***************进行跨行die规划***********//
                    int repeatx = (int)tt.ScanPlan.Slices[IndexSlice].Scans[IndexScan].ReapeatXY.X;
                    int repeaty = (int)tt.ScanPlan.Slices[IndexSlice].Scans[IndexScan].ReapeatXY.Y;
                    //跨行虚拟die
                    List<Scan> scanEnja = new List<Scan>();
                    foreach (Scan scan in tt.ScanPlan.scanInspection)
                    {
                        if (tt.ScanPlan.Slices[IndexSlice].Scans[IndexScan].EnjaVir
                            && Math.Abs(scan.IndexX - IndexScan) % repeatx == 0 && Math.Abs(scan.IndexY - IndexSlice) % repeaty == 0
                            && !(Math.Abs(scan.IndexX - IndexScan) < 0.001 && Math.Abs(scan.IndexY - IndexSlice) < 0.001))
                        {
                            scanEnja.Add(scan);
                        }
                    }
                    if (scanEnja.Count >= 2)
                    {
                        //选取最近的
                        scanEnja.Sort((x, y) => (GetDistan(x.Area, tt.ScanPlan.Slices[IndexSlice].Scans[IndexScan].Area)).CompareTo(GetDistan(y.Area, tt.ScanPlan.Slices[IndexSlice].Scans[IndexScan].Area)));
                        IndexRef1 = scanEnja[0].Index;
                        IndexRef2 = scanEnja[1].Index;
                        JudgeShift = true;
                        return 1;
                    }

                    if (IndexRef1 == 0 && IndexRef2 == 0) return -11;
                }
                else return -12;
            }

            //一行只有3个die,15个fov
            if (tt.ScanPlan.Slices[IndexSlice].Scans.Count<=15)
            {
                //默认的配置：
                if (IndexScan < modeSetting.ScanSequence[0].Repeat)
                {
                    IndexRef1 = modeSetting.ScanSequence[0].Repeat + IndexScan;
                    IndexRef2 = 2 * modeSetting.ScanSequence[0].Repeat + IndexScan;
                }
                else if (IndexScan > tt.ScanPlan.Slices[IndexSlice].Scans.Count - 1 - modeSetting.ScanSequence[0].Repeat)
                {
                    IndexRef1 = IndexScan - 2 * modeSetting.ScanSequence[0].Repeat;
                    IndexRef2 = IndexScan - modeSetting.ScanSequence[0].Repeat;
                }
                else
                {
                    IndexRef1 = IndexScan - modeSetting.ScanSequence[0].Repeat;
                    IndexRef2 = modeSetting.ScanSequence[0].Repeat + IndexScan;
                }
            }
            else
            {
                //默认的配置：//9.25修改
                if (IndexScan < 2 * modeSetting.ScanSequence[0].Repeat)
                {
                    IndexRef1 = modeSetting.ScanSequence[0].Repeat + IndexScan;
                    IndexRef2 = 2 * modeSetting.ScanSequence[0].Repeat + IndexScan;
                }
                else if (IndexScan > tt.ScanPlan.Slices[IndexSlice].Scans.Count - 1 - 2 * modeSetting.ScanSequence[0].Repeat)
                {
                    IndexRef1 = IndexScan - 2 * modeSetting.ScanSequence[0].Repeat;
                    IndexRef2 = IndexScan - modeSetting.ScanSequence[0].Repeat;
                }
                else
                {
                    IndexRef1 = IndexScan + modeSetting.ScanSequence[0].Repeat;
                    IndexRef2 = 2 * modeSetting.ScanSequence[0].Repeat + IndexScan;
                }
            }
            
            if ((!tt.ScanPlan.Slices[IndexSlice].Scans[IndexRef1].ScanVirDie && !tt.ScanPlan.Slices[IndexSlice].Scans[IndexRef2].ScanVirDie)
                && (IndexRef1 != 0 || IndexRef2 != 0))
            {
                IndexRef1 += ImageIndexWafer;
                IndexRef2 += ImageIndexWafer;
                return 1;
            }
            //特殊情况0：虚拟die(默认分配的存在虚拟die图)
            else if (tt.ScanPlan.Slices[IndexSlice].Scans[IndexRef1].ScanVirDie || tt.ScanPlan.Slices[IndexSlice].Scans[IndexRef2].ScanVirDie)
            {
                IndexRef1 = 0;
                IndexRef2 = 0;

                if (IndexScan < (tt.ScanPlan.Slices[IndexSlice].Scans.Count - 1 - 2 * modeSetting.ScanSequence[0].Repeat))
                {
                    for (int indexV = IndexScan; indexV < tt.ScanPlan.Slices[IndexSlice].Scans.Count; indexV += modeSetting.ScanSequence[0].Repeat)
                    {
                        IndexRef1 = indexV + modeSetting.ScanSequence[0].Repeat;
                        IndexRef2 = indexV + 2 * modeSetting.ScanSequence[0].Repeat;
                        if ((IndexRef1 > tt.ScanPlan.Slices[IndexSlice].Scans.Count - 1) || (IndexRef2 > tt.ScanPlan.Slices[IndexSlice].Scans.Count - 1))
                        {
                            IndexRef1 = 0;
                            IndexRef2 = 0;
                            continue;
                        }

                        if (!tt.ScanPlan.Slices[IndexSlice].Scans[IndexRef1].ScanVirDie && !tt.ScanPlan.Slices[IndexSlice].Scans[IndexRef2].ScanVirDie) break;
                        else
                        {
                            IndexRef1 = 0;
                            IndexRef2 = 0;
                        }
                    }

                }

                if ((IndexRef1 == 0 && IndexRef2 == 0) && IndexScan > 2 * modeSetting.ScanSequence[0].Repeat - 1)
                {
                    for (int indexV = IndexScan; indexV > 0; indexV -= modeSetting.ScanSequence[0].Repeat)
                    {
                        IndexRef1 = indexV - modeSetting.ScanSequence[0].Repeat;
                        IndexRef2 = indexV - 2 * modeSetting.ScanSequence[0].Repeat;
                        if (IndexRef1 < 1 || IndexRef2 < 1 || IndexRef1 > tt.ScanPlan.Slices[IndexSlice].Scans.Count - 1 || IndexRef2 > tt.ScanPlan.Slices[IndexSlice].Scans.Count - 1)
                        {
                            IndexRef1 = 0;
                            IndexRef2 = 0;
                            continue;
                        }
                        if (!tt.ScanPlan.Slices[IndexSlice].Scans[IndexRef1].ScanVirDie && !tt.ScanPlan.Slices[IndexSlice].Scans[IndexRef2].ScanVirDie) break;
                        else
                        {
                            IndexRef1 = 0;
                            IndexRef2 = 0;
                        }
                    }
                }
                //每行die数量不足支撑dietodie,则清空规划
                if (IndexRef1 >= tt.ScanPlan.Slices[IndexSlice].Scans.Count || IndexRef2 >= tt.ScanPlan.Slices[IndexSlice].Scans.Count)
                {
                    IndexRef1 = 0;
                    IndexRef2 = 0;
                };

                //***************满足同行虚拟die规划***********//
                if (IndexRef1 != 0 || IndexRef2 != 0)
                {
                    IndexRef1 += ImageIndexWafer;
                    IndexRef2 += ImageIndexWafer;
                    return 1;
                }
                else if (tt.ScanPlan.scanInspection.Count < (tt.ScanPlan.scanInspection[0].ReapeatXY.X * tt.ScanPlan.scanInspection[0].ReapeatXY.Y))
                {
                    return -13;
                }

                //***************进行跨行虚拟die规划***********//
                int repeatx = (int)tt.ScanPlan.Slices[IndexSlice].Scans[IndexScan].ReapeatXY.X;
                int repeaty = (int)tt.ScanPlan.Slices[IndexSlice].Scans[IndexScan].ReapeatXY.Y;
                //跨行虚拟die
                List<Scan> scanEnja = new List<Scan>();
                foreach (Scan scan in tt.ScanPlan.scanInspection)
                {
                    if (tt.ScanPlan.Slices[IndexSlice].Scans[IndexScan].EnjaVir
                        && Math.Abs(scan.IndexX - IndexScan) % repeatx == 0 && Math.Abs(scan.IndexY - IndexSlice) % repeaty == 0
                        && !(Math.Abs(scan.IndexX - IndexScan) <0.001 && Math.Abs(scan.IndexY - IndexSlice) < 0.001))
                    {
                        scanEnja.Add(scan);
                    }
                }
                if (scanEnja.Count >= 2)
                {
                    //选取最近的
                    scanEnja.Sort((x, y) => (GetDistan(x.Area, tt.ScanPlan.Slices[IndexSlice].Scans[IndexScan].Area)).CompareTo(GetDistan(y.Area, tt.ScanPlan.Slices[IndexSlice].Scans[IndexScan].Area)));
                    IndexRef1 = scanEnja[0].Index;
                    IndexRef2 = scanEnja[1].Index;
                    JudgeShift = true;
                    return 1;
                }

                if (IndexRef1 == 0 && IndexRef2 == 0) return -13;

            }
            else return -14;
            return -14;
        }
        /// <summary>
        /// 动态调整每张图的参数组大小
        /// </summary>
        /// <returns></returns>
        private int ImageArrayNum(int maxscans)
        {
            /*动态调整每张图的参数数量：
             * 条件1：有无probe检测区分
             * 条件2：每张图最多会包含的检测框数量，以及，一行最多的图数（由倍率决定）
             */
            int ImgAN = 0;
            try
            {
                bool probe = inspectionParameter.IsEnablePadErase;
                int NumImageX = (int)Math.Ceiling(fovX / cameraParameters.Recipe.Wafer.DiePitchX);
                int NumImageY = (int)Math.Ceiling(fovSize / cameraParameters.Recipe.Wafer.DiePitchY);

                int numX = 2 * NumImageX;
                int numY = 2 * NumImageY;
                //int maxscans = tt.ScanPlan.Slices[tt.ScanPlan.Slices.Count() / 2].Scans.Count;
                //20个roi的冗余
                ImgAN = (numX * numY + 20) * 6 + 5;
            
                //每张图pad总数：每个roi内pad区域存储：数量、ROI，共5个参数
                if (probe || PadRois.Count() > 0)
                {
                    int NumImgPad = 4*PadRois.Count() * (2*NumImageX+1) * (2*NumImageY+1) + 1;
                    ImgAN += NumImgPad;
                }

                //固定存储参数
                ImgAN += 20;
                if ((ImgAN * maxscans) > 2147483591 || ImgAN<21) return 0;
              
                return ImgAN;
            }
            catch (Exception e)
            {
                return ImgAN;
            }
        }
        /// <summary>
        /// recipe参数转换检测参数，目前只SurfaceAOI
        /// </summary>
        /// <param name="data"> recipe序列化成byte[]的数据作为输入</param>
        /// <returns>返回检测参数，返回的double[]作为inspection.dll里Inspection接口的InspectionParam检测参数输入（地址形式）
        ///         （返回数组大小，等于1个视为错误，recipe错误加载）
        /// </returns>
        public List<double[]> GetParam(byte[] data)
       {
            List<double[]> errs = new List<double[]>(1);
            errs.Add(new double[1]);
            try
            {
                //初始化参数
                int judgePara = IniPara(data);
                if (judgePara !=1) { errs[0][0] = judgePara; return errs; }
              
                //List<double[]>类型，每行参数一个double[]
                List<double[]> ImageRectLists = new List<double[]>();
                //每行前的图总量
                int ImageIndexWafer = 0;
                int number = this.ImageArrayNum(tt.ScanPlan.Slices[tt.ScanPlan.Slices.Count() / 2].Scans.Count);
                if (number<20) { errs[0][0] = -6; return errs; }

                for (int index = 0; index < tt.ScanPlan.Slices.Count; ++index)
                {
                    double[] ImageRects = new double[tt.ScanPlan.Slices[index].Scans.Count() * number + 20];

                    //每行的：前20个留作整个WAFER检测数据
                    WSTICParameters inspectionParameter = cameraParameters.Recipe.InspectionParameter as WSTICParameters;
                    //表面检测卡控参数
                    ImageRects[0] = inspectionParameter.BrightBlackJudge;//默认0时表示明场画面检测，1表示当前暗场检测，2表示明场同时检测<这个留着后续明暗场同时的现场>
                    ImageRects[1] = inspectionParameter.DeltaThreashold;
                    ImageRects[2] = inspectionParameter.BackThr; //暗场画面时表示背景最小检测灰阶阈值 （仅暗场画面使用，配合BrightBlackJudge ）
                    ImageRects[3] = inspectionParameter.DeltaBlack; //明场画面时表示暗缺陷的色差（仅明场画面使用，配合BrightBlackJudge ）
                    ImageRects[4] = inspectionParameter.SurfaceOpen;
                    ImageRects[5] = inspectionParameter.MinDetectWidth;
                    ImageRects[6] = inspectionParameter.MinDetectHeight;
                    ImageRects[7] = inspectionParameter.MinDetectArea;
                    ImageRects[8] = inspectionParameter.CThreashold;  //色差，兼容
                    //pad检测参数
                    ImageRects[9] = inspectionParameter.IsEnablePadErase == true ? 1 : 0;                 
                    ImageRects[10] = inspectionParameter.ErodeValue;
                    ImageRects[11] = inspectionParameter.ThresholdValue;
                    ImageRects[12] = inspectionParameter.OpenValue;
                    //其余参数
                    ImageRects[15] = number;
                    int IproiSize = 4 * PadRois.Count() + 6;
                    ImageRects[16] = IproiSize;  //单个die内的pad框数据大小
                    ImageRects[17] = inspectionParameter.IsEnableResultImageSave == true ? 1 : 0;

                    int ImageIndex = -1;
                    for (int index2 = 0; index2 < tt.ScanPlan.Slices[index].Scans.Count; ++index2)
                    {

                        ImageIndex++;
                        int IndexRef1 = 0;
                        int IndexRef2 = 0;
                        bool JudgeShift = false;
                        int JuPlan = IndexRefs(index, index2, ImageIndexWafer, JudgeShift, ref IndexRef1, ref IndexRef2);

                        if (JuPlan !=1)
                        { errs[0][0] = JuPlan; return errs; };
                       if(IndexRef1>= cameraParameters.Recipe.Wafer.FovMap.Count() || IndexRef2 >= cameraParameters.Recipe.Wafer.FovMap.Count()) 
                        { errs[0][0] = -4; return errs; };
                        /////////*****************************************************************图号计算**********************************************************************/////////

                        //存储触发检测的图片点数据
                        Dictionary<int, int> TriggerInspetion = new Dictionary<int, int>();
                        TriggerInspetion.Add(IndexRef1, IndexRef2 );
                        TriggerInspetions.Add(ImageIndex + ImageIndexWafer, TriggerInspetion);

                        SWM.Scan scan = tt.ScanPlan.Slices[index].Scans[index2];
                        List<IPROI> iproiList;
                        tt.IPROIDictionary.TryGetValue(scan, out iproiList);

                        ImageRects[ImageIndex * number + 20] = iproiList.Count;
                        //使用新接口，这两个图片索引参数废弃，第一个改为判断（跨行索引的图，默认开偏移判断）
                        ImageRects[ImageIndex * number + 21] = IndexRef1- ImageIndexWafer;  //9.27：跨行规划暂未考虑;检测对应的参数问题
                        ImageRects[ImageIndex * number + 22] = IndexRef2- ImageIndexWafer;
                        ImageRects[ImageIndex * number + 23] = Parameters.cameraParameters.Recipe.Wafer.FovMap[ImageIndex + ImageIndexWafer].TypeForChrEdge == AutoReviewSystem.Data.DiePosition.ChrWaferEdge ? 1 : 0; //进行色差复判的图
                        //测试
                        for (int index3 = 0; index3 < iproiList.Count; ++index3)
                        {
                            GoldenROI GROI = new GoldenROI();
                            GROI.ScanPosition = modeSetting.ScanPositions[modeSetting.ConvertScanIndex(index, index2)];
                            GROI.Iroi = iproiList[index3];
                            iproiList[index3].GoldROI = GROI;

                            GoldenDie GDie = new GoldenDie();
                            if (GoldenDie.PadRoi.Count > 0 && (GROI.XRel > GoldenDie.DieWidthP || GROI.YRel > GoldenDie.DieHeightP))
                            { errs[0][0] = -2; return errs; }

                            List<System.Windows.Rect> IP_PadRoi_GD = new List<System.Windows.Rect>();
                            System.Windows.Rect IProiGD = new System.Windows.Rect(GROI.XRel, dieHeight - GROI.YRel, iproiList[index3].ImageArea.Width, iproiList[index3].ImageArea.Height);
                            bool judge = GDie.Contain(IProiGD, ref IP_PadRoi_GD);


                            List<System.Windows.Rect> IP_PadRoi_Image = new List<System.Windows.Rect>();
                            if (judge == true)
                            {
                                GDie.GetIPad(GROI, IP_PadRoi_GD, ref IP_PadRoi_Image);
                            }

                            ImageRects[ImageIndex * number + IproiSize * index3 + 25] = (int)(iproiList[index3].ImageArea.X);
                            ImageRects[ImageIndex * number + IproiSize * index3 + 26] = (int)(iproiList[index3].ImageArea.Y);
                            ImageRects[ImageIndex * number + IproiSize * index3 + 27] = (int)(iproiList[index3].ImageArea.Width);
                            ImageRects[ImageIndex * number + IproiSize * index3 + 28] = (int)(iproiList[index3].ImageArea.Height);
                            ImageRects[ImageIndex * number + IproiSize * index3 + 29] = (int)(iproiList[index3].IPROIType == Base.ID_IPROIType.INCLUDE ? 0 : 1);
                            ImageRects[ImageIndex * number + IproiSize * index3 + 30] = IP_PadRoi_Image.Count;

                            for (int index4 = 0; index4 < IP_PadRoi_Image.Count; index4++)
                            {
                                ImageRects[ImageIndex * number + IproiSize * index3 + 31 + index4 * 4] = IP_PadRoi_Image[index4].X;
                                ImageRects[ImageIndex * number + IproiSize * index3 + 32 + index4 * 4] = IP_PadRoi_Image[index4].Y;
                                ImageRects[ImageIndex * number + IproiSize * index3 + 33 + index4 * 4] = IP_PadRoi_Image[index4].Width;
                                ImageRects[ImageIndex * number + IproiSize * index3 + 34 + index4 * 4] = IP_PadRoi_Image[index4].Height;
                            }
                        }
                    }
                    ImageIndexWafer += ImageIndex + 1;
                    ImageRectLists.Add(ImageRects);

                 }                   
                 if (ImageRectLists.Count() < 1) { errs[0][0] = -6; return errs; }
               
                DateTime startime1 = DateTime.Now;
                bool result = RenewTriggerInspetions(ImageRectLists,number);
                if (!result) { errs[0][0] = -4; return errs; }
                DateTime endtime1 = DateTime.Now;
                Console.WriteLine("重新计算触发接口图片：{0}ms", (endtime1 - startime1).TotalMilliseconds);
                return ImageRectLists;
   
            }
            catch (Exception ex)
            {
                errs[0][0] = -8; return errs;
            }
        }

        /// <summary>
        /// 获取完整Die图像，该完整die图像可用于：检测完成后缺陷分区卡控、probemark检测前识别区域标注、probemark检测前pad区域标注等
        /// 新的Recipe建立完成后调用（同一recipe不变更时，反复跑产品时<即有当前recipe下完整Die图像>，不需要调用））
        /// </summary>
        /// <param name="data">输入参数：CameraParameters的序列化数组</param>
        /// <param name="DieIndex">输入参数：指定为GoldenDie的索引号（为：Recipe.Wafer.Map中Die类中的Index）</param>
        /// <param name="dieImage">输出参数： 调用端 new List<int>() ,
        ///             用来存储需要获取图像的CapturePoints索引号
        ///             （拍照后的图片，请按该顺序存储！！首地址作为inspection.dll里GoldImage接口的imageBuffer）</param>
        /// <returns>返回goldendie参数，返回的double[]作为inspection.dll里GoldImage接口的GoldParam输入（地址形式）      
        ///         （返回数组大小，等于1个视为错误，recipe错误加载）
        /// </returns>
        /// 
        public double[] GetGoldenImage(byte[] data, System.Drawing.Point DieIndex, ref List<int> dieImage)
        {
            try
            {
                //初始化参数
                int judgePara = IniPara(data);
                if (judgePara != 1) return new double[1];
                //接收recipe参数并保存，目前只SurfaceAOI
                double fovX = cameraParameters.Recipe.FieldType.FieldOfViewWidth * 0.001;
                double fovSize = cameraParameters.Recipe.FieldType.FieldOfViewHeight * 0.001;

                int ImageIndex = -1;
                Dictionary<int, List<int>> DieImageIndex = new Dictionary<int, List<int>>();
                Dictionary<int, System.Drawing.Point> ImgIndex = new Dictionary<int, System.Drawing.Point>();
                for (int index = 0; index < tt.ScanPlan.Slices.Count; ++index)
                {
                    for (int index2 = 0; index2 < tt.ScanPlan.Slices[index].Scans.Count; ++index2)
                    {
                        ImageIndex++;

                        SWM.Scan scan = tt.ScanPlan.Slices[index].Scans[index2];
                        List<IPROI> iproiList;
                        tt.IPROIDictionary.TryGetValue(scan, out iproiList);
                        ImgIndex.Add(ImageIndex, new System.Drawing.Point(index2, index));

                        for (int index3 = 0; index3 < iproiList.Count; ++index3)
                        {
                            GoldenROI GROI = new GoldenROI();
                            GROI.ScanPosition = modeSetting.ScanPositions[modeSetting.ConvertScanIndex(index, index2)];
                            GROI.Iroi = iproiList[index3];
                            iproiList[index3].GoldROI = GROI;

                            int curdieIndex = iproiList[index3].DieID;
                            if (DieImageIndex.ContainsKey(curdieIndex))
                            {
                                if (!DieImageIndex[curdieIndex].Contains(ImageIndex))
                                {
                                    List<int> dieI = DieImageIndex[curdieIndex];
                                    dieI.Add(ImageIndex);
                                    DieImageIndex[curdieIndex] = dieI;
                                }
                            }
                            else
                            {
                                List<int> dieI = new List<int>();
                                dieI.Add(ImageIndex);
                                DieImageIndex.Add(curdieIndex, dieI);
                            }
                        }

                        tt.IPROIDictionary[scan] = iproiList;
                    }
                }
                //（c#） 1输出给上位机拍照点位：GoldImagePoints的索引和goldenDie参数
                //（c++）2上位机输入图片，和1中输出参数，根据参数和图片提取goldenDie图片
                if (dieImage.Count != 0) dieImage = null;  //GoldImagePoints的索引
                DieImageIndex.TryGetValue(DieIndex.X * 1000 + DieIndex.Y, out dieImage);
                if (dieImage.Count < 1) return new double[1];

                List<IPROI> ImageROI = new List<IPROI>();

                for (int i = 0; i < dieImage.Count; i++)
                {
                    System.Drawing.Point temp;
                    ImgIndex.TryGetValue(dieImage[i], out temp);
                    SWM.Scan scan = tt.ScanPlan.Slices[temp.Y].Scans[temp.X];
                    List<IPROI> iproiList;
                    tt.IPROIDictionary.TryGetValue(scan, out iproiList);
                    for (int j = 0; j < iproiList.Count; j++)
                    {
                        if (iproiList[j].DieID == DieIndex.X * 1000 + DieIndex.Y)
                        {
                            ImageROI.Add(iproiList[j]);
                        }
                    }
                }

                //参数转换，输出给c++
                //前10个留出
                double[] ImageRects = new double[ImageROI.Count() * 10 + 10];
                ImageRects[0] = cameraParameters.Width;  //图片宽
                ImageRects[1] = cameraParameters.Height; //图片高
                ImageRects[2] = (int)(cameraParameters.Recipe.Wafer.DieWidth / (1.0 * fovX / cameraParameters.Width));  //Die宽(pixel)
                ImageRects[3] = (int)(cameraParameters.Recipe.Wafer.DieHeight / (1.0 * fovSize / cameraParameters.Height)); //Die高(pixel)
                ImageRects[4] = ImageROI.Count;//IPROI的数量

                OpenCvSharp.Rect Img = new OpenCvSharp.Rect(0, 0, cameraParameters.Width, cameraParameters.Height);
                OpenCvSharp.Rect ImgDie = new OpenCvSharp.Rect(0, 0, (int)ImageRects[2], (int)ImageRects[3]);

                for (int j = 0; j < ImageROI.Count; j++)
                {
                    OpenCvSharp.Rect ImageArea = new OpenCvSharp.Rect((int)ImageROI[j].ImageArea.X, (int)ImageROI[j].ImageArea.Y, (int)ImageROI[j].ImageArea.Width, (int)ImageROI[j].ImageArea.Height);

                    OpenCvSharp.Rect rectImg = OpenCvSharp.Rect.Intersect(ImageArea, Img);

                    OpenCvSharp.Rect rectDie = new OpenCvSharp.Rect(ImageROI[j].GoldROI.XRel, (int)(ImageRects[3] - ImageROI[j].GoldROI.YRel), rectImg.Width, rectImg.Height);
                    OpenCvSharp.Rect rectDieImg = OpenCvSharp.Rect.Intersect(rectDie, ImgDie);

                    if (rectDieImg.Width != rectImg.Width || rectDieImg.Height != rectImg.Height)
                    {
                        rectImg.Width = rectDieImg.Width;
                        rectImg.Height = rectDieImg.Height;
                        rectImg = OpenCvSharp.Rect.Intersect(rectImg, Img);
                    }

                    ImageRects[9 + 10 * j + 1] = rectImg.X;
                    ImageRects[9 + 10 * j + 2] = rectImg.Y;
                    ImageRects[9 + 10 * j + 3] = rectImg.Width;
                    ImageRects[9 + 10 * j + 4] = rectImg.Height;

                    ImageRects[9 + 10 * j + 5] = rectDieImg.X;
                    ImageRects[9 + 10 * j + 6] = rectDieImg.Y;


                    if ((ImageRects[9 + 10 * j + 5] + ImageRects[9 + 10 * j + 3] - 1) > (ImageRects[2] - 1))
                    {
                        return new double[1];
                    }
                    if ((ImageRects[9 + 10 * j + 6] + ImageRects[9 + 10 * j + 4] - 1) > (ImageRects[3] - 1))
                    {
                        return new double[1];
                    }
                }

                if (dieImage == null) return new double[1];

                return ImageRects;

            }

            catch (Exception ex)
            {
                return new double[1];
            }
        }
        /// <summary>
        /// Golden Die检测模式检测（注：拍照及map roi区域仍然是dietodie模式，但图片采用golden die）
        /// </summary>
        /// <param name="data">输入参数：CameraParameters的序列化数组</param>
        /// <returns>返回检测参数，返回的double[]作为inspection.dll里InspectionGold接口的InspectionParam检测参数输入（地址形式）
        ///         （返回数组大小，等于1个视为错误，recipe错误加载）
        /// </returns>
        public double[] GetGoldParam(byte[] data)
        {
            try
            {
                //初始化参数
                int judgePara = IniPara(data);
                if (judgePara != 1) { return new double[1]; }

                //接收recipe参数并保存，目前只SurfaceAOI
                if (cameraParameters.Recipe != null)
                {
                    double fovX = cameraParameters.Recipe.FieldType.FieldOfViewWidth * 0.001;
                    double fovSize = cameraParameters.Recipe.FieldType.FieldOfViewHeight * 0.001;
                    double diePitchX = cameraParameters.Recipe.Wafer.DiePitchX;
                    double diePitchY = cameraParameters.Recipe.Wafer.DiePitchY;
                    double dieWidth = cameraParameters.Recipe.Wafer.DieWidth / (1.0 * fovX / cameraParameters.Width);  //Die宽(pixel)
                    double dieHeight = cameraParameters.Recipe.Wafer.DieHeight / (1.0 * fovSize / cameraParameters.Height); //Die高(pixel)

                    //根据单张图片die数量，动态单图数组上线
                    //每2503个留作图像数据，每个ROI区域5个参数
                    int number = 400 * 25 + 3;
                    int numX = 2 * (int)Math.Floor(diePitchX / fovX);
                    int numY = 2 * (int)Math.Floor(diePitchY / fovSize);
                    if (numX * numY > 300 && numX * numY < 1000)
                    {
                        number = 1000 * 25 + 3;
                        if (cameraParameters.Recipe.ImageCount > 6000) return new double[1];
                    }

                    if (numX * numY >= 1000 && numX * numY < 2000)
                    {
                        number = 2000 * 25 + 3;
                        if (cameraParameters.Recipe.ImageCount > 3000) return new double[1];
                    }

                    if (numX * numY >= 2000 && numX * numY < 5000)
                    {
                        number = 5000 * 25 + 3;
                        if (cameraParameters.Recipe.ImageCount > 1000) return new double[1];
                    }
                    if (numX * numY > 5000) return new double[1];

                    if (cameraParameters.Recipe.RunMode == RunMode.SurfaceAOI)
                    {
                        Directory.CreateDirectory(string.Format("{0}\\r", (object)Environment.CurrentDirectory));
                        SWMCore tt = GetMapInfo();
                       
                        double[] ImageRects = new double[tt.IPROIDictionary.Count() * number + 20];

                        //前20个留作整个WAFER检测数据（目前只用前10个）
                        if (cameraParameters.Recipe.InspectionParameterType == InspectionMode.WSTIC)
                        {
                            WSTICParameters inspectionParameter2 = cameraParameters.Recipe.InspectionParameter as WSTICParameters;
                            //ImageRects[0] = Parameters.MilApplication;
                            //ImageRects[1] = Parameters.MilSystem;
                            ImageRects[2] = inspectionParameter2.MinDetectWidth;
                            ImageRects[3] = inspectionParameter2.MinDetectHeight;
                            ImageRects[4] = inspectionParameter2.DeltaThreashold;
                            ImageRects[5] = inspectionParameter2.ErodeValue;
                            ImageRects[6] = inspectionParameter2.IsEnablePadErase == true ? 1 : 0;
                            ImageRects[7] = inspectionParameter2.IsEnableResultImageSave == true ? 1 : 0;
                            ImageRects[8] = inspectionParameter2.OpenValue;
                            ImageRects[9] = inspectionParameter2.MinDetectArea;
                            ImageRects[10] = inspectionParameter2.ThresholdValue;
                        }

                        ImageRects[11] = dieWidth;
                        ImageRects[12] = dieHeight;
                        ImageRects[13] = number;
                        SurfaceAOISetting modeSetting = cameraParameters.Recipe.ModeSetting as SurfaceAOISetting;
                        int ImageIndex = -1;
                        //遍历每行的每张图序，按序存取数据，每张图数据
                        //         前5个：（ROI数量，对比图1每行的图序，对比图2每行的图序）
                        //         后：每5个一组（RoiX/RoiY/RoiW/RoiH/Roi类型）
                        for (int index = 0; index < tt.ScanPlan.Slices.Count; ++index)
                        {
                            int n = modeSetting.ScanSequence[index].Repeat;

                            //存储触发检测的图片点数据

                            for (int index2 = 0; index2 < tt.ScanPlan.Slices[index].Scans.Count; ++index2)
                            {

                                ImageIndex++;
                                SWM.Scan scan = tt.ScanPlan.Slices[index].Scans[index2];
                                List<IPROI> iproiList;
                                tt.IPROIDictionary.TryGetValue(scan, out iproiList);

                                ImageRects[ImageIndex * number + 20] = iproiList.Count;
                                for (int index3 = 0; index3 < iproiList.Count; ++index3)
                                {
                                    GoldenROI GROI = new GoldenROI();
                                    GROI.ScanPosition = modeSetting.ScanPositions[modeSetting.ConvertScanIndex(index, index2)];
                                    GROI.Iroi = iproiList[index3];
                                    iproiList[index3].GoldROI = GROI;

                                    ImageRects[ImageIndex * number + 7 * index3 + 23] = GROI.XRel;
                                    ImageRects[ImageIndex * number + 7 * index3 + 24] = dieHeight - GROI.YRel; //转换成左上角
                                    ImageRects[ImageIndex * number + 7 * index3 + 25] = (int)Math.Round(iproiList[index3].ImageArea.X);
                                    ImageRects[ImageIndex * number + 7 * index3 + 26] = (int)Math.Round(iproiList[index3].ImageArea.Y);
                                    ImageRects[ImageIndex * number + 7 * index3 + 27] = (int)Math.Round(iproiList[index3].ImageArea.Width);
                                    ImageRects[ImageIndex * number + 7 * index3 + 28] = (int)Math.Round(iproiList[index3].ImageArea.Height);
                                    ImageRects[ImageIndex * number + 7 * index3 + 29] = (int)(iproiList[index3].IPROIType == Base.ID_IPROIType.INCLUDE ? 0 : 1);


                                }
                            }
                        }
                        return ImageRects;
                    }
                }

                return new double[1];
            }
            catch (Exception ex)
            {
                return new double[1];
            }
        }
        /// <summary>
        /// 启动map缩略图生成,生成固定大小缩略图（20500*20500）
        /// </summary>
        /// <param name="data">输入参数：CameraParameters的序列化数组</param>
        /// <param name="ImagePath">wafer全部图片存放的主目录路径：实际读取时，按照ImagePath//s _t _r 1//图号.format 的路径读取 </param>
        /// <param name="format">图片格式：bmp、jpg、png</param>
        /// <returns>返回生成固定大小缩略图（20500*20500）</returns>
        public byte[] GetMapImage(byte[] data, string ImagePath, int MapSize, string format)
        {
            int MapImgW = MapSize;
            byte[] MapImage = new byte[MapImgW * MapImgW];
            try
            {
                //初始化参数
                int judgePara = IniPara(data);
                if (judgePara != 1) return MapImage;
               
                double fovX = cameraParameters.Recipe.FieldType.FieldOfViewWidth * 0.001;
                double fovSize = cameraParameters.Recipe.FieldType.FieldOfViewHeight * 0.001;
                double diePitchX = cameraParameters.Recipe.Wafer.DiePitchX;
                double diePitchY = cameraParameters.Recipe.Wafer.DiePitchY;
                double dieWidth = cameraParameters.Recipe.Wafer.DieWidth / (1.0 * fovX / cameraParameters.Width);  //Die宽(pixel)
                double dieHeight = cameraParameters.Recipe.Wafer.DieHeight / (1.0 * fovSize / cameraParameters.Height); //Die高(pixel)

                int scrWidth = (int)Math.Ceiling(cameraParameters.Recipe.Wafer.ScribeLaneX / (1.0 * fovX / cameraParameters.Width));  //Die宽(pixel)
                int scrHeight = (int)Math.Ceiling(cameraParameters.Recipe.Wafer.ScribeLaneY / (1.0 * fovSize / cameraParameters.Height)); //Die高(pixel)

                double pixSizeX = 1000 * (1.0 * fovX / cameraParameters.Width);
                double pixSizeY = 1000 * (1.0 * fovSize / cameraParameters.Height);
                   
    
                double scale = (MapImgW - 500) / (Math.Max(tt.Wafer.Area.Width, tt.Wafer.Area.Height) / pixSizeX);
                Mat MapMat = new Mat(MapImgW, MapImgW, MatType.CV_8UC1, new Scalar(255));//创建一个红色的图片

                        //根据单张图片die数量，动态单图数组上线
                        //每2503个留作图像数据，每个ROI区域5个参数

                int numX = 2 * (int)Math.Floor(fovX / diePitchX);
                int numY = 2 * (int)Math.Floor(fovSize / diePitchY);

                        //更改成List<double[]>类型，每行参数一个double[]
                List<double[]> ImageRectLists = new List<double[]>();
                SurfaceAOISetting modeSetting = cameraParameters.Recipe.ModeSetting as SurfaceAOISetting;

                        //遍历每行的每张图序，按序存取数据，每张图数据
                        //         前5个：（ROI数量，对比图1每行的图序，对比图2每行的图序）
                        //         后：每5个一组（RoiX/RoiY/RoiW/RoiH/Roi类型）


                        //每行前的图总量
                 int ImageIndexWafer = 0;
                 OpenCvSharp.Rect imageROI = new OpenCvSharp.Rect(0, 0, cameraParameters.Width, cameraParameters.Height);
                 OpenCvSharp.Rect MapROI = new OpenCvSharp.Rect(0, 0, MapMat.Width, MapMat.Height);
                 for (int index = 0; index < tt.ScanPlan.Slices.Count; ++index)
                 {
                      int ImageIndex = -1;
                      int n = modeSetting.ScanSequence[index].Repeat;

                      for (int index2 = 0; index2 < tt.ScanPlan.Slices[index].Scans.Count; ++index2)
                      {
                           ImageIndex++;


                           SWM.Scan scan = tt.ScanPlan.Slices[index].Scans[index2];
                           List<IPROI> iproiList;
                           tt.IPROIDictionary.TryGetValue(scan, out iproiList);

                           Mat img = new Mat();
                           string Path = ImagePath + string.Format("\\s {0}_t {1}_r {2}\\{3}.", index, tt.ScanPlan.Slices[index].Scans.Count, modeSetting.ScanSequence[index].Repeat, ImageIndex + ImageIndexWafer);
                            Path += "jpg";// format;
                           img = Cv2.ImRead(Path, 0);
                           if (img.Width == 0) continue;

                           for (int index3 = 0; index3 < iproiList.Count; ++index3)
                                {
                                    //缩略图
                                    if (iproiList[index3].IPROIType == Base.ID_IPROIType.INCLUDE)
                                    {
                                        //
                                        OpenCvSharp.Rect SrcROI = new OpenCvSharp.Rect((int)(iproiList[index3].ImageArea.X), (int)(iproiList[index3].ImageArea.Y),
                                      (int)(iproiList[index3].ImageArea.Width + scrWidth), (int)(iproiList[index3].ImageArea.Height + scrHeight));
                                        SrcROI = OpenCvSharp.Rect.Intersect(imageROI, SrcROI);

                                        OpenCvSharp.Rect DstROI = new OpenCvSharp.Rect((int)(scale * (iproiList[index3].Area.X - tt.Wafer.Area.X) / pixSizeX),
                                            MapImgW - (int)(scale * (iproiList[index3].Area.Height) / pixSizeY) - (int)(scale * (iproiList[index3].Area.Y - tt.Wafer.Area.Y) / pixSizeY),
                                             (int)Math.Ceiling(scale * (iproiList[index3].Area.Width + cameraParameters.Recipe.Wafer.ScribeLaneX * 1000) / pixSizeX),
                                             (int)Math.Ceiling(scale * (iproiList[index3].Area.Height + cameraParameters.Recipe.Wafer.ScribeLaneY * 1000) / pixSizeY));

                                        DstROI = OpenCvSharp.Rect.Intersect(MapROI, DstROI);

                                        if (DstROI.Height > 0 && DstROI.Width > 0 && SrcROI.Width > 0 && SrcROI.Height > 0)
                                        {
                                            Mat temp = new Mat(img, SrcROI);
                                            Mat temp2 = new Mat();
                                            Cv2.Resize(temp, temp2, new OpenCvSharp.Size(DstROI.Width, DstROI.Height));
                                            Mat pos = new Mat(MapMat, DstROI);
                                            temp2.CopyTo(pos);
                                        }
                                    }
                                }
                      }
                      ImageIndexWafer += ImageIndex + 1;
                   
                }
                 Marshal.Copy(MapMat.Data, MapImage, 0, MapImage.Length);
              
                 return MapImage;
            }
            catch (Exception ex)
            {
                return MapImage;
            }
        }
        /// <summary>
        /// 切换recipe（即重新调用GetParam前）调用
        /// </summary>
        /// <returns>正确释放,返回1；清空失败，返回-1；</returns>
        ///// 
        public int DeleteMIL()
        {
            try
            {
                Parameters.cameraParameters = null;
                Parameters.modeSetting = null;
                Parameters.tt = null;
                Parameters.TriggerInspetions = null;
                Parameters.TriggerInspetions = new Dictionary<int, Dictionary<int, int>>();

                GoldenDie.DieHeightP = 0;
                GoldenDie.DieWidthP = 0;
                GoldenDie.PadRoi = null;
   
               return 1;
            }
            catch (Exception ex)
            {
               return -1;
            }
        }

        private SWMCore GetMapInfo()
        {

            try
            {
                AutoReviewSystem.Data.Wafer wafer1 = cameraParameters.Recipe.Wafer;
                SWM.Wafer wafer = new SWM.Wafer((uint)(wafer1.Diameter * 500.0));
                List<SWM.Die> dieList = new List<SWM.Die>();
               
                foreach (AutoReviewSystem.Data.Slice slice in cameraParameters.Recipe.Wafer.Slices)
                    dieList.AddRange((IEnumerable<SWM.Die>)slice.ConvertAll<SWM.Die>((Converter<AutoReviewSystem.Data.Die, SWM.Die>)(die => new SWM.Die(wafer, new System.Windows.Rect(die.MapBounds.X * 1000.0, die.MapBounds.Y * 1000.0, die.MapBounds.Width * 1000.0, die.MapBounds.Height * 1000.0))
                    {
                        ID = die.Index.X * 1000 + die.Index.Y,
                    })));
                wafer.Dies = dieList;
               
                System.Windows.Rect rect1 = dieList[0].Area;
                foreach (SWM.Die die in dieList)
                    rect1 = System.Windows.Rect.Union(rect1, die.Area);
                wafer.Area = rect1;
                SurfaceAOISetting modeSetting = cameraParameters.Recipe.ModeSetting as SurfaceAOISetting;
                ScanPlan plan = new ScanPlan(new System.Windows.Rect(wafer1.MapBounds.X * 1000.0, wafer1.MapBounds.Y * 1000.0, wafer1.MapBounds.Width * 1000.0, wafer1.MapBounds.Height * 1000.0));
                plan.Slices = ((IEnumerable<SurfaceAOISetting.Sequence>)modeSetting.ScanSequence).ToList<SurfaceAOISetting.Sequence>().ConvertAll<SWM.Slice>((Converter<SurfaceAOISetting.Sequence, SWM.Slice>)(seq =>
                {
                    List<System.Windows.Rect> rectList = new List<System.Windows.Rect>();
                    System.Windows.Rect rect = new System.Windows.Rect(seq.FOV[0].X, seq.FOV[0].Y, seq.FOV[0].Width, seq.FOV[0].Height);
                    rectList.Add(rect);
                    for (int index = 1; index < seq.FOV.Length; ++index)
                    {
                        System.Windows.Rect rect2 = new System.Windows.Rect(seq.FOV[index].X, seq.FOV[index].Y, seq.FOV[index].Width, seq.FOV[index].Height);
                        rect = System.Windows.Rect.Union(rect, rect2);
                        rectList.Add(rect2);
                    }
                    SWM.Slice slice = new SWM.Slice(plan, rect);
                    slice.Scans = rectList.ConvertAll<Scan>((Converter<System.Windows.Rect, Scan>)(s => new Scan(slice, s)));

                    return slice;
                }));
                wafer.FovMap = wafer1.FovMap;
           
                return new SWMCore(wafer, plan);
            }
            catch (Exception ex)
            {
                // Program.Log.Error((object)string.Format("Processor.{1}(): {2}\r\n{3}", (object)MethodBase.GetCurrentMethod().Name, (object)ex.Message, (object)ex.StackTrace));
            }
            return (SWMCore)null;
        }

        private bool RemoveIndexRef(int index, int indexRef, int refI,
            ref Dictionary<int, Dictionary<int, int>> TriggerInspetionsbak,
            ref bool[] InspecJudge, ref int[] InspecNums)
        {
            if (index < 0 || index >= InspecNums.Length ||
                indexRef < 0 || indexRef >= InspecJudge.Length)
            {
                throw new IndexOutOfRangeException(
                    string.Format("RemoveIndexRef索引越界：index={0}, indexRef={1}, count={2}",
                        index, indexRef, InspecJudge.Length));
            }

            // 自身引用不需要删除自身Trigger。
            if (index == indexRef)
            {
                InspecJudge[indexRef] = true;
                return false;
            }

            bool indexRefIsActive = TriggerInspetionsbak.ContainsKey(indexRef);

            // 仍负责检测其它图片的Trigger必须保留，否则会造成覆盖链断裂。
            if (indexRefIsActive && InspecNums[indexRef] != 0)
            {
                InspecJudge[indexRef] = true;
                return false;
            }

            // indexRef只检测自身且尚未被接管，改由当前index负责检测。
            if (indexRefIsActive && !InspecJudge[indexRef])
            {
                TriggerInspetionsbak.Remove(indexRef);
                InspecJudge[indexRef] = true;
                InspecNums[index] |= refI;
                return true;
            }

            // indexRef已不在活动Trigger表，但尚未记录覆盖关系，由当前index接管。
            if (!indexRefIsActive && !InspecJudge[indexRef])
            {
                InspecJudge[indexRef] = true;
                InspecNums[index] |= refI;
            }

            return false;
        }

        private bool RenewTriggerInspetions(List<double[]> ImageRectLists, int number)
        {
            try
            {
                // 深拷贝活动Trigger表，验证成功前不修改对外发布的全局表。
                Dictionary<int, Dictionary<int, int>> TriggerInspetionsbak =
                    TriggerInspetions.ToDictionary(
                        pair => pair.Key,
                        pair => new Dictionary<int, int>(pair.Value));
                //表示图片是否被检测
                bool[] InspecJudge = Enumerable.Repeat(false, TriggerInspetions.Count).ToArray();//Enumerable.Repeat(false, cameraParameters.Recipe.ImageCount).ToArray();
                //表示作为key时候，需要检测的图数:默认0，ref1则加1，ref2则加2
                int[] InspecNums = Enumerable.Repeat(0, TriggerInspetions.Count).ToArray();//Enumerable.Repeat(0, cameraParameters.Recipe.ImageCount).ToArray();
                //每行前的图总量
                int ImageIndexWafer = 0;
                Console.WriteLine("读取recipe图数量{0}", cameraParameters.Recipe.ImageCount);
                //用list存图片有没有被检测过，默认false，一旦被非虚拟图检测过，则赋true
                //在对应的参数里设置：0表示只检测当前图，1表示检测当前图和前一张，2表示测当前图和后一张，3表示全检测（原本用来判定是否偏移的参数：只中间图的参数有）
                for (int index = 0; index < tt.ScanPlan.Slices.Count; ++index)
                {
                    
                    int ImageIndexSlice = -1;
                    for (int index2 = 0; index2 < tt.ScanPlan.Slices[index].Scans.Count; ++index2)
                    {
                        ImageIndexSlice++;
                        if (!tt.ScanPlan.Slices[index].Scans[index2].ScanVirDie)
                        {
                            Dictionary<int, int> indexs;
                            TriggerInspetionsbak.TryGetValue(ImageIndexWafer + ImageIndexSlice, out indexs);
                            int indexRef1;
                            int indexRef2;
                            //非虚拟die检测
                            if (indexs != null)
                            {
                                foreach (var item in indexs)
                                {
                                    indexRef1 = item.Key;
                                    indexRef2 = item.Value;
                                  
                                    //判断当前是否要删除，以及删除后状态变更
                                    bool remove1 = RemoveIndexRef(ImageIndexWafer + ImageIndexSlice, indexRef1, 1, ref TriggerInspetionsbak, ref InspecJudge, ref InspecNums);
                                    bool remove2 = RemoveIndexRef(ImageIndexWafer + ImageIndexSlice, indexRef2, 2, ref TriggerInspetionsbak, ref InspecJudge, ref InspecNums);
                                   
                                    //更新检测状态
                                    if (InspecNums[ImageIndexWafer + ImageIndexSlice] ==0) ImageRectLists[index][ImageIndexSlice * number + 24] = 0;
                                    else if(InspecNums[ImageIndexWafer + ImageIndexSlice] == 1) ImageRectLists[index][ImageIndexSlice * number + 24] = 1;
                                    else if (InspecNums[ImageIndexWafer + ImageIndexSlice] == 2) ImageRectLists[index][ImageIndexSlice * number + 24] = 2;
                                    else if (InspecNums[ImageIndexWafer + ImageIndexSlice] == 3) ImageRectLists[index][ImageIndexSlice * number + 24] = 3;

                                    if(remove1) ImageRectLists[index][(indexRef1- ImageIndexWafer) * number + 24] = -1; //表示参数无效
                                    if (remove2) ImageRectLists[index][(indexRef2 - ImageIndexWafer) * number + 24] = -1; //表示参数无效
                                }
                            }
                            else ImageRectLists[index][ImageIndexSlice * number + 24] = -1; //表示参数无效
                        }
                        else
                        {
                            //虚拟die的图：只检测当前
                            InspecJudge[ImageIndexWafer + ImageIndexSlice] = true;
                            InspecNums[ImageIndexWafer + ImageIndexSlice] = 0;
                        }
                    }
                    ImageIndexWafer += ImageIndexSlice + 1;
                }
               
                //检查键值对：是否有没有检测的
                bool[] InspecJudgeReview = Enumerable.Repeat(false, cameraParameters.Recipe.ImageCount).ToArray();
               
                foreach (KeyValuePair<int, Dictionary<int, int>> kvp in TriggerInspetionsbak)
                {
                    // 对键值对进行操作
                    if (InspecNums[kvp.Key] == 0)
                    {
                        InspecJudgeReview[kvp.Key] = true;
                    }
                    else
                    {
                        InspecJudgeReview[kvp.Key] = true;
                        Dictionary<int, int> indexs;
                        TriggerInspetionsbak.TryGetValue(kvp.Key, out indexs);
                        int indexRef1;
                        int indexRef2;
                        foreach (var item in indexs)
                        {
                            indexRef1 = item.Key;
                            indexRef2 = item.Value;

                            if (InspecNums[kvp.Key] == 1) InspecJudgeReview[indexRef1] = true;
                            else if (InspecNums[kvp.Key] == 2) InspecJudgeReview[indexRef2] = true;
                            else if (InspecNums[kvp.Key] == 3) { InspecJudgeReview[indexRef1] = true; InspecJudgeReview[indexRef2] = true; }
                            }
                    }
                }
               
                List<int> missedIndexes = Enumerable.Range(0, InspecJudgeReview.Length)
                    .Where(index => !InspecJudgeReview[index])
                    .ToList();

                if (missedIndexes.Count > 0)
                {
                    Console.WriteLine(
                        "Trigger覆盖失败：Recipe.ImageCount={0}, ScanCount={1}, " +
                        "TriggerCount={2}, ActiveTriggerCount={3}, MissedCount={4}, Missed=[{5}]",
                        cameraParameters.Recipe.ImageCount,
                        ImageIndexWafer,
                        TriggerInspetions.Count,
                        TriggerInspetionsbak.Count,
                        missedIndexes.Count,
                        string.Join(",", missedIndexes));
                    return false;
                }

                // 全部图片覆盖成功后，才发布去重后的活动Trigger表。
                TriggerInspetions = TriggerInspetionsbak;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "RenewTriggerInspetions异常：{0}\r\n{1}",
                    ex.Message,
                    ex.StackTrace);
                return false;
            }
        }
    }
}
