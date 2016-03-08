﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.OleDb;
//using Excel = Microsoft.Office.Interop.Excel;
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
    public class ReadInfo
    {
        // Two private variables for example purposes
        private double _a;
        private double _b;



        // We make the constructor for this object internal because the 
        // Dynamo user should construct an object through a static method
        internal ReadInfo(double a, double b)
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
        public static ReadInfo ByTwoDoubles(double a, double b)
        {
            return new ReadInfo(a, b);
        }

        /// <summary>
        /// Example property returning the value _a inside the object
        /// </summary>
        public double A
        {
            get { return _a; }
        }


        //TO READ SAT FILE CONTAINING NURBS CURVE
        public static Autodesk.DesignScript.Geometry.Geometry[] readSAT(string path)
        {
            Autodesk.DesignScript.Geometry.Geometry[] nurbCurve =  Autodesk.DesignScript.Geometry.Geometry.ImportFromSAT(path);
            
            return nurbCurve;
           
        }

        //GET INPUT POINT LIST ( FROM SAT FILE ) AND MAKE A GRID OF SQUARES
        public static void makeSquaresFromPointList(Autodesk.DesignScript.Geometry.Point[] ptList)
        {
            double gridSize = 100;
            double flrHeight = 5;
            GridBasis gr = new GridBasis(gridSize, flrHeight);

            
                

        }

        //READ THE POINTS FROM THE INPUT NURBS CURVE
        public static void pointListOutline(string path)
        {
            Autodesk.DesignScript.Geometry.Geometry[] nurbCurve = Autodesk.DesignScript.Geometry.Geometry.ImportFromSAT(path);
        }

        //TO READ .CSV FILE AND ACCESS THE DATA
        public static List<List<string>> readCSV(string path)
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



            }

            dataStack.Add(progIdList);
            dataStack.Add(programList);
            dataStack.Add(deptNameList);
            dataStack.Add(progQuantList);
            dataStack.Add(areaEachProgList);
            dataStack.Add(prefValProgList);
            dataStack.Add(progAdjList);

            return dataStack;


        }

        public static List<Point> makePointGrid(List<List<string>> dataStack)
        {

            int lenPointLists = dataStack[0].Count;
            int count = 0;
            double x= 0, y = 0;
            double addX = 10, addY = 10;

            List<Point> ptList = new List<Point>();

            for(int i = 0; i < lenPointLists; i++)
            {
                //ptList.Add(Point.ByCoordinates(0, 0));
            }


            for(int i =0; i<lenPointLists; i++)
            {
                count += 1;
                if (count > 5)
                {
                    count = 0;
                    x = 0;
                    y += addY;
                }

                ptList.Add(Point.ByCoordinates(x, y));
                x += addX;


            }

            return ptList;

        }


        public static List<Circle> makeBubblesShape(Point[] ptList, List<List<string>> dataStack, double scale, Boolean tag)
        {
            List<Circle> cirList = new List<Circle>();
            List<string> progAreaList = dataStack[3];
            List<string> progQuantList = dataStack[4];

            List<double> progTotalList = new List<double>();
            for (int i = 0; i < progAreaList.Count; i++)
            {
                double totArea = 0;
                if (!tag)
                {
                    totArea = Convert.ToDouble(progAreaList[i]) * Convert.ToDouble(progQuantList[i]);
                }
                else
                {
                    totArea = Convert.ToDouble(progAreaList[i]);
                }

                progTotalList.Add(totArea);
            }

            double maxArea = progTotalList.Max();
            List<double> progNormalizedTotArea = new List<double>();
            for (int i = 0; i < progTotalList.Count; i++)
            {
                progNormalizedTotArea.Add(progTotalList[i] * scale / maxArea);
            }

            for (int i = 0; i < progNormalizedTotArea.Count; i++)
            {
                Circle cir = Circle.ByCenterPointRadius(ptList[i], progNormalizedTotArea[i]);
                cirList.Add(cir);
                //cir.Dispose();
            }

            
            return cirList;

        }







        public static List<double> makeBubbles(Point[] ptList, List<List<string>> dataStack, double scale, Boolean tag)
        {
            List<Circle> cirList = new List<Circle>();
            List<string> progAreaList = dataStack[3];
            List<string> progQuantList = dataStack[4];

            List<double> progTotalList = new List<double>();
            for(int i =0; i < progAreaList.Count; i++)
            {
                double totArea = 0;
                if (!tag)                   
                {
                   totArea = Convert.ToDouble(progAreaList[i]) * Convert.ToDouble(progQuantList[i]);
                }
                else
                {
                    totArea = Convert.ToDouble(progAreaList[i]);
                }
                
                progTotalList.Add(totArea);
            }

            double maxArea = progTotalList.Max();
            List<double> progNormalizedTotArea = new List<double>();
            for (int i = 0; i < progTotalList.Count; i++)
            {
                progNormalizedTotArea.Add(progTotalList[i]*scale/maxArea);
            }

            
            return progNormalizedTotArea;

        }


        private static Dictionary<string, object> pushBubbles(List<Point> ptList, List<List<string>> dataStack, List<double> radiusListOrig,int iteration, double spacing)
        {

            List<Circle> cirList = new List<Circle>();
            List<double> radiusList = new List<double>();

            Random rn = new Random();
            for(int i = 0; i < ptList.Count; i++)
            {
                double ran = Convert.ToDouble(rn.Next(2, 10));
                radiusList.Add(ran);
            }

            for (int i=0; i < iteration; i++)
            {
                for(int j =0; j< ptList.Count; j++)
                {
                    for (int k = j+1; k < ptList.Count; k++)
                    {
                        if (j == k)
                        {
                            //continue;
                        }

                        double rad1 = radiusList[j];
                        double rad2 = radiusList[k];
                        double totRad = rad1 + rad2;
                        if (ptList[j].Equals(ptList[k]))
                        {
                            //continue;
                        }
                        double dist = ptList[j].DistanceTo(ptList[k]);

                        //double dist = Math.Sqrt(Math.Pow((ptList[j].X - ptList[k].X), 2) + Math.Pow((ptList[j].Y - ptList[k].Y), 2));
                        double diff = 0;
                        if ( totRad == dist)
                        {
                            continue;
                        }

                        //if totRadius is greater than distance
                        if(totRad > dist)
                        {
                            //diff = totRad - (dist);
                        }
                        //if totRadius is lesser than distance
                        else
                        {
                            //diff = dist - totRad;
                        }
                        diff = dist - totRad + spacing;

                        Vector vec1 = Vector.ByTwoPoints(ptList[k], ptList[j]);
                        vec1 = vec1.Normalized();
                        vec1 = vec1.Scale(diff);
                        Point p1 = ptList[k].Add(vec1);
                        //ptList[k] = p1;
                        ptList.RemoveAt(k);
                        ptList.Insert(k, p1);



                    }// end of outer for loop k
                }// end of outer for loop j
            }// end of outer for loop i
            for (int i = 0; i < radiusList.Count; i++)
            {
                Circle cir = Circle.ByCenterPointRadius(ptList[i], radiusList[i]);
                cirList.Add(cir);
            }
            return new Dictionary<string, object>
            {
                { "BubbleList", cirList },
                { "PointList", ptList },
                { "RadiusList", radiusList }
            };
        }

        //IMPLEMENTING ANOTHER CIRCLE PACKING STRATEGY
        public static Dictionary<string, object> bubblePacker(List<Point> ptList, List<double> radiusList, int iteration = 100)
        {
           // List<Point> ptList = new List<Point>(ptListIn);
            List<Circle> cirList = new List<Circle>();
            List<double> velXList = new List<double>();
            List<double> velYList = new List<double>();
            //List<double> radiusList = new List<double>();

            double maxSpeed = 0.05;
            double pushFactor = 0.05;
            double pullFactor = 0.01;

            
            //make the radius list
            Random rn = new Random();
            for (int i = 0; i < ptList.Count; i++)
            {
                double ran = Convert.ToDouble(rn.Next(2, 10));
                //radiusList.Add(ran);
            }


            //main iteration loop
            for (int i = 0; i < iteration; i++)
            {

                velXList.Clear();
                velYList.Clear();

                //make the velocity list
                for (int v = 0; v < ptList.Count; v++)
                {
                    velXList.Add(ptList[v].X * -1 * pullFactor);
                    velYList.Add(ptList[v].Y * -1 * pullFactor);
                }

                for (int j = 0; j < ptList.Count; j++)
                {
                    for (int k = j + 1; k < ptList.Count; k++)
                    {

                        double rad1 = radiusList[j];
                        double rad2 = radiusList[k];
                        double diam = rad1 + rad2;
                        if (ptList[j].Equals(ptList[k]))
                        {
                            //continue;
                        }
                        Vector vect = Vector.ByTwoPoints(ptList[j], ptList[k]);
                        //double dist = vect.Length;
                        double dist = ptList[j].DistanceTo(ptList[k]);
                        //double dist = Math.Sqrt(Math.Pow((ptList[j].X - ptList[k].X), 2) + Math.Pow((ptList[j].Y - ptList[k].Y), 2));
                        if (dist < diam)
                        {
                            velXList[j] += vect.X * -1 * pushFactor;
                            velYList[j] += vect.Y * -1 * pushFactor;
                            velXList[k] += vect.X * 1 * pushFactor;
                            velYList[k] += vect.Y * 1 * pushFactor;
                            
                        }
                    }// end of 'k' for loop
                }// end of 'j' for loop


                for (int p = 0; p < ptList.Count; p++)
                {
                    if (velXList[p] > maxSpeed)
                    {
                        velXList[p] = maxSpeed;
                    }

                    if (velYList[p] > maxSpeed)
                    {
                        velYList[p] = maxSpeed;
                    }


                    Vector vc = Vector.ByTwoPoints(Point.ByCoordinates(0, 0), Point.ByCoordinates(velXList[p], velYList[p]));
                    Point pn = ptList[p].Add(vc);
                    ptList.RemoveAt(p);
                    ptList.Insert(p, pn);
                }
               


            }// end of 'i' for loop

            for (int n = 0; n < ptList.Count; n++)
            {
                Circle cir = Circle.ByCenterPointRadius(ptList[n], radiusList[n]);
                cirList.Add(cir);
            }


            return new Dictionary<string, object>
            {
                { "BubbleList", cirList },
                { "PointList", ptList }
            };

        }

        [MultiReturn(new[] { "add", "mult" })]
        private  Dictionary<string, object> ReturnMultiExample(double a, double b)
        {
            return new Dictionary<string, object>
            {
                { "add", (a + b) },
                { "mult", (a * b) }
            };
        }

        //MAKING ADJACENCY LIST AND LINE CONNECTION BETWEEN POINTS
        public static Dictionary<string, object> makeAdjacencyGraph(Point[] ptList, List<List<string>> dataStack)
        {
            List<int> idList = new List<int>();
            List<string> adjList = dataStack[6];


            List<List<int>> adjListPlace = new List<List<int>>();
            Random rnd1 = new Random();
            Random rnd2 = new Random();
            for (int i =0; i < adjList.Count; i++)
            {
                
                int randomNum = rnd1.Next(0, 7);                
                List<int> adjAdd = new List<int>();

                for (int j = 0; j < randomNum; j++)
                {
                    int num = rnd2.Next(0, 15);
                    adjAdd.Add(num);
                }
                adjListPlace.Add(adjAdd);
            }

            List<List<Line>> allLinList = new List<List<Line>>();
            for (int i =0; i < adjList.Count; i++)
            {
                List<int> adjAdd = new List<int>();
                adjAdd = adjListPlace[i];
                List<Line> linList = new List<Line>();
                for (int j = 0; j < adjAdd.Count; j++)
                {
                    if(ptList[i].X == ptList[j].X && ptList[i].Y == ptList[j].Y)
                    {
                        continue;
                    }
                    Line ln = Line.ByStartPointEndPoint(ptList[i], ptList[j]);
                    linList.Add(ln);
                }
                allLinList.Add(linList);              

            }


            return new Dictionary<string, object>
            {
                { "AdjacencyList", adjListPlace },
                { "ConnectionList", allLinList }
            };

        }

       

   

    


    }
}
