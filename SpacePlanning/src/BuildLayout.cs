using System;
using System.Collections.Generic;
using stuffer;
using Autodesk.DesignScript.Runtime;
using System.Diagnostics;
using Autodesk.DesignScript.Geometry;
using System.Linq;

namespace SpacePlanning
{
    /// <summary>
    /// Builds department and programs polygons based on input contextual data.
    /// </summary>
    public static class BuildLayout
    {
        
        internal static double SPACING = 20; //higher value makes code faster, 6, 10 was good too
        internal static double SPACING2 = 20;
        internal static Random RANGENERATE = new Random();
        internal static double RECURSE = 0;
        internal static Point2d REFERENCEPOINT = new Point2d(0,0);
        internal static int DEPTCOUNT = 5;
        internal static double DIVISION = 4;

        internal const string KPU = "kpu";
        internal const string REG = "regular";
        internal const string PUBLIC = "public";

        #region - Public Methods


        //arranges depts on site and updates dept data object
        /// <summary>
        /// Arranges dept on site by assigning polygon2d's to each dept in the Dept Data object.
        /// Returns Dept polygon2d's, Left Over polygon2d's, Circulation polygon2d's and Updated Dept Data object.
        /// </summary>
        /// <param name="deptData">List of DeptData object.</param>
        /// <param name="buildingOutline">Building outline polygon2d geometry.</param>
        /// <param name="kpuDepthList">Depth list of the main department.</param>
        /// <param name="kpuWidthList">Width list of the main department.</param>
        /// <param name="acceptableWidth">Acceptable width in meters while allocating area and polygon2d to each dept on site.</param>
        /// <param name="polyDivision">Point division of each polygon2d. Lower number represents high point count per polygon2d.</param>
        /// <param name="designSeed">Values to restart dept placment algorithm and return another design output.</param>
        /// <param name="noExternalWall">Boolean toggle to turn on or off requirement of external wall for KPU.</param>
        /// <param name="unlimitedKPU">Boolean toggle to turn on or off unlimied KPU placement.</param>
        /// <param name="mode3D">Boolean toggle to turn on or off 3d mode.</param>
        /// <param name="totalBuildingHeight">Total height of the building.</param>
        /// <param name="avgFloorHeight">Floor height of the building.</param>
        /// <param name="numDeptPerFloor">Number of depts per floor.</param>
        /// <param name="highIteration">Boolean to toggle high iteration to compute department placement.</param>
        /// <returns name="DeptData">Updated Dept Data object</returns>
        /// <returns name="LeftOverPolys">Polygon2d's not assigned to any department.</returns>
        /// <search>
        /// DeptData object, department arrangement on site
        /// </search>
        [MultiReturn(new[] { "DeptData", "LeftOverPolys", "OtherDeptPoly", "SubdividedPoly" })]//"CirculationPolys", "OtherDeptMainPoly" 
        public static Dictionary<string, object> PlaceDepartments(List<DeptData> deptData, List<Polygon2d> buildingOutline, Point2d attractorPoint, List<double> kpuDepthList, 
             int designSeed = 50, double circulationWidth = 5, bool noExternalWall = false, bool unlimitedKPU = true, bool mode3D = false, double totalBuildingHeight = 60, double avgFloorHeight = 15, int numDeptPerFloor = 2, bool highIteration = false)
        {
            if (highIteration == true) DEPTCOUNT = 5;
            //double acceptableWidth;
            double polyDivision = 8;
            List<DeptData> deptDataInp = deptData;
            Dictionary<string, object> obj = new Dictionary<string, object>();
            deptData = deptDataInp.Select(x => new DeptData(x)).ToList(); // example of deep copy
            List<double> heightList = new List<double>();
            if (mode3D == true)
            {
                int numFloors = (int)Math.Floor(totalBuildingHeight / avgFloorHeight);
                for (int i = 0; i < numFloors; i++) heightList.Add((i) * avgFloorHeight);
                Trace.WriteLine("Heightlist formed");
                for (int i = 0; i < deptData.Count; i++)
                {
                    deptData[i].Mode3D = true;
                    deptData[i].FloorHeightList = heightList;
                    deptData[i].NumDeptPerFloor = numDeptPerFloor;
                }
            }
            if (deptData[0].Mode3D)
            {
                return BuildLayout3D.PlaceDepartments3D(deptData, buildingOutline, kpuDepthList, attractorPoint, 
                                        designSeed, circulationWidth,noExternalWall, unlimitedKPU, numDeptPerFloor);
            }
            else {
                return BuildLayout3D.PlaceDepartments2D(deptData, buildingOutline, kpuDepthList, attractorPoint,
                                        designSeed, circulationWidth,noExternalWall, unlimitedKPU);
            }  
        }




        //arranges program elements inside primary dept unit and updates program data 
        /// <summary>
        /// Assigns program elements inside the primary department polygon2d.
        /// </summary>
        /// <param name="deptPoly">Polygon2d's of primary department which needs program arrangement inside.</param>
        /// <param name="progData">Program Data object</param>
        /// <param name="primaryProgramWidth">Width of the primary program element in  department.</param>
        /// <param name="recompute">Regardless of the recompute value, it is used to restart computing the node every time it's value is changed.</param>
        /// <returns name="PolyAfterSplit">Polygon2d's obtained after assigning programs inside the department.</returns>
        /// <returns name="ProgramData">Updated program data object.</returns>
        /// <returns name="ProgramsAddedCount">Number of program units added.</returns>
        [MultiReturn(new[] { "ProgramData", "ProgramsAddedCount" })]
        internal static Dictionary<string, object> PlaceKPUPrograms(List<Polygon2d> deptPoly, List<ProgramData> progData, List<double> primaryProgramWidthList, int space = 10)
        {

            if (!ValidateObject.CheckPolyList(deptPoly)) return null;
            if (progData == null || progData.Count == 0) return null;
            int roomCount = 0;
            List<Polygon2d> polyList = new List<Polygon2d>();
            List<Point2d> pointsList = new List<Point2d>();
            Queue<ProgramData> programDataRetrieved = new Queue<ProgramData>();
            List<ProgramData> progDataAddedList = new List<ProgramData>();
            ProgramData copyProgData = new ProgramData(progData[0]);
            int index = 0;
            for (int i = 0; i < progData.Count; i++) programDataRetrieved.Enqueue(progData[i]);
            for (int i = 0; i < deptPoly.Count; i++)
            {
                Polygon2d poly = deptPoly[i];
                if (!ValidateObject.CheckPoly(poly)) continue;
                int dir = 0, count = 0,lineId =0;

                List<double> spans = PolygonUtility.GetSpansXYFromPolygon2d(poly.Points);
                double setSpan = 1000000000000, fac = 1.5;
                if (spans[0] > spans[1]) { setSpan = spans[0]; dir = 1; } // poly is horizontal, dir should be 1
                else { setSpan = spans[1]; dir = 0; }// poly is vertical, dir should be 0
                Polygon2d currentPoly = poly;
                List<Polygon2d> polyAfterSplitting = new List<Polygon2d>();
                ProgramData progItem = new ProgramData(progData[0]);
                Point2d centerPt = PolygonUtility.CentroidOfPoly(currentPoly);

                int lineOrient = ValidateObject.CheckLineOrient(currentPoly.Lines[0]);
                if (lineOrient == dir) lineId = 0;
                else lineId = 1;                
                if (i > 2) index += 1;
                if (index > primaryProgramWidthList.Count - 1) index = 0;
                double primaryProgramWidth = primaryProgramWidthList[index];
                while (setSpan > primaryProgramWidth && count < 200)
                {
                    if (programDataRetrieved.Count == 0) programDataRetrieved.Enqueue(copyProgData);
                    //Trace.WriteLine("Keep going : " + count);
                    double dist = 0;
                    if (setSpan < fac * primaryProgramWidth)
                    {
                        progItem = programDataRetrieved.Dequeue();
                        progItem.ProgAreaProvided = PolygonUtility.AreaPolygon(currentPoly);
                        polyList.Add(currentPoly);
                        progDataAddedList.Add(progItem);
                        count += 1;
                        break;
                    }
                    else dist = primaryProgramWidth;
           
                    Dictionary<string, object> splitReturn = SplitObject.SplitByOffsetFromLine(currentPoly, lineId, dist, 10);
                    if(splitReturn != null)
                    {
                        polyAfterSplitting.Clear();
                        Polygon2d polyA = (Polygon2d)splitReturn["PolyAfterSplit"];
                        Polygon2d polyB = (Polygon2d)splitReturn["LeftOverPoly"];
                        polyAfterSplitting.Add(polyA); polyAfterSplitting.Add(polyB);
                        progItem = programDataRetrieved.Dequeue();
                        progItem.ProgAreaProvided = PolygonUtility.AreaPolygon(polyAfterSplitting[0]);
                        polyList.Add(polyAfterSplitting[0]);
                        currentPoly = polyAfterSplitting[1];
                        setSpan -= dist;
                        progDataAddedList.Add(progItem);
                        count += 1;
                    }          
                }// end of while
                //add the last left over poly for each dept poly
                if (polyAfterSplitting.Count > 0)
                {
                    polyList.Add(polyAfterSplitting[1]);
                    progItem = copyProgData;
                    progItem.ProgAreaProvided = PolygonUtility.AreaPolygon(polyAfterSplitting[1]);
                    progDataAddedList.Add(progItem);
                    count += 1;
                }
            }// end of for loop

            roomCount = progDataAddedList.Count;
            List<ProgramData> UpdatedProgramDataList = new List<ProgramData>();
            for (int i = 0; i < progDataAddedList.Count; i++) //progData.Count
            {
                ProgramData progItem = progDataAddedList[i];
                ProgramData progNew = new ProgramData(progItem);
                if (i < polyList.Count) progNew.PolyAssignedToProg = new List<Polygon2d> { polyList[i] };
                else progNew.PolyAssignedToProg = null;
                UpdatedProgramDataList.Add(progNew);
            }
            List<Polygon2d> cleanPolyList = ValidateObject.CheckAndCleanPolygon2dList(polyList);
            return new Dictionary<string, object>
            {
                { "ProgramData",(UpdatedProgramDataList) },
                { "ProgramsAddedCount" , (roomCount) }
            };
        }






        //arranges program elements inside secondary dept units and updates program data object
        /// <summary>
        /// Assigns program elements inside the secondary department polygon2d.
        /// </summary>
        /// <param name="deptDataInp">Dept Data object.</param>
        /// <param name="recompute">This value is used to restart computing the node every time its value is changed.</param>
        /// <returns></returns>
        [MultiReturn(new[] { "PolyAfterSplit", "ProgramData" })]
        internal static Dictionary<string, object> PlaceREGPrograms(DeptData deptDataInp,double minAllowedDim = 5, int designSeed = 10, bool checkAspectRatio = true)
        {
            if (deptDataInp == null) return null;
            double ratio = 0.5;
            DeptData deptData = new DeptData(deptDataInp);
            List<Polygon2d> deptPoly = deptData.PolyAssignedToDept;
            List<ProgramData> progData = deptData.ProgramsInDept;
            if (!ValidateObject.CheckPolyList(deptPoly)) return null;
            if (progData == null || progData.Count == 0) return null;
            List<List<Polygon2d>> polyList = new List<List<Polygon2d>>();
            List<Polygon2d> polyCoverList = new List<Polygon2d>();


            //SORT THE POLYSUBDIVS
            Point2d center = PolygonUtility.CentroidOfPolyList(deptPoly);
            List<int> sortedPolyIndices = PolygonUtility.SortPolygonsFromAPoint(deptPoly, center);
            List<Polygon2d> sortedPolySubDivs = new List<Polygon2d>();
            for (int k = 0; k < sortedPolyIndices.Count; k++) { sortedPolySubDivs.Add(deptPoly[sortedPolyIndices[k]]); }
            deptPoly = sortedPolySubDivs; 


            //Stack<ProgramData> programDataRetrieved = new Stack<ProgramData>();
            //Stack<Polygon2d> polygonAvailable = new Stack<Polygon2d>();
            Queue<Polygon2d> polygonAvailable = new Queue<Polygon2d>();
            for (int j = 0; j < deptPoly.Count; j++) { polygonAvailable.Enqueue(deptPoly[j]); }
            double areaAssigned = 0, eps = 50, max = 0.73, min = 0.27;
            int count = 0,countIn = 0, maxTry = 15;
            Random ran = new Random(designSeed);
            for(int i = 0; i < progData.Count; i++)
            {
                ProgramData progItem = progData[i];
                progItem.PolyAssignedToProg = new List<Polygon2d>();
                double areaNeeded = progItem.ProgAreaNeeded;
                while (areaAssigned < areaNeeded && polygonAvailable.Count > 0)// && count < maxTry
                {
                    ratio = BasicUtility.RandomBetweenNumbers(ran, max, min);
                    ratio = 0.5;
                    Polygon2d currentPoly = polygonAvailable.Dequeue();
                    double areaPoly = PolygonUtility.AreaPolygon(currentPoly);
                    int compareArea = BasicUtility.CheckWithinRange(areaNeeded, areaPoly, eps);
                    if (compareArea == 1) // current poly area is more =  compareArea == 1
                    {
                        Dictionary<string,object> splitObj = SplitObject.SplitByRatio(currentPoly, ratio);
                        if (splitObj != null)
                        {
                            List<Polygon2d> polyAfterSplit = (List<Polygon2d>)splitObj["PolyAfterSplit"];
                            if (polyAfterSplit == null)
                            {
                                ratio = 0.65;
                                while(polyAfterSplit == null && countIn < maxTry)
                                {
                                    countIn += 1;
                                    ratio -= 0.02;
                                    currentPoly = new Polygon2d(PolygonUtility.SmoothPolygon(currentPoly.Points, 3),0);
                                    splitObj = SplitObject.SplitByRatio(currentPoly, ratio,3);
                                    if (splitObj == null) continue;
                                    polyAfterSplit = (List<Polygon2d>)splitObj["PolyAfterSplit"];
                                }
                            }
                            if (polyAfterSplit == null) continue;
                            for (int j = 0; j < polyAfterSplit.Count; j++) polygonAvailable.Enqueue(polyAfterSplit[j]);
                            count += 1;
                            continue;
                        }
                        else
                        {
                            //area within range
                            if (ValidateObject.CheckPoly(currentPoly))
                            {
                                if (checkAspectRatio)
                                {
                                    if (ValidateObject.CheckPolyAspectRatio(currentPoly, minAllowedDim))
                                    {
                                        progItem.PolyAssignedToProg.Add(currentPoly);
                                        areaAssigned += areaPoly;
                                    }
                                }
                                else
                                {
                                    progItem.PolyAssignedToProg.Add(currentPoly);
                                    areaAssigned += areaPoly;
                                }    
                            }                            
                            count += 1;
                        }
                    }else
                    {
                        //area within range
                        if (ValidateObject.CheckPoly(currentPoly))
                        {
                            if (checkAspectRatio)
                            {
                                if (ValidateObject.CheckPolyAspectRatio(currentPoly, minAllowedDim))
                                {
                                    progItem.PolyAssignedToProg.Add(currentPoly);
                                    areaAssigned += areaPoly;
                                }
                            }
                            else
                            {
                                progItem.PolyAssignedToProg.Add(currentPoly);
                                areaAssigned += areaPoly;
                            }
                        }
                        count += 1;
                    }
                 
                }// end of while
                polyList.Add(progItem.PolyAssignedToProg);
                progItem.ProgAreaProvided = areaAssigned;
                if (progItem.PolyAssignedToProg.Count > 1) { if (progItem.ProgramName.IndexOf("##") == -1) progItem.ProgramName += " ##"; }// + progItem.ProgID;  }
                count = 0;
                areaAssigned = 0;
            }// end of for loop

            

            List<ProgramData> newProgDataList = progData.Select(x => new ProgramData(x)).ToList(); // example of deep copy    
                       
            return new Dictionary<string, object>
            {
                { "PolyAfterSplit", (polyList) },
                { "ProgramData",(newProgDataList) }
            };
        }



        //arranges program elements inside secondary dept units and updates program data object
        /// <summary>
        /// Assigns program elements inside the secondary department polygon2d.
        /// </summary>
        /// <param name="deptData">List of Department Data Objects.</param>
        /// <param name="kpuProgramWidthList">Width of the program poly in the primary department</param>
        /// <param name="minAllowedDim">Minimum allowed dimension of the program space.</param>
        /// <param name="designSeed">Values to restart program placment algorithm and return another design output.</param>
        /// <param name="checkAspectRatio">Boolean value to toggle check aspect ratio of the programs.</param>
        /// <returns name="DeptData">Updated department data object.</returns>
        [MultiReturn(new[] { "DeptData" })]
        public static Dictionary<string, object> PlacePrograms(List<DeptData> deptData, List<double> kpuProgramWidthList, double minAllowedDim = 5, 
            int designSeed = 5, bool checkAspectRatio = false)
        {
            if (deptData == null) return null;
            List<DeptData> deptDataInp = deptData;
            Dictionary<string, object> obj = new Dictionary<string, object>();
            deptData = deptDataInp.Select(x => new DeptData(x)).ToList(); // example of deep copy

            if (deptDataInp[0].Mode3D)
            {
                return BuildLayout3D.PlacePrograms3D(deptData, kpuProgramWidthList, minAllowedDim, designSeed, checkAspectRatio);
            }
            else {
                return BuildLayout3D.PlacePrograms2D(deptData, kpuProgramWidthList, minAllowedDim, designSeed, checkAspectRatio);
            }
        }



        #endregion

        
        #region - Private Methods  

       

        [MultiReturn(new[] { "DeptPoly", "LeftOverPoly", "AllPolys", "AreaAdded", "AllNodes" })]
        internal static Dictionary<string, object> FitRegDept(double deptAreaTarget, List<Polygon2d> polyList)
        {
            if (!ValidateObject.CheckPolyList(polyList)) return null;

            int count = 0, maxTry = 10;
            Queue<Polygon2d> polyAvailable = new Queue<Polygon2d>();
            List<Polygon2d> polysToDept = new List<Polygon2d>(), leftOverPoly = new List<Polygon2d>();
            for (int i = 0; i < polyList.Count; i++) polyAvailable.Enqueue(polyList[i]);
         
            double areaAssigned = 0, ratio = 0.3;
            int dir = 0;
            double areaLeftTobeAssigned = deptAreaTarget - areaAssigned;
            while (areaAssigned < deptAreaTarget && polyAvailable.Count > 0)
            {
                Polygon2d currentPoly = polyAvailable.Dequeue();
                //split the poly if area is more than requirement
                if(PolygonUtility.AreaPolygon(currentPoly) > areaLeftTobeAssigned)
                {
                    Dictionary<string,object> splitObj = SplitObject.SplitByRatio(currentPoly, ratio, dir);
                    List<Polygon2d> polySplit = new List<Polygon2d>();

                    while(splitObj == null && count < maxTry)
                    {
                        count += 1;
                        ratio += 0.02;
                        dir = BasicUtility.ToggleInputInt(dir);
                        splitObj = SplitObject.SplitByRatio(currentPoly, ratio, dir);
                    }
                    if (splitObj != null)
                    {
                        polySplit = (List<Polygon2d>)splitObj["PolyAfterSplit"];
                        if (!ValidateObject.CheckPolyList(polySplit)) continue;
                        polySplit = PolygonUtility.SortPolygonList(polySplit);
                        currentPoly = polySplit[0];
                        polyAvailable.Enqueue(polySplit[1]);
                    }
                    dir = BasicUtility.ToggleInputInt(dir);
                }

                areaAssigned += PolygonUtility.AreaPolygon(currentPoly);
                areaLeftTobeAssigned = deptAreaTarget - areaAssigned;
                polysToDept.Add(currentPoly);
            }


            List<Polygon2d> leftOverList = polyAvailable.ToList();
            Point2d center = PolygonUtility.CentroidOfPolyList(leftOverList);
            List<int> sortedPolyIndices = PolygonUtility.SortPolygonsFromAPoint(leftOverList, center);
            List<Polygon2d> sortedPolySubDivs = new List<Polygon2d>();
            for (int k = 0; k < sortedPolyIndices.Count; k++) { sortedPolySubDivs.Add(leftOverList[sortedPolyIndices[k]]); }
            leftOverList = sortedPolySubDivs; 
            return new Dictionary<string, object>
            {
                { "DeptPoly", (polysToDept) },
                { "LeftOverPoly", (leftOverList) },
                { "AllPolys", (polyList)},
                { "AreaAdded", (areaAssigned) },
                { "AllNodes", (null)}
            };
        }

        
        internal static List<Line2d> RandomizeLineList(List<Line2d> lineList, int designSeed = 0)
        {
            if (lineList == null) return null;
            List<int> indices = new List<int>();
            for (int i = 0; i < lineList.Count; i++) indices.Add(i);
            List<int> indicesRandom = BasicUtility.RandomizeList(indices, new Random(designSeed));
            List<Line2d> lineNewList = new List<Line2d>();
            for (int i = 0; i < lineList.Count; i++) lineNewList.Add(lineList[indicesRandom[i]]);
            return lineNewList;
        }

     

        //places KPU dept with window or external wall need
        [MultiReturn(new[] { "PolyAfterSplit", "LeftOverPoly", "AreaAssignedToBlock" })]
        public static Dictionary<string, object> FitKPUDept(Polygon2d poly, double kpuDepth,
            double area, double thresDistance = 10, int designSeed = 5, double circulationWidth = 3, bool stackOptions = false, 
            Line2d exitLine = null, bool mode = false)
        {

            if (!ValidateObject.CheckPoly(poly)) return null;
            poly = new Polygon2d(poly.Points);
            kpuDepth += circulationWidth;
            Polygon2d currentPoly = new Polygon2d(poly.Points);

            List<Polygon2d> kpuDeptBlocks = new List<Polygon2d>(), leftOverBlocks = new List<Polygon2d>() { currentPoly };
            double areaAssigned = 0;
            if (mode)
            {
                Dictionary<string, object> kpuDeptObj = SplitObject.AssignBlocksBasedOnDistance(leftOverBlocks, kpuDepth, area, thresDistance, designSeed, false, stackOptions);

                if (kpuDeptObj == null) return null;
                kpuDeptBlocks = (List<Polygon2d>)kpuDeptObj["PolyAfterSplit"];
                leftOverBlocks = (List<Polygon2d>)kpuDeptObj["LeftOverPoly"];
                if (!ValidateObject.CheckPolyList(kpuDeptBlocks) || !ValidateObject.CheckPolyList(leftOverBlocks)) return null;
                areaAssigned = (double)kpuDeptObj["AreaAssignedToBlock"];

                return new Dictionary<string, object>
                {
                    { "PolyAfterSplit", (kpuDeptBlocks) },
                    { "LeftOverPoly", (leftOverBlocks) },
                    { "AreaAssignedToBlock", (areaAssigned) },
                };
            }


            Polygon2d polyCorridors = currentPoly;
            List<Polygon2d> polyBlockList = new List<Polygon2d>();
            List<int> lineIdList = new List<int>();
            int lineId = 0, count = 0, countMain = 0, maxTry = 40;
            double areaAdded = 0;
            double areaLeftToBeAdded = area - areaAdded;

            List<int> indices = new List<int>();
            for (int i = 0; i < currentPoly.Points.Count; i++) indices.Add(i);



            Stack<int> lineIdStack = new Stack<int>();
            Queue<int> lineIdQueue = new Queue<int>();
            for (int i = 0; i < currentPoly.Points.Count; i++) lineIdStack.Push(i);
            for (int i = 0; i < currentPoly.Points.Count; i++) lineIdQueue.Enqueue(i);
            if (stackOptions)
            {
                indices = BasicUtility.RandomizeList(indices, new Random(designSeed));
                lineIdQueue.Clear();
                for (int i = 0; i < currentPoly.Points.Count; i++) lineIdQueue.Enqueue(indices[i]);

            }
            while (areaAdded < area && lineIdQueue.Count > 0 && countMain < 1000)
            {
                //lineId = lineIdStack.Pop();
                lineId = lineIdQueue.Dequeue();
                countMain += 1;
                bool error = false;
                double maxLength = areaLeftToBeAdded / kpuDepth;

                double param = 0;
                if (currentPoly.Lines[lineId].Length > maxLength)
                {
                    Trace.WriteLine("Max length found");
                    param = maxLength / currentPoly.Lines[lineId].Length;
                    currentPoly = SplitObject.AddPointToPoly(currentPoly, lineId, param);
                    lineIdStack = new Stack<int>();
                    for (int i = 0; i < currentPoly.Points.Count; i++) lineIdStack.Push(i);
                    for (int i = 0; i < currentPoly.Points.Count; i++) lineIdQueue.Enqueue(i);

                    // lineId = lineIdStack.Pop();
                    lineId = lineIdQueue.Dequeue();
                }


                if (currentPoly.Lines[lineId].Length > 200)
                {
                    Trace.WriteLine("Too Long for a dept");
                    param = 0.5;
                    currentPoly = SplitObject.AddPointToPoly(currentPoly, lineId, param);
                    lineIdStack = new Stack<int>();
                    for (int i = 0; i < currentPoly.Points.Count; i++) lineIdStack.Push(i);
                    for (int i = 0; i < currentPoly.Points.Count; i++) lineIdQueue.Enqueue(i);
                    // lineId = lineIdStack.Pop();
                    lineId = lineIdQueue.Dequeue();
                }


                bool checkOffset = LineUtility.TestLineInPolyOffset(currentPoly, lineId, kpuDepth);
                param = 0.8;
                while (!checkOffset && count < maxTry)
                {
                    count += 1;
                    param -= 0.02;
                    Polygon2d tempPoly = SplitObject.AddPointToPoly(currentPoly, lineId, param);
                    checkOffset = LineUtility.TestLineInPolyOffset(currentPoly, lineId, kpuDepth);
                    if (checkOffset)
                    {
                        lineIdStack = new Stack<int>();
                        currentPoly = tempPoly;
                        for (int i = 0; i < currentPoly.Points.Count; i++) lineIdStack.Push(i);
                        for (int i = 0; i < currentPoly.Points.Count; i++) lineIdQueue.Enqueue(i);
                        lineId = lineIdQueue.Dequeue();
                        //lineId = lineIdStack.Pop();
                    }

                }
                if (!checkOffset || currentPoly.Lines[lineId].Length <= kpuDepth) continue;
                if (!PolygonUtility.FindAdjacentPolyToALine(poly, currentPoly.Lines[lineId])) continue;
                if (stackOptions)
                {
                    if (ValidateObject.CheckIfTwoLinesSame(currentPoly.Lines[lineId], exitLine)) continue;
                }


                Dictionary<string, object> splitObj = SplitObject.SplitByOffsetFromLine(currentPoly, lineId, kpuDepth, thresDistance);
                Polygon2d polySplit = (Polygon2d)splitObj["PolyAfterSplit"];
                Point2d center = PolygonUtility.CentroidOfPoly(polySplit);
                Polygon2d leftOver = (Polygon2d)splitObj["LeftOverPoly"];
                if (ValidateObject.CheckPolygonSelfIntersection(leftOver)) error = true;
                if (!GraphicsUtility.PointInsidePolygonTest(currentPoly, center)) error = true;
                if (!error)
                {
                    polyBlockList.Add(polySplit);
                    areaAdded += currentPoly.Lines[lineId].Length * kpuDepth;
                    lineIdList.Add(lineId);
                    areaLeftToBeAdded = area - areaAdded;
                    currentPoly = leftOver;
                }
            }

         
            
            return new Dictionary<string, object>
            {
                { "PolyAfterSplit", (polyBlockList) },
                { "LeftOverPoly", (currentPoly) },
                { "AreaAssignedToBlock", (areaAdded) },
            };
        }



        //places public dept based on area need and placement of an attractor point by the user 
        [MultiReturn(new[] { "PolyAfterSplit", "LeftOverPoly", "AreaAssignedToBlock", "ExitLine" })]
        public static Dictionary<string, object> FitPublicDept(Polygon2d poly, Point2d attractorPoint,
           double area, int designSeed = 5)
        {

            if (!ValidateObject.CheckPoly(poly)) return null;
            Polygon2d currentPoly = new Polygon2d(poly.Points);
            
            double areaAdded = 0, areaLeftTobeAdded = area- areaAdded;
            int count = 0, maxTry = 5;

            List<Polygon2d> polySplitList = new List<Polygon2d>();
            Polygon2d splitPoly = new Polygon2d(null), leftPoly = new Polygon2d(null);
            List<Polygon2d> leftPolyList = new List<Polygon2d>();
            while(areaAdded < area && count < maxTry)
            {
                double aspRatio = 0.8; // l/w
                double maxWidth = Math.Sqrt(areaLeftTobeAdded * aspRatio);
                double maxLength = areaLeftTobeAdded / maxWidth;
                double fac = 0.75;
                count += 1;
                
                int lineIdCurrent = PointUtility.FindClosestPointIndex(currentPoly.Points, attractorPoint);

                if (currentPoly.Lines[lineIdCurrent].Length > maxLength)
                {
                    double param = maxLength / currentPoly.Lines[lineIdCurrent].Length;
                    currentPoly = SplitObject.AddPointToPoly(currentPoly, lineIdCurrent, param);                    
                }

                maxLength = currentPoly.Lines[lineIdCurrent].Length;
                maxWidth = areaLeftTobeAdded / maxLength;
                double allowedWidth = LineUtility.FindMaxOffsetInPoly(currentPoly, lineIdCurrent);
                if (allowedWidth < maxWidth * fac) maxWidth = allowedWidth * fac;


                if (!LineUtility.TestLineInPolyOffset(currentPoly, lineIdCurrent, maxWidth)) maxWidth = maxWidth * 0.5;    
                Dictionary<string,object> splitObj = SplitObject.SplitByOffsetFromLine(currentPoly, lineIdCurrent, maxWidth, 0);
                splitPoly = (Polygon2d)splitObj["PolyAfterSplit"];
                leftPoly = (Polygon2d)splitObj["LeftOverPoly"];

                areaAdded += PolygonUtility.AreaPolygon(splitPoly);
                areaLeftTobeAdded = area - areaAdded;
                currentPoly = leftPoly;
                leftPolyList.Add(leftPoly);

                if (PolygonUtility.AreaPolygon(splitPoly) > 2) polySplitList.Add(splitPoly);

                
            }
            List<Line2d> lineListCorridor = new List<Line2d>();
            Point2d center = PolygonUtility.CentroidOfPoly(poly);
            for (int i = 0; i < polySplitList.Count; i++)
            {
                List<Line2d> lines = polySplitList[i].Lines;
                for(int j=0; j < lines.Count; j++) if (lines[j].Length > 5) lineListCorridor.Add(lines[j]);
           
            }
            Line2d exitLine = new Line2d(new Point2d(0, 0), new Point2d(0, 100));
            int index = PointUtility.FindClosestLineFromPoint(lineListCorridor, center);
            if (lineListCorridor.Count > 0)  exitLine = lineListCorridor[index];
            else exitLine = null;


            return new Dictionary<string, object>
            {
                { "PolyAfterSplit", (polySplitList) },
                { "LeftOverPoly", (leftPoly) },
                { "AreaAssignedToBlock", (areaAdded) },
                { "ExitLine", (exitLine) }
            };
        }



        //adds circulation polygons to a list of polygons and a container polygon
        [MultiReturn(new[] { "DeptData" })]
        public static Dictionary<string, object> GetDataToDeptData(List<DeptData> deptData,List<List<List<Polygon2d>>> deptPolyList, List<List<double>> areaAssignedDeptList, List<List<List<Polygon2d>>> deptCircPolyList)
        {
            if (deptData == null) return null;
            List<DeptData> deptDataInp = deptData;
            Dictionary<string, object> obj = new Dictionary<string, object>();
            deptData = deptDataInp.Select(x => new DeptData(x)).ToList(); // example of deep copy
            List<List<Polygon2d>> deptPolyListFlat = new List<List<Polygon2d>>(), deptCircPolyListFlat = new List<List<Polygon2d>>();
            List<double> areaAssignedListFlat = new List<double>();

            for (int i = 0; i < deptPolyList.Count; i++) deptPolyListFlat.AddRange(deptPolyList[i]);
            for (int i = 0; i < deptCircPolyList.Count; i++) deptCircPolyListFlat.AddRange(deptCircPolyList[i]);
            for (int i = 0; i < areaAssignedDeptList.Count; i++) areaAssignedListFlat.AddRange(areaAssignedDeptList[i]);

            for (int i = 0; i < deptData.Count; i++)
            {
                int index = i;
                double area = areaAssignedListFlat[index];

                deptData[i].PolyAssignedToDept = deptPolyListFlat[i];
                if (index > areaAssignedListFlat.Count - 1) area = 0;
                else area = areaAssignedListFlat[index];
                deptData[i].DeptAreaProvided = area;
                deptData[i].DeptCirculationPoly = deptCircPolyListFlat[i];
            }

            
            List<DeptData> deptDataNew = deptData;
            deptData = deptDataNew.Select(x => new DeptData(x)).ToList(); // example of deep copy
            

            return new Dictionary<string, object>
            {
                { "DeptData", (deptData) },
            };
        }

        //adds circulation polygons to a list of polygons and a container polygon
        [MultiReturn(new[] { "PolyAfterSplit", "LeftOverPoly" })]
        public static Dictionary<string, object> AddCirculationPoly(List<Polygon2d> polyList,List<Polygon2d> containerPoly,
            double circulationWidth = 3)
        {
            //containerPoly = new Polygon2d(containerPoly.Points);
            List<Polygon2d> polyCleanList = new List<Polygon2d>();
            for(int i = 0; i < polyList.Count; i++) polyCleanList.Add(new Polygon2d(polyList[i].Points));

            polyList = polyCleanList;

            if (!ValidateObject.CheckPolyList(polyList)) return null;
            List<Polygon2d> polysToVerify = new List<Polygon2d>();

            for(int i = 0; i < containerPoly.Count; i++) polysToVerify.Add(new Polygon2d(containerPoly[i].Points));
          
           
            List<Polygon2d> polyListNew = new List<Polygon2d>();
            //List<List<Polygon2d>> polyCorridors = new List<List<Polygon2d>>();
            List<Polygon2d> polyCorridors = new List<Polygon2d>();
            List<int> lineIdList = new List<int>();
            for (int i=0;i<polyList.Count;i++)
            {
                List<Line2d> lineList = new List<Line2d>();
                for (int j = 0; j < polysToVerify.Count; j++) lineList.AddRange(polysToVerify[j].Lines);
                lineIdList = PolygonUtility.FindNotAdjacentPolyToLinesEdges(polyList[i], lineList, 0, 0);

                Dictionary<string,object> splitObj = SplitObject.SplitByOffsetFromLineList(polyList[i], lineIdList, circulationWidth, 0);
                List<Polygon2d> polySplits = (List<Polygon2d>)splitObj["PolyAfterSplit"];
                Polygon2d leftOverPoly = (Polygon2d)splitObj["LeftOverPoly"];
                polysToVerify.AddRange(polySplits);
                polyCorridors.AddRange(polySplits);
                polyListNew.Add(leftOverPoly);
                lineIdList.Clear();
            }

           

            return new Dictionary<string, object>
            {
                { "PolyAfterSplit", (polyCorridors) },
                { "LeftOverPoly", (polyListNew) }
            };
        }




        //makes a space data tree from dept data
        [MultiReturn(new[] { "SpaceTree", "NodeList" })]
        internal static Dictionary<string, object> CreateSpaceTreeFromDeptData(Node root, List<Node> nodeList,
            Point origin, double spaceX, double spaceY, double radius, bool symettry = true)
        {
            SpaceDataTree tree = new SpaceDataTree(root, origin, spaceX, spaceY);
            Node current = root;
            Node nodeAdditionResult = null;
            for (int i = 0; i < nodeList.Count; i++)
            {
                if (current.NodeType == NodeType.Space) current = current.ParentNode;
                nodeAdditionResult = tree.AddNewNodeSide(current, nodeList[i]);
                if (nodeAdditionResult == current) break;
                else if (nodeAdditionResult != current && nodeAdditionResult != null) current = nodeAdditionResult;
                else current = nodeList[i];
            }
            return new Dictionary<string, object>
            {
                { "SpaceTree", (tree) },
                { "NodeList", (nodeList) }
            };
        }



        //dept assignment new way
        [MultiReturn(new[] { "DeptData", "LeftOverPolys", "OtherDeptPoly" ,"SubdividedPoly"})]//"CirculationPolys", "OtherDeptMainPoly" 
        public static Dictionary<string, object> DeptPlacer(List<DeptData> deptData, List<Polygon2d> polyList, Point2d attractorPoint, List<double> kpuDepthList,
            int designSeed = 5, bool noExternalWall = false,
            bool unlimitedKPU = true, bool stackOptionsDept = false, bool stackOptionsProg = false)
        {
            double acceptableWidth = 0;
            double circulationWidth = 3;
            if (deptData == null) { return null; }
            if (!ValidateObject.CheckPolyList(polyList)) return null;
            Trace.WriteLine("DEPT PLACE KPU STARTS +++++++++++++++++++++++++++++");
            List<double> AllDeptAreaAdded = new List<double>();
            List<List<Polygon2d>> AllDeptPolys = new List<List<Polygon2d>>();
            List<List<Polygon2d>> AllDeptCircPolys = new List<List<Polygon2d>>();
            List<Polygon2d> leftOverPoly = new List<Polygon2d>(), polyCirculation = new List<Polygon2d>();//changed from stack
            List<Polygon2d> otherDeptPoly = new List<Polygon2d>();
            List<Polygon2d> subDividedPoly = new List<Polygon2d>();
            int count = 0, maxTry = 20;
            bool prepareReg = false, kpuPlaced = false, noKpuMode = false;// to disable multiple KPU
            double areaAvailable = 0, ratio = 0.6;

            ratio = BasicUtility.RandomBetweenNumbers(new Random(designSeed), 0.76, 0.23);

            double totalAreaInPoly = 0;
            for (int i = 0; i < polyList.Count; i++) totalAreaInPoly += Math.Abs(PolygonUtility.AreaPolygon(polyList[i]));

            double totalDeptProp = 0;
            for (int i = 0; i < deptData.Count; i++)
            {
                double areaAssigned = 0;
                DeptData deptItem = deptData[i];
                //kpuplaced is added to make sure only one kpu added
                if ((deptItem.DepartmentType.IndexOf(KPU.ToLower()) == -1) && kpuPlaced) totalDeptProp += deptItem.DeptAreaProportionNeeded;

                if ((deptItem.DepartmentType.IndexOf(KPU.ToLower()) != -1 ||
                    deptItem.DepartmentType.IndexOf(KPU.ToUpper()) != -1)) kpuPlaced = true;
             }
            kpuPlaced = false;
            List<double> areaNeededDept = new List<double>();
            //for (int i = 0; i < deptData.Count; i++) areaNeededDept.Add(deptData[i].DeptAreaProportionNeeded * totalAreaInPoly); // this maintains dept area proportion and fills the whole poly 
            for (int i = 0; i < deptData.Count; i++) areaNeededDept.Add(deptData[i].DeptAreaNeeded); // this maintains amount of area needed based on prog doc
            
            List<Polygon2d> leftOverBlocks = polyList;
            List<Polygon2d> polySubdivSaved = new List<Polygon2d>();
            Polygon2d currentPoly = polyList[0];
            List<double> areaEachKPUList = new List<double>();
            double areaKpu = 0;
            for (int j = 0; j < kpuDepthList.Count; j++) areaEachKPUList.Add(1000);  //areaEachKPUList.Add(kpuWidthList[j] * kpuDepthList[j]);
            int kpuNum = 0;
            for (int i = 0; i < deptData.Count; i++)
            {
                List<Polygon2d> currentPolyList = new List<Polygon2d>();
                int index = i;
                double thresDistance = 20;
                double areaAssigned = 0;
                DeptData deptItem = deptData[i];

                Line2d exitLine = new Line2d(new Point2d(0, 0), new Point2d(0, 100));

                // if dept is PUBLIC TYPE 
                if ((deptItem.DepartmentType.IndexOf(PUBLIC.ToLower()) != -1 ||
                    deptItem.DepartmentType.IndexOf(PUBLIC.ToUpper()) != -1))
                {
                    currentPolyList = polyList;
                    double areaNeeded = areaNeededDept[i];
                    Dictionary<string, object> publicDeptObj = FitPublicDept(leftOverBlocks[0], attractorPoint, areaNeeded, designSeed);

                    if (publicDeptObj == null) return null;
                    List<Polygon2d> publicDepts = (List<Polygon2d>)publicDeptObj["PolyAfterSplit"];
                    leftOverBlocks[0] = (Polygon2d)publicDeptObj["LeftOverPoly"];
                    if (!ValidateObject.CheckPolyList(publicDepts) || !ValidateObject.CheckPolyList(leftOverBlocks)) return null;
                    areaAssigned = (double)publicDeptObj["AreaAssignedToBlock"];
                    exitLine = (Line2d)publicDeptObj["ExitLine"];
                    List<Polygon2d> cirPublicDeptPoly = new List<Polygon2d>();

                    
                    //place circulation on Public Dept Poly
                    Dictionary<string, object> circPublicDeptObj = AddCirculationPoly(publicDepts, currentPolyList, circulationWidth); // "PolyAfterSplit", "LeftOverPoly"
                   
                    if (circPublicDeptObj != null)
                    {
                        cirPublicDeptPoly = (List<Polygon2d>)circPublicDeptObj["PolyAfterSplit"];
                        publicDepts = (List<Polygon2d>)circPublicDeptObj["LeftOverPoly"];
                    }
                    
                    AllDeptPolys.Add(publicDepts);
                    AllDeptAreaAdded.Add(areaAssigned);
                    AllDeptCircPolys.Add(cirPublicDeptPoly);
                    for (int j = 0; j < leftOverBlocks.Count; j++)
                    {
                        otherDeptPoly.Add(new Polygon2d(leftOverBlocks[j].Points));// just for debugging
                        leftOverPoly.Add(leftOverBlocks[j]);
                    }
                }// end of public dept placement
                
                else if ((deptItem.DepartmentType.IndexOf(KPU.ToLower()) != -1 ||
                    deptItem.DepartmentType.IndexOf(KPU.ToUpper()) != -1))// key planning unit - to disabled multiple kpu same lvl use => // && !kpuPlaced
                {
                    double areaNeeded = areaNeededDept[i];
                    double areaLeftOverBlocks = 0;
                    for (int k = 0; k < leftOverBlocks.Count; k++) areaLeftOverBlocks += PolygonUtility.AreaPolygon(leftOverBlocks[k]);
                    if (unlimitedKPU) areaNeeded = 0.9 * areaLeftOverBlocks;
                    //else areaNeeded = 6000;
                    //if(!stackOptionsDept && areaNeeded> 0.75 * areaLeftOverBlocks) areaNeeded = 0.75 * areaLeftOverBlocks;
                    if (index > kpuDepthList.Count - 1) index = 0;
                    double kpuDepth = kpuDepthList[index];
                    Dictionary<string, object> kpuDeptObj = new Dictionary<string, object>();
                    List<Polygon2d> kpuBlock = new List<Polygon2d>();
                    //currentPoly = leftOverBlocks[0];
                    //currentPolyList = leftOverBlocks;
                    for (int j = 0; j < leftOverBlocks.Count; j++) currentPolyList.Add(new Polygon2d(leftOverBlocks[j].Points));
                    kpuDeptObj = FitKPUDept(currentPolyList[0], kpuDepth, areaNeeded, thresDistance, designSeed, 3, stackOptionsDept, exitLine, stackOptionsDept); // "PolyAfterSplit", "LeftOverPoly", "AreaAssignedToBlock" 
                    try
                    {
                        kpuBlock = (List<Polygon2d>)kpuDeptObj["PolyAfterSplit"];
                        leftOverBlocks[0] = (Polygon2d)kpuDeptObj["LeftOverPoly"];
                        areaAssigned = (double)kpuDeptObj["AreaAssignedToBlock"];
                    }
                    catch
                    {
                        kpuDeptObj = FitKPUDept(leftOverBlocks[0], kpuDepth, areaNeeded, thresDistance, designSeed, 3, stackOptionsDept, exitLine); // "PolyAfterSplit", "LeftOverPoly", "AreaAssignedToBlock" 
                        kpuBlock = (List<Polygon2d>)kpuDeptObj["PolyAfterSplit"];
                        leftOverBlocks[0] = (Polygon2d)kpuDeptObj["LeftOverPoly"];
                        areaAssigned = (double)kpuDeptObj["AreaAssignedToBlock"];
                    }
                    List<Polygon2d> cirKPUPoly = new List<Polygon2d>();
                    
                    //place circulation on key planning unit Dept 
                    Dictionary<string, object> circKPUtObj = AddCirculationPoly(kpuBlock, currentPolyList, circulationWidth); // "PolyAfterSplit", "LeftOverPoly"                                      
                    if (circKPUtObj != null)
                    {
                        cirKPUPoly = (List<Polygon2d>)circKPUtObj["PolyAfterSplit"];
                        kpuBlock = (List<Polygon2d>)circKPUtObj["LeftOverPoly"];
                    }             
                    AllDeptPolys.Add(kpuBlock);
                    AllDeptAreaAdded.Add(areaAssigned);
                    AllDeptCircPolys.Add(cirKPUPoly);

                    for (int j = 0; j < leftOverBlocks.Count; j++)
                    {
                        otherDeptPoly.Add(new Polygon2d(leftOverBlocks[j].Points));// just for debugging
                        leftOverPoly.Add(leftOverBlocks[j]);
                    }
                    kpuNum += 1;
                    //kpuPlaced = true;
                }// end of kpu placement
                else // regular depts
                {
                    //when there is no kpu in the requirement
                    if (!kpuPlaced) { leftOverPoly = leftOverBlocks; kpuPlaced = true; noKpuMode = true; }
                    if (!prepareReg) // only need to do once, places a grid of rectangles before other depts get alloted
                    {
                        currentPolyList.Clear();
                        for (int j = 0; j < leftOverPoly.Count; j++) currentPolyList.Add(new Polygon2d(leftOverPoly[j].Points));
                        List<List<Polygon2d>> polySubDivs = new List<List<Polygon2d>>();
                        Point2d center = PolygonUtility.CentroidOfPolyList(leftOverPoly);
                        List<Point2d> ptLists = new List<Point2d>();
                        for (int j = 0; j < leftOverPoly.Count; j++) ptLists.AddRange(leftOverPoly[j].Points);

                        Point2d lowestPt = ptLists[PointUtility.LowestPointFromList(ptLists)];
                        Point2d ptToSort = lowestPt;
                        //Point2d ptToSort = center;
                        double arealeft = 0;
                        for (int j = 0; j < leftOverPoly.Count; j++) { arealeft += PolygonUtility.AreaPolygon(leftOverPoly[j]); }
                        if (stackOptionsProg)
                        {
                            double upper = arealeft / 6, lower = arealeft / 12;
                            //acceptableWidth = BasicUtility.RandomBetweenNumbers(new Random(designSeed), upper, lower);  
                        }
                        acceptableWidth = Math.Sqrt(arealeft) / DIVISION;
                        acceptableWidth = 35;
                        polySubDivs = SplitObject.SplitRecursivelyToSubdividePoly(leftOverPoly, acceptableWidth, ratio);

                        bool checkPoly1 = ValidateObject.CheckPolygon2dListOrtho(polySubDivs[0], 0.5);
                        bool checkPoly2 = ValidateObject.CheckPolygon2dListOrtho(polySubDivs[1], 0.5);
                        while (polySubDivs == null || polySubDivs.Count == 0 || !checkPoly1 || !checkPoly2 && count < maxTry)
                        {
                            ratio -= 0.01;
                            if (ratio < 0) ratio = 0.6; break;
                            polySubDivs = SplitObject.SplitRecursivelyToSubdividePoly(leftOverPoly, acceptableWidth, ratio);
                            count += 1;
                        }
                        for (int j = 0; j < polySubDivs[0].Count; j++) { polySubdivSaved.Add(new Polygon2d(polySubDivs[0][j].Points)); }
                        List<int> sortedPolyIndices = PolygonUtility.SortPolygonsFromAPoint(polySubDivs[0], ptToSort);
                        List<Polygon2d> sortedPolySubDivs = new List<Polygon2d>();
                        for (int k = 0; k < sortedPolyIndices.Count; k++) { sortedPolySubDivs.Add(polySubDivs[0][sortedPolyIndices[k]]); }
                        leftOverPoly = sortedPolySubDivs; // polySubDivs[0]            
                        if (leftOverPoly == null) break;
                        prepareReg = true;
                                                
                        //add the circulation
                        //place circulation on reg Dept Poly
                        Dictionary<string, object> circREGObj = AddCirculationPoly(leftOverPoly, currentPolyList, circulationWidth);                    
                        
                        List<Polygon2d> cirREGPoly = new List<Polygon2d>();                        
                        if (circREGObj != null)
                        {
                            cirREGPoly = (List<Polygon2d>)circREGObj["PolyAfterSplit"];
                            leftOverPoly = (List<Polygon2d>)circREGObj["LeftOverPoly"];
                        }                        
                        AllDeptCircPolys.Add(cirREGPoly);    
                    }// end of prepare reg dept
                    double areaNeeded = deptItem.DeptAreaNeeded;
                    Dictionary<string, object> assignedByRatioObj = FitRegDept(areaNeeded, leftOverPoly);
                    currentPolyList = leftOverPoly;
                    if (assignedByRatioObj == null) continue;
                    Trace.WriteLine("Assignment worked " + i);
                    List<Polygon2d> everyDeptPoly = (List<Polygon2d>)assignedByRatioObj["DeptPoly"];
                    leftOverPoly = (List<Polygon2d>)assignedByRatioObj["LeftOverPoly"];
                    areaAssigned = (double)assignedByRatioObj["AreaAdded"];    
                    List<Node> AllNodesList = (List<Node>)assignedByRatioObj["AllNodes"];
                    AllDeptAreaAdded.Add(areaAssigned);
                    AllDeptPolys.Add(everyDeptPoly);
                    //
                }// end of regular dept placement
            }// end of for loop
            //clean dept polys based on their fitness
            for (int i = 0; i < AllDeptPolys.Count; i++) AllDeptPolys[i] = ValidateObject.CheckAndCleanPolygon2dList(AllDeptPolys[i]);

            //update dept data based on polys assigned
            List<DeptData> UpdatedDeptData = new List<DeptData>();
            for (int i = 0; i < deptData.Count; i++)
            {
                DeptData newDeptData = new DeptData(deptData[i]);
                if (i < AllDeptAreaAdded.Count)
                {
                    Trace.WriteLine("Dept playing : " + i);
                    newDeptData.DeptAreaProvided = AllDeptAreaAdded[i];
                    newDeptData.PolyAssignedToDept = AllDeptPolys[i];
                    if (i < AllDeptCircPolys.Count) newDeptData.DeptCirculationPoly = AllDeptCircPolys[i];
                    UpdatedDeptData.Add(newDeptData);
                }
                else
                {
                    newDeptData.DeptAreaProvided = 0;
                    newDeptData.PolyAssignedToDept = new List<Polygon2d>();
                    UpdatedDeptData.Add(newDeptData);
                }
            }

            //added to compute area percentage for each dept
            double totalDeptArea = 0;
            for (int i = 0; i < UpdatedDeptData.Count; i++) totalDeptArea += UpdatedDeptData[i].DeptAreaProvided;
            for (int i = 0; i < UpdatedDeptData.Count; i++)
            {
                UpdatedDeptData[i].DeptAreaProportionAchieved = Math.Round((UpdatedDeptData[i].DeptAreaProvided / totalDeptArea), 3);
                if (stackOptionsProg)
                {
                    if (UpdatedDeptData[i].ProgramsInDept != null || UpdatedDeptData[i].ProgramsInDept.Count > 0)
                        UpdatedDeptData[i].ProgramsInDept = ReadData.RandomizeProgramList(UpdatedDeptData[i].ProgramsInDept, designSeed);             
                }

            }
            if (leftOverPoly.Count == 0) leftOverPoly = null;
            Trace.WriteLine("DEPT PLACE KPU ENDS +++++++++++++++++++++++++++++++");
            return new Dictionary<string, object>
            {
                { "DeptData", (UpdatedDeptData) },
                { "LeftOverPolys", (leftOverPoly) },
                { "OtherDeptPoly", (otherDeptPoly)},
                { "SubdividedPoly", (polySubdivSaved) }
            };
        }

        #endregion

    }
}
