/*
    CrossNet - Copyright (c) 2007 Olivier Nallet

    Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
    to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
    and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
    DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
    OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Text;

using Reflector;
using Reflector.CodeModel;

using CrossNet.Common;
using CrossNet.Interfaces;

namespace CrossNet.Net
{
    public class LanguageManager
    {
        public static ITypeGenerator TypeGenerator
        {
            get
            {
                return (mTypeGenerator);
            }
        }

        public static IStatementGenerator StatementGenerator
        {
            get
            {
                return (mStatementGenerator);
            }
        }

        public static IExpressionGenerator ExpressionGenerator
        {
            get
            {
                return (mExpressionGenerator);
            }
        }

        public static IReferenceGenerator ReferenceGenerator
        {
            get
            {
                return (mReferenceGenerator);
            }
        }

        public static INameFixup NameFixup
        {
            get
            {
                return (mNameFixup);
            }
        }

        public static ILocalTypeManager LocalTypeManager
        {
            get
            {
                return (mLocalTypeManager);
            }
        }

        public static ITypeInfoFactory TypeInfoFactory
        {
            get
            {
                return (mTypeInfoFactory);
            }
        }

        public static void Init(ITypeGenerator typeGenerator, IStatementGenerator statementGenerator, IExpressionGenerator expressionGenerator,
                                IReferenceGenerator referenceGenerator, INameFixup nameFixup,
                                ILocalTypeManager localtypeManager, ITypeInfoFactory typeInfoFactory)
        {
            mTypeGenerator = typeGenerator;
            mStatementGenerator = statementGenerator;
            mExpressionGenerator = expressionGenerator;
            mReferenceGenerator = referenceGenerator;
            mNameFixup = nameFixup;
            mLocalTypeManager = localtypeManager;
            mTypeInfoFactory = typeInfoFactory;
        }

        public static GeneratedData GenerateCode(IAssembly assembly, AssemblyData assemblyData)
        {
            return (TypeGenerator.GenerateCode(assembly, assemblyData));
        }

        private static IExpressionGenerator mExpressionGenerator;
        private static IReferenceGenerator mReferenceGenerator;
        private static IStatementGenerator mStatementGenerator;
        private static ITypeGenerator mTypeGenerator;
        private static INameFixup mNameFixup;
        private static ILocalTypeManager mLocalTypeManager;
        private static ITypeInfoFactory mTypeInfoFactory;

    }
}
