using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using Random = System.Random;

internal class DiceCube
{
    public static readonly Random Rand = new Random();
    public static readonly Vector3[] Axis = new Vector3[] { Vector3.right, Vector3.up, Vector3.forward };

    private Func<IEnumerator<WaitForSeconds>> OnFinish;
    private RubikCube cube = null;

    private int[][][] dots;

    private List<Path> paths = null;
    private readonly int Dimensions = 3;

    public DiceCube()
    {
        ResetDots();
    }

    public DiceCube SetCube(RubikCube cube)
    {
        ResetDots();
        this.cube = cube;
        return this;
    }

    public void SetOnFinishListener(Func<IEnumerator<WaitForSeconds>> OnFinish)
    {
        this.OnFinish = OnFinish;
    }

    public DiceCube GenerateRandomPath()
    {
        paths = new();

        for (int i = 0; i < 10; i++)
        {
            paths.Add(new Path());
        }

        return this;
    }

    public DiceCube Rotate(float shuffleSpeed, bool showDebug = false)
    {
        if (paths == null)
        {
            throw new Exception("There are no paths!");
        }

        //paths = new()
        //{
        //    new Path(Vector3.forward, 0, true),
        //    new Path(Vector3.forward, 1, false),
        //    new Path(Vector3.right, 0, false),
        //    new Path(Vector3.forward, 0, false),
        //    new Path(Vector3.right, 0, true)
        //};

        //paths = new()
        //{
        //    new Path(Vector3.right, 1, true),
        //    new Path(Vector3.up, 2, true),
        //    new Path(Vector3.up, 0, false),
        //    new Path(Vector3.right, 2, false),
        //    new Path(Vector3.right, 2, false)
        //};

        //paths = new()
        //{
        //    new Path(Vector3.right, 2, true),
        //    new Path(Vector3.up, 2, true),
        //    new Path(Vector3.up, 0, false),
        //    new Path(Vector3.right, 2, true),
        //    new Path(Vector3.up, 0, false)
        //};

        if (cube != null)
        {
            cube.Shuffle(paths, shuffleSpeed);
        }
        
        foreach (Path path in paths)
        {
            RotateDisk(path.Axis, path.Layer, path.PositiveRotation);
            
            //Debug.Log("Forgatva: " + String.Join(", ", GetDotCountsOnDisks()));
        }

        //if (showDebug)
        //{
        //    for (int i = 0; i < 6; i++)
        //    {
        //        for (int j = 0; j < Dimensions; j++)
        //        {
        //            for (int k = 0; k < Dimensions; k++)
        //            {

        //            }
        //        }
        //    }
        //}
        //foreach (Path path in paths)
        //{
        //    Debug.Log("Axis: " + path.Axis + ", Layer: " + path.Layer + ", Positive: " + path.PositiveRotation);
        //}
        if (OnFinish != null)
        {
            OnFinish();
        }

        return this;
    }

    public List<int> GetDotCountsOnDisks()
    {
        List<int> dotCounts = new(Enumerable.Repeat(0, 6));

        for (int i = 0; i < 6; i++)
        { 
            for (int j = 0; j < Dimensions; j++)
            {
                for (int k = 0; k < Dimensions; k++)
                {
                    dotCounts[i] += dots[i][j][k];
                }
            }
        }

        return dotCounts;
    }

    public void RotateDisk(Vector3 pAxis, int pLayer, bool pPositiveRotation)
    {
        if (pAxis.x == 1)
        {
            int index = (pLayer == 2)
                ? 1
                : (pLayer == 0)
                    ? 3
                    : -1;

            if (pPositiveRotation)
            {
                int[] pivot = dots[0].GetColumn(pLayer);
                bool counterClockvise = (index == 3);

                dots[0].SetColumn(pLayer, dots[5].GetColumn(pLayer));
                dots[5].SetColumn(pLayer, Enumerable.Reverse(dots[2].GetColumn(Dimensions - pLayer - 1)).ToArray());
                dots[2].SetColumn(Dimensions - pLayer - 1, Enumerable.Reverse(dots[4].GetColumn(pLayer)).ToArray());
                dots[4].SetColumn(pLayer, pivot);

                if (index != -1)
                {
                    dots[index].RotateBy90Degrees(counterClockvise);
                }
            }
            else
            {
                int[] pivot = dots[0].GetColumn(pLayer);
                bool counterClockvise = (index == 1);

                dots[0].SetColumn(pLayer, dots[4].GetColumn(pLayer));
                dots[4].SetColumn(pLayer, Enumerable.Reverse(dots[2].GetColumn(Dimensions - pLayer - 1)).ToArray());
                dots[2].SetColumn(Dimensions - pLayer - 1, Enumerable.Reverse(dots[5].GetColumn(pLayer)).ToArray());
                dots[5].SetColumn(pLayer, pivot);

                if (index != -1)
                {
                    dots[index].RotateBy90Degrees(counterClockvise);
                }
            }
        }
        else if (pAxis.y == 1)
        {
            int index = (pLayer == 2)
                ? 4
                : (pLayer == 0)
                    ? 5
                    : -1;

            if (pPositiveRotation)
            {
                int[] pivot = dots[0].GetRow(Dimensions - pLayer - 1);
                bool counterClockvise = (index == 5);

                dots[0].SetRow(Dimensions - pLayer - 1, dots[1].GetRow(Dimensions - pLayer - 1));
                dots[1].SetRow(Dimensions - pLayer - 1, dots[2].GetRow(Dimensions - pLayer - 1));
                dots[2].SetRow(Dimensions - pLayer - 1, dots[3].GetRow(Dimensions - pLayer - 1));
                dots[3].SetRow(Dimensions - pLayer - 1, pivot);

                if (index != -1)
                {
                    dots[index].RotateBy90Degrees(counterClockvise);
                }
            }
            else
            {
                int[] pivot = dots[0].GetRow(Dimensions - pLayer - 1);
                bool counterClockvise = (index == 4);

                dots[0].SetRow(Dimensions - pLayer - 1, dots[3].GetRow(Dimensions - pLayer - 1));
                dots[3].SetRow(Dimensions - pLayer - 1, dots[2].GetRow(Dimensions - pLayer - 1));
                dots[2].SetRow(Dimensions - pLayer - 1, dots[1].GetRow(Dimensions - pLayer - 1));
                dots[1].SetRow(Dimensions - pLayer - 1, pivot);

                if (index != -1)
                {
                    dots[index].RotateBy90Degrees(counterClockvise);
                }
            }
        }
        else if (pAxis.z == 1)
        {
            int index = (pLayer == 2)
                ? 2
                : (pLayer == 0)
                    ? 0
                    : -1;

            if (pPositiveRotation)
            {
                int[] pivot = dots[4].GetRow(Dimensions - pLayer - 1);
                bool counterClockvise = (index == 0);

                dots[4].SetRow(Dimensions - pLayer - 1, dots[1].GetColumn(pLayer));
                dots[1].SetColumn(pLayer, Enumerable.Reverse(dots[5].GetRow(pLayer)).ToArray());
                dots[5].SetRow(pLayer, dots[3].GetColumn(Dimensions - pLayer - 1));
                dots[3].SetColumn(Dimensions - pLayer - 1, Enumerable.Reverse(pivot).ToArray());

                if (index != -1)
                {
                    dots[index].RotateBy90Degrees(counterClockvise);
                }
            }
            else
            {
                int[] pivot = dots[3].GetColumn(Dimensions - pLayer - 1);
                bool counterClockvise = (index == 2);

                dots[3].SetColumn(Dimensions - pLayer - 1, dots[5].GetRow(pLayer));
                dots[5].SetRow(pLayer, Enumerable.Reverse(dots[1].GetColumn(pLayer)).ToArray());
                dots[1].SetColumn(pLayer, dots[4].GetRow(Dimensions - pLayer - 1));
                dots[4].SetRow(Dimensions - pLayer - 1, Enumerable.Reverse(pivot).ToArray());

                if (index != -1)
                {
                    dots[index].RotateBy90Degrees(counterClockvise);
                }
            }
        }
    }

    private void ResetDots()
    {
        this.dots = new int[][][]
        {
            new int[][] {
                new int[] { 0, 0, 0 },
                new int[] { 0, 1, 0 },
                new int[] { 0, 0, 0 }
            },
            new int[][] {
                new int[] { 1, 0, 1 },
                new int[] { 0, 1, 0 },
                new int[] { 1, 0, 1 },
            },
            new int[][] {
                new int[] { 1, 1, 1 },
                new int[] { 0, 0, 0 },
                new int[] { 1, 1, 1 },
            },
            new int[][] {
                new int[] { 0, 0, 1 },
                new int[] { 0, 0, 0 },
                new int[] { 1, 0, 0 },
            },
            new int[][] {
                new int[] { 1, 0, 1 },
                new int[] { 0, 0, 0 },
                new int[] { 1, 0, 1 },
            },
            new int[][] {
                new int[] { 0, 0, 1 },
                new int[] { 0, 1, 0 },
                new int[] { 1, 0, 0 },
            },
        };
    }   
}

public class Path
{
    public Vector3 Axis { get; set; }
    public int Layer { get; set; }
    public bool PositiveRotation { get; set; }

    public Path(Vector3 Axis, int Layer, bool PositiveRotation)
    {
        this.Axis = Axis;
        this.Layer = Layer;
        this.PositiveRotation = PositiveRotation;
    }

    public Path()
    {
        Axis = DiceCube.Axis[DiceCube.Rand.Next(0, 3)];
        Layer = DiceCube.Rand.Next(0, RubikCube.Dimensions);
        PositiveRotation = (DiceCube.Rand.NextDouble() < 0.5f);
    }
}