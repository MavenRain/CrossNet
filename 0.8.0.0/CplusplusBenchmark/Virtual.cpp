/*
    CrossNet - C++ Benchmark
*/

class BaseClass
{
public:
    virtual int Function(int /*a*/)
    {
        return (-1);
    }
};

class MyClass : public BaseClass
{
public:
    virtual int Function(int a)
    {
        return (a);
    }
};

bool VirtualTest(int N)
{
    BaseClass * myClass0 = new MyClass();
    BaseClass * myClass1 = new MyClass();
    BaseClass * myClass2 = new MyClass();
    BaseClass * myClass3 = new MyClass();
    BaseClass * myClass4 = new MyClass();
    BaseClass * myClass5 = new MyClass();
    BaseClass * myClass6 = new MyClass();
    BaseClass * myClass7 = new MyClass();
    BaseClass * myClass8 = new MyClass();
    BaseClass * myClass9 = new MyClass();
    BaseClass * myClassA = new MyClass();
    BaseClass * myClassB = new MyClass();
    BaseClass * myClassC = new MyClass();
    BaseClass * myClassD = new MyClass();
    BaseClass * myClassE = new MyClass();
    BaseClass * myClassF = new MyClass();

    for (int i = 0; i < N; ++i)
    {
        for (int k = 0; k < 1000 * 1000; ++k)
        {
            // Note that this benchmark will spend a lot of time in the loop code
            //  This actually will make it a closer of a real code
            //  (i.e. we are never doing 1 million interface call in a row, there is always
            //  something else around. So I guess this would be a good demonstration of a real worst case).
            int result = myClass0->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClass1->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClass2->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClass3->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClass4->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClass5->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClass6->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClass7->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClass8->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClass9->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClassA->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClassB->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClassC->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClassD->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClassE->Function(42);
            if (result != 42)
            {
                return (false);
            }

            result = myClassF->Function(42);
            if (result != 42)
            {
                return (false);
            }
        }
    }

    delete myClass0;
    delete myClass1;
    delete myClass2;
    delete myClass3;
    delete myClass4;
    delete myClass5;
    delete myClass6;
    delete myClass7;
    delete myClass8;
    delete myClass9;
    delete myClassA;
    delete myClassB;
    delete myClassC;
    delete myClassD;
    delete myClassE;
    delete myClassF;

    return (true);
}