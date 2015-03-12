/*
    CrossNet - C++ Benchmark
*/

#include <hash_map>

template <typename Key>
struct MyCompare
{
    enum
    {
        bucket_size = 4,
        min_buckets = 8,
    };

    MyCompare()
    {
    }

    size_t operator()(Key key) const
    {
        return (size_t)(key);
    }

    bool operator()(Key key1, Key key2) const
    {
        return (key1 != key2);
    }
};

bool DictionaryTest(int N)
{
    for (int i = 0; i < N; ++i)
    {
        const int NUM_VALUES = 10 * 1024;

        typedef stdext::hash_map<int, double, MyCompare<int> > Dictionary;
        Dictionary   dic;

        for (int j = 0; j < NUM_VALUES; ++j)
        {
            dic[j] = (double)j;
        }

        for (int j = 0; j < 100; ++j)
        {
            for (int k = 0; k < NUM_VALUES; ++k)
            {
                Dictionary::const_iterator it = dic.find(k);
                double result = it->second;
                if (result != (double)k)
                {
                    return (false);
                }
            }
        }
    }
    return (true);
}

