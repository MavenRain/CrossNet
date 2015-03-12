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
using Reflector.CodeModel.Memory;

using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.Common
{
    static class TypeInfoManager
    {
        public static ITypeInfo FindTypeInfo(string fullName)
        {
            ITypeInfo value;
            bool found = sAllTypeInfosFullName.TryGetValue(fullName, out value);
            return (value);
        }

        public static ITypeInfo GetTypeInfo(LocalType localType)
        {
            IType type = localType.EmbeddedType;
            if (type is IReferenceType)
            {
                type = ((IReferenceType)type).ElementType;
            }
            ITypeReference typeReference = type as ITypeReference;
            if (typeReference != null)
            {
                return (GetTypeInfo(typeReference));
            }
            return (null);
        }

        public static ITypeInfo GetTypeInfo(IType type)
        {
            ITypeReference typeReference = type as ITypeReference;
            if (typeReference != null)
            {
                return (GetTypeInfo(typeReference));
            }
            return (null);
        }

        public static ITypeInfo GetTypeInfo(ITypeReference typeReference)
        {
            if (typeReference == null)
            {
                return (null);
            }
            ITypeInfo value;
            bool found;

            ++sReentrantCounter;
            if (sReentrantCounter < NUM_MAX_RECURSION)
            {
                // In some very rare cases (like in System.Dat.SqlXml.dll),
                // the type can reference itself. Especially with templated code...
                // Detect the case and avoid to look up in the hash-table one more time
                // to avoid infinite recursion...
                // The issue is because of the incorrect comparison in Reflector, we have to use a function that _might_ call GetTypeInfo()
                found = sAllTypeInfosReference.TryGetValue(typeReference, out value);
                if (found)
                {
                    --sReentrantCounter;
                    return (value);
                }
            }
            string fullName = LanguageManager.ReferenceGenerator.GenerateCodeTypeReferenceAsString(typeReference, sFakeParsingInfo);
            found = sAllTypeInfosFullName.TryGetValue(fullName, out value);
            if (found)
            {
                --sReentrantCounter;
                return (value);
            }

            --sReentrantCounter;

            value = LanguageManager.TypeInfoFactory.Create(typeReference, fullName);
            sAllTypeInfosFullName.Add(fullName, value);
            sAllTypeInfosReference.Add(typeReference, value);
            value.FillInfo(typeReference);      // Have to call FillInfo to get the TypeDeclaration...
            sAllTypeInfosDeclaration[value.TypeDeclaration] = value;

            return (value);
        }

        public static ITypeInfo GetTypeInfo(ITypeDeclaration typeDeclaration)
        {
            if (typeDeclaration == null)
            {
                return (null);
            }
            ITypeInfo value;
            bool found;

            ++sReentrantCounter;
            if (sReentrantCounter < NUM_MAX_RECURSION)
            {
                // In some very rare cases (like in System.Dat.SqlXml.dll),
                // the type can reference itself. Especially with templated code...
                // Detect the case and avoid to look up in the hash-table one more time
                // to avoid infinite recursion...
                // The issue is because of the incorrect comparison is Reflector, we have to use a function that _might_ call GetTypeInfo()


                found = sAllTypeInfosDeclaration.TryGetValue(typeDeclaration, out value);
                if (found)
                {
                    --sReentrantCounter;
                    return (value);
                }
            }

            sFakeParsingInfo.DebugTypeFullName = typeDeclaration.Namespace + "::" + typeDeclaration.Name;
            string fullName = LanguageManager.ReferenceGenerator.GenerateCodeTypeReference(typeDeclaration, sFakeParsingInfo).Text;
            found = sAllTypeInfosFullName.TryGetValue(fullName, out value);
            if (found)
            {
                --sReentrantCounter;
                return (value);
            }

            --sReentrantCounter;

            value = LanguageManager.TypeInfoFactory.Create(typeDeclaration, fullName);
            sAllTypeInfosFullName.Add(fullName, value);
            sAllTypeInfosDeclaration.Add(typeDeclaration, value);
            sAllTypeInfosReference[typeDeclaration] = value;        // ITypeDeclaration is a ITypeReference
            value.FillInfo(typeDeclaration);
            return (value);
        }

        public static bool HasImplicitOperator(LocalType localType)
        {
            IReferenceType refType = localType.EmbeddedType as IReferenceType;
            if (refType != null)
            {
                localType = LanguageManager.LocalTypeManager.GetLocalType(refType.ElementType);
            }
            if (localType.IsPrimitiveType)
            {
                return (true);
            }
            if (localType.Same(LanguageManager.LocalTypeManager.TypeString))
            {
                return (true);
            }
            return (false);
        }

        class TypeReferenceComparer : IEqualityComparer<ITypeReference>
        {
            public bool Equals(ITypeReference x, ITypeReference y)
            {
/*
                if (x.Equals(y) == false)
                {
                    return (false);
                }

                // Do the comparison of the generic components
                // Reflector doesn't do this...
                // Actually there is a bug in IType.Equals()
                // Even if the IType don't represent the same things, they will return true...
                int numArgs = x.GenericArguments.Count;
                if (numArgs != y.GenericArguments.Count)
                {
                    return (false);
                }

                for (int i = 0; i < numArgs; ++i)
                {
                    IType xType = x.GenericArguments[i];
                    IType yType = y.GenericArguments[i];
                    // We have to use CompareTo to solve the Equals bug
                    if (xType.CompareTo(yType) != 0)
                    {
                        return (false);
                    }
                }
                return (true);
 */
                return (Util.CompareTypeReference(x, y));
            }

            public int GetHashCode(ITypeReference obj)
            {
                return (obj.GetHashCode());
            }
        }

        class TypeDeclarationComparer : IEqualityComparer<ITypeDeclaration>
        {
            public bool Equals(ITypeDeclaration x, ITypeDeclaration y)
            {
/*
                if (x.Equals(y) == false)
                {
                    return (false);
                }

                // Do the comparison of the generic components
                // Reflector doesn't do this...
                // Actually there is a bug in IType.Equals()
                // Even if the IType don't represent the same things, they will return true...
                int numArgs = x.GenericArguments.Count;
                if (numArgs != y.GenericArguments.Count)
                {
                    return (false);
                }

                for (int i = 0; i < numArgs; ++i)
                {
                    IType xType = x.GenericArguments[i];
                    IType yType = y.GenericArguments[i];
                    // We have to use CompareTo to solve the Equals bug
                    if (xType.CompareTo(yType) != 0)
                    {
                        return (false);
                    }
                }
                return (true);
 */
                return (Util.CompareTypeReference(x, y));
            }

            public int GetHashCode(ITypeDeclaration obj)
            {
                return (obj.GetHashCode());
            }
        }

        private static int sReentrantCounter = 0;

#warning    Find a better solution for type comparison...
        private const int NUM_MAX_RECURSION = 4;

        private static IEqualityComparer<ITypeReference> sTypeReferenceComparer = new TypeReferenceComparer();
        private static IEqualityComparer<ITypeDeclaration> sTypeDeclarationComparer = new TypeDeclarationComparer();

        private static IDictionary<ITypeReference, ITypeInfo> sAllTypeInfosReference = new Dictionary<ITypeReference, ITypeInfo>(1000, sTypeReferenceComparer);
        private static IDictionary<ITypeDeclaration, ITypeInfo> sAllTypeInfosDeclaration = new Dictionary<ITypeDeclaration, ITypeInfo>(1000, sTypeDeclarationComparer);
        private static IDictionary<string, ITypeInfo> sAllTypeInfosFullName = new Dictionary<string, ITypeInfo>(1000);
        private static readonly ParsingInfo sFakeParsingInfo = new ParsingInfo(null);
    }
}
