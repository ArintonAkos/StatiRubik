using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class ArrayExtensions
{
    public static void SetColumn<T>(this T[][] matrix, int columnNumber, T[] column)
    {
        for (int i = 0; i < RubikCube.Dimensions; i++)
        {
            matrix[i][columnNumber] = column[i];
        }
    }

    public static T[] GetColumn<T>(this T[][] matrix, int columnNumber)
    {
        return Enumerable.Range(0, RubikCube.Dimensions)
                .Select(x => matrix[x][columnNumber])
                .ToArray();
    }

    public static void SetRow<T>(this T[][] matrix, int rowNumber, T[] row)
    {
        for (int i = 0; i < RubikCube.Dimensions; i++)
        {
            matrix[rowNumber][i] = row[i];
        }
    }

    public static T[] GetRow<T>(this T[][] matrix, int rowNumber)
    {
        return Enumerable.Range(0, RubikCube.Dimensions)
                .Select(x => matrix[rowNumber][x])
                .ToArray();
    }

    public static void RotateBy90Degrees<T>(this T[][] matrix, bool counterClockVise = false)
    {
        if (counterClockVise)
        {
            for (int i = 0; i < matrix.Length / 2; i++)
            {
                for (int j = i; j < matrix.Length - i - 1; j++)
                {
                    T temp = matrix[i][j];
                    matrix[i][j] = matrix[j][matrix.Length - 1 - i];
                    matrix[j][matrix.Length - 1 - i] = matrix[matrix.Length - 1 - i][matrix.Length - 1 - j];
                    matrix[matrix.Length - 1 - i][matrix.Length - 1 - j] = matrix[matrix.Length - 1 - j][i];
                    matrix[matrix.Length - 1 - j][i] = temp;
                }
            }

            return;
        }


        for (int i = 0; i < matrix.Length / 2; i++)
        {
            for (int j = i; j < matrix.Length - i - 1; j++)
            {
                T temp = matrix[i][j];
                matrix[i][j] = matrix[matrix.Length - 1 - j][i];
                matrix[matrix.Length - 1 - j][i] = matrix[matrix.Length - 1 - i][matrix.Length - 1 - j];
                matrix[matrix.Length - 1 - i][matrix.Length - 1 - j] = matrix[j][matrix.Length - 1 - i];
                matrix[j][matrix.Length - 1 - i] = temp;
            }
        }
    }

    private static void Transpose<T>(this T[][] matrix)
    {
        for (int i = 0; i < matrix.Length; i++)
        {
            for (int j = i; j < matrix.Length; j++)
            {
                (matrix[j][i], matrix[i][j]) = (matrix[i][j], matrix[j][i]);
            }
        }
    }
}
