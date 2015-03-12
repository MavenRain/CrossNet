/*
    CrossNet - C++ Benchmark
*/

#include <math.h>

const double twothirds = 2.0 / 3.0;

static double s1, s2, s3, s4, s5, s6, s7, s8, s9;

bool PartialSumsTest(int N)
{
    const int ITERATIONS = 1000;

    for (int i = 0; i < N; ++i)
    {
        double a1 = 0.0, a2 = 0.0, a3 = 0.0, a4 = 0.0, a5 = 0.0;
        double a6 = 0.0, a7 = 0.0, a8 = 0.0, a9 = 0.0, alt = -1.0;

        for (int k = 1; k <= ITERATIONS; k++)
        {
            double k2 = pow((double)k, (double)2.0), k3 = k2 * k;
            double sk = sin((double)k), ck = cos((double)k);
            alt = -alt;

            a1 += pow(twothirds, (double)(k - 1));
            a2 += pow((double)k, (double)-0.5);
            a3 += 1.0 / (k * (k + 1.0));
            a4 += 1.0 / (k3 * sk * sk);
            a5 += 1.0 / (k3 * ck * ck);
            a6 += 1.0 / k;
            a7 += 1.0 / k2;
            a8 += alt / k;
            a9 += alt / (2.0 * k - 1.0);
        }

        s1 = a1;
        s2 = a2;
        s3 = a3;
        s4 = a4;
        s5 = a5;
        s6 = a6;
        s7 = a7;
        s8 = a8;
        s9 = a9;
    }
    return (true);
}
