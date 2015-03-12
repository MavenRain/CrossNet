/*
    CrossNet - C++ Benchmark
*/

#include <stdlib.h>
#include "dlmalloc.h"

static int NUM_TOGGLE = 1000 * 1000;
static volatile bool sVolatileValue;

static void * sNextAlloc = NULL;

class Toggle
{
public:
    bool state;
    explicit Toggle(bool start_state)
    {
        this->state = start_state;
    }

    bool value()
    {
        return (this->state);
    }

    Toggle * activate()
    {
        this->state = !this->state;
        return (this);
    }

    void * operator new(size_t /*size*/)
    {
        if (sNextAlloc != NULL)
        {
            void * nextPtr = *(void * *)sNextAlloc;
            void * ptr = sNextAlloc;
            sNextAlloc = nextPtr;
            return (ptr);
        }
        else
        {
            // Assume 16 is fine for the allocations... (this class and derived class as well).
            return (dlmalloc(16));
        }
    }

    void operator delete(void * buffer)
    {
        *(void * *)buffer = sNextAlloc;
        sNextAlloc = buffer;
    }
};

class NthToggle : public Toggle
{
public:
    int count_max;
    int counter;

    NthToggle(bool start_state, int max_counter)
        : Toggle(start_state)
    {
        this->count_max = max_counter;
        this->counter = 0;
    }
    NthToggle * activate()
    {
        this->counter += 1;
        if (this->counter >= this->count_max)
        {
            this->state = !this->state;
            this->counter = 0;
        }
        return (this);
    }
};

bool GcTest(int N)
{
    for (int loop = 0; loop < N; ++loop)
    {
        Toggle * mainToggle = new Toggle(true);
        for (int i = 0; i < 5; i++)
        {
            sVolatileValue = mainToggle->activate()->value();
        }

        // Create temp objects (that are going to be collected soon)
        for (int i = 0; i < NUM_TOGGLE; i++)
        {
            Toggle * toggle = new Toggle(true);
            delete toggle;
        }

        NthToggle * nthToggle = new NthToggle(true, 3);
        for (int i = 0; i < 8; i++)
        {
            sVolatileValue = nthToggle->activate()->value();
        }

        for (int i = 0; i < NUM_TOGGLE; i++)
        {
            NthToggle * toggle = new NthToggle(true, 3);
            delete toggle;
        }

        // To make sure local variables are traced correctly...
        for (int i = 0; i < 5; i++)
        {
            sVolatileValue = mainToggle->activate()->value();
        }

        for (int i = 0; i < 8; i++)
        {
            sVolatileValue = nthToggle->activate()->value();
        }

        delete mainToggle;
        delete nthToggle;
    }

    return (true);
}

