/*
    CrossNet - C++ Benchmark
*/

#include "dlmalloc.h"

static int SIZE = 30;

#define SIZE 30

int **mkmatrix(int rows, int cols) {
    int i, j, count = 1;
    int **m = (int **) dlmalloc(rows * sizeof(int *));
    for (i=0; i<rows; i++) {
    m[i] = (int *) dlmalloc(cols * sizeof(int));
    for (j=0; j<cols; j++) {
        m[i][j] = count;
        ++count;                // To match C# version because of Reflector's bug
    }
    }
    return(m);
}

void zeromatrix(int rows, int cols, int **m) {
    int i, j;
    for (i=0; i<rows; i++)
    for (j=0; j<cols; j++)
        m[i][j] = 0;
}

void freematrix(int rows, int **m) {
    while (--rows > -1) { dlfree(m[rows]); }
    dlfree(m);
}

int **mmult(int rows, int cols, int **m1, int **m2, int **m3) {
    int i, j, k, val;
    for (i=0; i<rows; i++) {
    for (j=0; j<cols; j++) {
        val = 0;
        for (k=0; k<cols; k++) {
        val += m1[i][k] * m2[k][j];
        }
        m3[i][j] = val;
    }
    }
    return(m3);
}

bool MatrixTest(int N)
{
    int **m1 = mkmatrix(SIZE, SIZE);
    int **m2 = mkmatrix(SIZE, SIZE);
    int **mm = mkmatrix(SIZE, SIZE);

    for (int i=0; i<N; i++)
    {
        mm = mmult(SIZE, SIZE, m1, m2, mm);

        if (mm[0][0] != 270165)
        {
            return (false);
        }

        if (mm[2][7] != 1070820)
        {
            return (false);
        }

        if (mm[17][5] != 7019790)
        {
            return (false);
        }

        if (mm[25][12] != 10355745)
        {
            return (false);
        }
    }

    freematrix(SIZE, m1);
    freematrix(SIZE, m2);
    freematrix(SIZE, mm);
    return(true);
}
