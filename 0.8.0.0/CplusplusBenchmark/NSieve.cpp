/*
    CrossNet - C++ Benchmark
*/

static int nsieve(int m, bool * isPrime)
{
    for (int i = 2; i <= m; i++)
        isPrime[i] = true;
    int count = 0;

    for (int i = 2; i <= m; i++)
    {
        if (isPrime[i])
        {
            for (int k = i + i; k <= m; k += i)
                isPrime[k] = false;
            count++;
        }
    }
    return count;
}

bool NSieveTest(int N)
{
    for (int i = 0; i < N; ++i)
    {
        const int m = 1 * 1024 * 1024;      // 1 MB so it mostly stays in cache
                                            //  We are benchmarking algorithm, not memory accesses ;)
        bool * flags = new bool[m + 1];
        int numPrimes = nsieve(m, flags);
        if (numPrimes != 82025)
        {
            return (false);
        }
        delete[] flags;
    }
    return (true);
}
