/*
    CrossNet - C++ Benchmark
*/

int n = 16;

bool NestedLoopsTest(int N)
{
    for (int i = 0; i < N; ++i)
    {
        int x = 0;
        int a = n;
        while (a-- != 0)
        {
            int b = n;
            while (b-- != 0)
            {
                int c = n;
                while (c-- != 0)
                {
                    int d = n;
                    while (d-- != 0)
                    {
                        int e = n;
                        while (e-- != 0)
                        {
                            int f = n;
                            while (f-- != 0)
                            {
                                x++;
                            }
                        }
                    }
                }
            }
        }
        if (x != 16777216)
        {
            return (false);
        }
    }
    return (true);
}

void ForceCompilation(int N)
{
    n = N;
}
