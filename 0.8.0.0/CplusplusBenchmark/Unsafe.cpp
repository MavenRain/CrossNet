/*
    CrossNet - C++ Benchmark
*/

const int SIZE = 10000;

bool UnsafeTest(int N)
{
    int * array = new int[SIZE];
    for (int j = 0; j < SIZE; ++j)
    {
        array[j] = j;
    }

    for (int i = 0; i < N; ++i)
    {
        int counter = 0;

        {
            int * p = array;
            {
                int* buffer = p;
                int count = SIZE;
                while (count-- != 0)
                {
                    counter += *buffer++;
                }
            }
        }

        if (counter != (SIZE * (SIZE - 1)) / 2)
        {
            return (false);
        }
    }
    delete[] array;
    return (true);
}
