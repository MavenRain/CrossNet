/*
    CrossNet - C++ Benchmark
*/

const int SIZE = 10000;

bool ForeachOnArrayTest(int N)
{
    int * array = new int[SIZE];
    for (int j = 0; j < SIZE; ++j)
    {
        array[j] = j;
    }

    for (int i = 0; i < N; ++i)
    {
        int counter = 0;
        int length = SIZE;
        for (int j = 0 ; j < length ; ++j)
        {
            counter += array[j];
        }

        if (counter != (SIZE * (SIZE - 1)) / 2)
        {
            return (false);
        }
    }

    delete[] array;
    return (true);
}
