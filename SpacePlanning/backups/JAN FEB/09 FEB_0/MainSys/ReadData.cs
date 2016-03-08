﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.OleDb;
using System.Threading.Tasks;
using Autodesk.DesignScript.Runtime;
using Autodesk.DesignScript.Interfaces;
using Autodesk.DesignScript.Geometry;
using System.IO;
using stuffer;

///////////////////////////////////////////////////////////////////
/// NOTE: This project requires references to the ProtoInterface
/// and ProtoGeometry DLLs. These are found in the Dynamo install
/// directory.
///////////////////////////////////////////////////////////////////

namespace SpacePlanning
{
    public class ReadData
    {
        // Two private variables for example purposes
        private double _a;
        private double _b;



        // We make the constructor for this object internal because the 
        // Dynamo user should construct an object through a static method
        internal ReadData(double a, double b)
        {
            _a = a;
            _b = b;
        }

      

        /// <summary>
        /// An example of how to construct an object via a static method.
        /// This is needed as Dynamo lacks a "new" keyword to construct a 
        /// new object
        /// </summary>
        /// <param name="a">1st number. This will be stored in the Class.</param>
        /// <param name="b">2nd number. This will be stored in the Class</param>
        /// <returns>A newly-constructed ZeroTouchEssentials object</returns>
        public static ReadData ByTestObjectMake(double a, double b)
        {
            return new ReadData(a, b);
        }

        /// <summary>
        /// Example property returning the value _a inside the object
        /// </summary>
        private double _A
        {
            get { return _a; }
        }




        //0     ////////////////////////
        //TO READ .CSV FILE AND ACCESS THE DATA
        [MultiReturn(new[] { "DataStackArray", "ProgramDataObject", "DeptNames" })]
        public static Dictionary<string, object> MakeDataStack(string path,double dimX,double dimY)
        {

            var reader = new StreamReader(File.OpenRead(@path));

            List<string> progIdList = new List<string>();
            List<string> programList = new List<string>();
            List<string> deptNameList = new List<string>();
            List<string> progQuantList = new List<string>();
            List<string> areaEachProgList = new List<string>();
            List<string> prefValProgList = new List<string>();
            List<string> progAdjList = new List<string>();


            List<List<string>> dataStack = new List<List<string>>();
            List<ProgramData> programDataStack = new List<ProgramData>();

            int readCount = 0;
            while (!reader.EndOfStream)
            {


                var line = reader.ReadLine();
                var values = line.Split(',');

                if (readCount == 0)
                {
                    readCount += 1;
                    continue;
                }
                progIdList.Add(values[0]);
                programList.Add(values[1]);
                deptNameList.Add(values[2]);
                progQuantList.Add(values[3]);
                areaEachProgList.Add(values[4]);
                prefValProgList.Add(values[5]);
                progAdjList.Add(values[6]);

                Cell dummyC = new Cell(Point2d.ByCoordinates(0, 0), 0, 0,true);
                List<Cell> dummyCell = new List<Cell>();
                dummyCell.Add(dummyC);

                List<string> adjList = new List<string>();
                adjList.Add(values[6]);

                ProgramData progData = new ProgramData(Convert.ToInt16(values[0]), values[1], values[2], Convert.ToInt16(values[3]),
                    Convert.ToDouble(values[4]), Convert.ToInt16(values[5]), adjList, dummyCell,dimX,dimY);

                programDataStack.Add(progData);

                

            }

            List<string> deptNames = GetDeptNames(deptNameList);
            dataStack.Add(progIdList);
            dataStack.Add(programList);
            dataStack.Add(deptNameList);
            dataStack.Add(progQuantList);
            dataStack.Add(areaEachProgList);
            dataStack.Add(prefValProgList);
            dataStack.Add(progAdjList);
            

            return new Dictionary<string, object>
            {
                { "DatStackArray", (dataStack) },
                { "ProgramDataObject", (programDataStack) },
                { "DeptNames", (deptNames) }
            };


        }

        //INTERNAL FUNC TO GET THE NAMES OF DEPARTMENTS/ZONES
        internal static List<string> GetDeptNames(List<string> deptNameList)
        {
            List<string> uniqueItemsList = deptNameList.Distinct().ToList();
            return uniqueItemsList;
        }

        // 0B ////////////////////////////
        //TO TEST IF PROGRAM DATA OUTPUTS CORRECT STUFFS
        [MultiReturn(new[] { "ProgrID", "ProgrName", "ProgDept" })]
        public static Dictionary<string, object> ProgramObjectCheck(List<ProgramData> progData)
        {
            List<int> progIdList = new List<int>();
            List<string> programList = new List<string>();
            List<string> deptNameList = new List<string>();
            List<string> progQuantList = new List<string>();

            for (int i =0; i<progData.Count; i++)
            {
                progIdList.Add(progData[i]._progrID);
                programList.Add(progData[i]._progName);
                deptNameList.Add(progData[i]._progDept);
            }
            return new Dictionary<string, object>
            {
                { "ProgrID", (progIdList) },
                { "ProgrName", (programList) },
                { "ProgDept", (deptNameList) }
            };
        }

        //1     ////////////////////////
        //TO READ SAT FILE CONTAINING NURBS CURVE
        public static Geometry[] ReadBuildingOutline(string path)
        {
           
            return Geometry.ImportFromSAT(path);
           
        }

        //2      ///////////////////////
        //TO GET THE POINT INFORMATION SORTED FROM BUILDING OUTLINE
        public static List<Point2d> FromOutlineGetPoints(List<NurbsCurve> nurbList)
        {
            List<Point2d> pointList = new List<Point2d>();

            for (int i = 0; i < nurbList.Count; i++)
            {
                Point2d pointPerCurve = Point2d.ByCoordinates(nurbList[i].StartPoint.X, nurbList[i].StartPoint.Y);
                pointList.Add(pointPerCurve);
                if (i == nurbList.Count)
                {
                    pointList.Add(Point2d.ByCoordinates(nurbList[i].EndPoint.X, nurbList[i].EndPoint.Y));
                }

            }

            return pointList;

        }


        // 3    //////////////////////////
        //TO GET BOUNDING RECTANGLE FROM POINT LIST
        public static List<Point2d> FromPointsGetBoundingPoly(List<Point2d> pointList)
        {
            List<Point2d> pointCoordList = new List<Point2d>();
            List<double> xCordList = new List<double>();
            List<double> yCordList = new List<double>();
            double xMax = 0, xMin = 0, yMax = 0, yMin = 0;
            for (int i = 0; i < pointList.Count; i++)
            {
                xCordList.Add(pointList[i].X);
                yCordList.Add(pointList[i].Y);
            }

            xMax = xCordList.Max();
            yMax = yCordList.Max();

            xMin = xCordList.Min();
            yMin = yCordList.Min();

            pointCoordList.Add(Point2d.ByCoordinates(xMin,yMin));
            pointCoordList.Add(Point2d.ByCoordinates(xMin, yMax));
            pointCoordList.Add(Point2d.ByCoordinates(xMax, yMax));
            pointCoordList.Add(Point2d.ByCoordinates(xMax, yMin));

            return pointCoordList;
        }




        //CONVERT POINT INFORMATION TO POINT2D INFORMATION
        internal static List<Point2d> PointtoPoint2D(List<Point> pointList)
        {
            List<Point2d> point2dList = new List<Point2d>();

           for(int i = 0; i < pointList.Count; i++)
            {
                Point2d p2 = Point2d.ByCoordinates(pointList[i].X, pointList[i].Y);
                point2dList.Add(p2);
            }
            pointList.Clear();        
            return point2dList;
        }


        //MAKE A RANGE 2D OBJECT FRO X AND Y DIRECTION FROM POINTLIST
        internal static Range2d FromPoint2dGetRange2D(List<Point2d> point2dList)
        {
            List<double> xCordList = new List<double>();
            List<double> yCordList = new List<double>();
            double xMax = 0, xMin = 0, yMax = 0, yMin = 0;
            for (int i = 0; i < point2dList.Count; i++)
            {
                xCordList.Add(point2dList[i].X);
                yCordList.Add(point2dList[i].Y);
            }

            xMax = xCordList.Max();
            yMax = yCordList.Max();

            xMin = xCordList.Min();
            yMin = yCordList.Min();

            Range1d x = new Range1d(xMin, xMax);
            Range1d y = new Range1d(yMin, yMax);
            Range2d xyRange = new Range2d(x, y);

            return xyRange;
        }

       
       
       

    }
}
