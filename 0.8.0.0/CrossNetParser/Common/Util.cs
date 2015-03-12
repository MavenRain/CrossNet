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
using System.Diagnostics;
using System.IO;
using System.Text;

using Reflector.CodeModel;

using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.Common
{
    public class Util
    {
        public static void WriteFileIfChanged(string fileName, string content)
        {
            bool writeFile = false;
            if (File.Exists(fileName) == false)
            {
                // File doesn't exist, write it
                writeFile = true;
            }
            else
            {
                // File does exist, compare the content
                string oldContent = File.ReadAllText(fileName);
                if (oldContent != content)
                {
                    // Different content, write it...
                    writeFile = true;
                }
            }

            if (writeFile)
            {
                File.WriteAllText(fileName, content);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection1"></param>
        /// <param name="collection2"></param>
        /// <param name="nonExactComparison">If true, the test will compare that collection1 parameters can be base classes of collection2 parameters. The comparison doesn't have to be a 100% match.</param>
        /// <returns></returns>
        public static bool CompareParameterDeclaration(IParameterDeclarationCollection collection1, IParameterDeclarationCollection collection2, bool nonExactComparison)
        {
            // First handle the case where there is no parameters...
            if ((collection1 == null) || (collection1.Count == 0))
            {
                if ((collection2 == null) || (collection2.Count == 0))
                {
                    return (true);
                }
                return (false);
            }

            int count = collection1.Count;
            if (count != collection2.Count)
            {
                // Not same number of parameters
                return (false);
            }

            int i;
            for (i = 0; i < count; ++i)
            {
                IParameterDeclaration parameter1 = collection1[i];
                IParameterDeclaration parameter2 = collection2[i];

                IType resolvedType1;
                ITypeReference parRef1 = GetCorrespondingTypeReference(parameter1.ParameterType, out resolvedType1);
                IType resolvedType2;
                ITypeReference parRef2 = GetCorrespondingTypeReference(parameter2.ParameterType, out resolvedType2);
                if ((parRef1 != null) && (parRef2 != null))
                {
                    if (CompareTypeReference(parRef1, parRef2) == false)
                    {
                        // When translated to reference, they are not pointing to the same types...

                        if (nonExactComparison)
                        {
                            // In case the comparison is not exact, let's see if parRef1 is base type of parRef2
                            ITypeInfo typeInfo1 = TypeInfoManager.GetTypeInfo(parRef1);
                            ITypeInfo typeInfo2 = TypeInfoManager.GetTypeInfo(parRef2);

                            if ((typeInfo1 != null) && (typeInfo2 != null))
                            {
                                if (typeInfo2.IsBaseType(typeInfo1))
                                {
                                    continue;
                                }
                            }
                        }
                        return (false);
                    }
                    continue;
                }

                // In case one of the typeReference is null, use the default, unaccurate comparison
                if (resolvedType1.CompareTo(resolvedType2) != 0)
                {
                    // These parameters don't have the same type
                    return (false);
                }
            }
            return (true);
        }

        public static IParameterDeclarationCollection CreateParameterDeclaration(params LocalType[] localTypes)
        {
            if ((localTypes == null) || (localTypes.Length == 0))
            {
                return (null);
            }
            IParameterDeclarationCollection collection = new ParameterDeclarationCollection();
            foreach (LocalType localType in localTypes)
            {
                collection.Add(new ParameterDeclaration(localType));
            }
            return (collection);
        }

        private class ParameterDeclarationCollection : IParameterDeclarationCollection
        {
            public void Add(IParameterDeclaration value)
            {
                mCollection.Add(value);
            }

            public void AddRange(System.Collections.ICollection value)
            {
                foreach (IParameterDeclaration parameter in value)
                {
                    mCollection.Add(parameter);
                }
            }

            public void Clear()
            {
                mCollection.Clear();
            }

            public bool Contains(IParameterDeclaration value)
            {
                return (mCollection.Contains(value));
            }

            public int IndexOf(IParameterDeclaration value)
            {
                return (mCollection.IndexOf(value));
            }

            public void Insert(int index, IParameterDeclaration value)
            {
                mCollection.Insert(index, value);
            }

            public void Remove(IParameterDeclaration value)
            {
                mCollection.Remove(value);
            }

            public void RemoveAt(int index)
            {
                mCollection.RemoveAt(index);
            }

            public IParameterDeclaration this[int index]
            {
                get
                {
                    return (mCollection[index]);
                }
                set
                {
                    mCollection[index] = value;
                }
            }

            public void CopyTo(Array array, int index)
            {
                mCollection.CopyTo((ParameterDeclaration[])array, index);
            }

            public int Count
            {
                get
                {
                    return (mCollection.Count);
                }
            }

            public bool IsSynchronized
            {
                get
                {
                    return (false);
                }
            }

            public object SyncRoot
            {
                get
                {
                    return (this);
                }
            }

            public System.Collections.IEnumerator GetEnumerator()
            {
                return (mCollection.GetEnumerator());
            }

            private List<IParameterDeclaration> mCollection = new List<IParameterDeclaration>();
        }

        private class ParameterDeclaration : IParameterDeclaration
        {
            public ParameterDeclaration(LocalType localType)
            {
                mLocalType = localType;
            }

            public IType ParameterType
            {
                get
                {
                    return (mLocalType.EmbeddedType);
                }
                set
                {
                    mLocalType = LanguageManager.LocalTypeManager.GetLocalType(value);
                }
            }

            public string Name
            {
                get
                {
                    return (mLocalType.ToString());
                }
                set
                {
                    throw new Exception("The method or operation is not implemented.");
                }
            }

            public IParameterDeclaration Resolve()
            {
                throw new Exception("The method or operation is not implemented.");
            }

            public ICustomAttributeCollection Attributes
            {
                get
                {
                    throw new Exception("The method or operation is not implemented.");
                }
            }

            public override string ToString()
            {
                return (mLocalType.FullName);
            }

            private LocalType mLocalType;
        }

        /// <summary>
        /// Gets the corresponding type reference, if possible.
        /// </summary>
        /// <param name="type">The type to parse.</param>
        /// <returns>The corresponding type reference, otherwise null.</returns>
        /// <remarks>This function is mainly used in regard to generics as if possible, 
        /// it will resolve the type down to the type reference.
        /// This more reliable than just converting to an ITypeReference, as in some cases this is not enough.</remarks>
        public static ITypeReference GetCorrespondingTypeReference(IType type, out IType resolvedType)
        {
            resolvedType = type;
            // First we check if it's already a TypeReference
            ITypeReference typeReference = type as ITypeReference;
            if (typeReference != null)
            {
                return (typeReference);
            }

            // No, so let's check if that's an IGenericArgument
            IGenericArgument genericArgument = type as IGenericArgument;
            if (genericArgument != null)
            {
                int index = genericArgument.Position;
                ITypeCollection collection = genericArgument.Owner.GenericArguments;
                IType genericType = collection[index];

                // Be careful with the following code...
                // Due to reflector circular data, it has a tendency to do infinite recursion

                for (; ; )
                {
                    resolvedType = genericType;
                    IGenericParameter genericParameter = genericType as IGenericParameter;
                    if (genericParameter != null)
                    {
                        genericType = genericParameter.Resolve();
                        if (genericType == null)
                        {
                            // Named template parameter (i.e. we don't know the type)
                            Debug.Assert(genericParameter.Variance == GenericParameterVariance.NonVariant);
                            return (null);
                        }
                        return (GetCorrespondingTypeReference(genericType, out resolvedType));
                    }

                    genericArgument = genericType as IGenericArgument;
                    if (genericArgument != null)
                    {
                        index = genericArgument.Position;
                        collection = genericArgument.Owner.GenericArguments;
                        genericType = collection[index];
                        continue;
                    }

                    typeReference = genericType as ITypeReference;
                    // If it's a ITypeReference, call the ITypeReference directly...
                    if (typeReference != null)
                    {
                        return (typeReference);
                    }

                    IArrayType arrayType = genericType as IArrayType;
                    if (arrayType != null)
                    {
                        // genericType can actually be IArrayType - TODO: Handle this case...
                        // For the moment do something fake with a code markup to detect the issue...
                        return (GetCorrespondingTypeReference(arrayType, out resolvedType));
                    }

                    Debug.Fail("Should never be here!");
                }
            }

            IRequiredModifier requiredModifier = type as IRequiredModifier;
            if (requiredModifier != null)
            {
                return (GetCorrespondingTypeReference(requiredModifier.ElementType, out resolvedType));
            }

            // It doesn't seem that we can convert it to a type reference...
            return (null);
        }

        public static bool CompareType(IType x, IType y)
        {
            IType resolvedType1;
            ITypeReference typeReferenceX = GetCorrespondingTypeReference(x, out resolvedType1);
            IType resolvedType2;
            ITypeReference typeReferenceY = GetCorrespondingTypeReference(y, out resolvedType2);

            if ((typeReferenceX != null) && (typeReferenceY != null))
            {
                return (CompareTypeReference(typeReferenceX, typeReferenceY));
            }
            return (resolvedType1.CompareTo(resolvedType2) == 0);
        }

        public static bool CompareTypeReference(ITypeReference x, ITypeReference y)
        {
/*
 * This is a quick way to determine if two types are the same or not
 * Unfortunately in some case (comparing type declaration with type reference), it is going to return false when we expect true
 * So currently we are doing a complete string comparison to have the safest (and slowest) result...
 *  * 
            if (x.Equals(y) == false)
            {
                return (false);
            }
*/
            // Before running the slow more, run some quick comparisons
            // Name, namespace, then number of generic arguments...
            if (x.Name != y.Name)
            {
                return (false);
            }
            if (x.Namespace != y.Namespace)
            {
                return (false);
            }

            int numArgs = x.GenericArguments.Count;
            if (numArgs != y.GenericArguments.Count)
            {
                return (false);
            }

#if false
            // Do the comparison of the generic components
            // Reflector doesn't do this...
            // Actually there is a bug in IType.Equals()
            // Even if the IType don't represent the same things, they will return true...

            for (int i = 0; i < numArgs; ++i)
            {
                IType xType = x.GenericArguments[i];
                IType yType = y.GenericArguments[i];
                // We have to use CompareTo to solve the Equals bug
                if (xType.CompareTo(yType) != 0)
                {
                    return (false);
                    break;
                }
            }
            return (true);
#else

            // The other solution is faster, that one returns more correct result...
            ParsingInfo info = new ParsingInfo(null);
#if DEBUG
            info.DebugTypeFullName = x.Namespace + "::" + x.Name;
#endif
            string firstText = LanguageManager.ReferenceGenerator.GenerateCodeTypeReferenceAsString(x, info);
            string secondText = LanguageManager.ReferenceGenerator.GenerateCodeTypeReferenceAsString(y, info);
            // Check if the two types are the same
            return (firstText == secondText);
#endif
        }

        public static StringData CombineStatements(StringData statement, ParsingInfo info)
        {
            if ((info.GetPreStatements() == null) && (info.GetPostStatements() == null))
            {
                // Most of the time, there is no pre and post-statements...
                return (statement);
            }

            if (info.CombineStatementEnabled == false)
            {
                // The combine has been disabled, this is a rare condition
                // So do the test _after_ pre and post statements have been tested
                return (statement);
            }

            // There is either some pre or pro statements
            StringData thisStatement = new StringData();

            if (info.GetPreStatements() != null)
            {
                // Add pre-statements
                foreach (StringData preStatement in info.GetPreStatements())
                {
                    thisStatement.Append(preStatement);
                    thisStatement.AppendSameLine(";\n");
                }
                info.ClearPreStatements();
            }

            // Add the current statement
            thisStatement.Append(statement);

            if (info.GetPostStatements() != null)
            {
                // Add the post-statements
                foreach (StringData postStatement in info.GetPostStatements())
                {
                    thisStatement.Append(postStatement);
                    thisStatement.AppendSameLine(";\n");
                }
                info.ClearPostStatements();
            }
            return (thisStatement);
        }

        public static StringData AppendStatements(StringData statement, StringData[] statements)
        {
            if (statements == null)
            {
                // Most of the time, there is no pre and pos statements...
                return (statement);
            }

            // Add statements
            foreach (StringData preStatement in statements)
            {
                statement.Append(preStatement);
                statement.AppendSameLine(";\n");
            }
            return (statement);
        }

        // Find the corresponding method...
        public static IMethodDeclaration FindMethod(ITypeInfo type, string name, IParameterDeclarationCollection parameters, IType returnType, bool nonExactComparison)
        {
            foreach (IMethodDeclaration method in type.TypeDeclaration.Methods)
            {
                if (method.Name != name)
                {
                    continue;
                }
                if (Util.CompareParameterDeclaration(method.Parameters, parameters, nonExactComparison) == false)
                {
                    continue;
                }

                if (returnType != null)
                {
                    if (Util.CompareType(method.ReturnType.Type, returnType) == false)
                    {
                        continue;
                    }
                }
                return (method);
            }

            // Didn't find it, try the base implementations
            if (type.BaseType != null)
            {
                IMethodDeclaration baseMethod = FindMethod(type.BaseType, name, parameters, returnType, nonExactComparison);
                if (baseMethod != null)
                {
                    return (baseMethod);
                }
            }

            // Still not, try now the interfaces
            foreach (ITypeInfo oneInterface in type.UnionOfInterfaces)
            {
                if (object.ReferenceEquals(type, oneInterface))
                {
                    // Don't search recursively for itself...
                    continue;
                }

                // Potentially here we might parse several time the same interfaces
                // if interfaces are deriving from other interfaces
                IMethodDeclaration baseMethod = FindMethod(oneInterface, name, parameters, returnType, nonExactComparison);
                if (baseMethod != null)
                {
                    return (baseMethod);
                }
            }
            return (null);
        }

        public static ITypeInfo FindInterfaceDefiningMethod(ITypeInfo type, string name, IParameterDeclarationCollection parameters, IType returnValue)
        {
            // Search in the current type
            foreach (IMethodDeclaration method in type.TypeDeclaration.Methods)
            {
                if (method.Name != name)
                {
                    continue;
                }
                if (Util.CompareParameterDeclaration(method.Parameters, parameters, false) == false)
                {
                    continue;
                }

                if (returnValue != null)
                {
                    if (Util.CompareType(method.ReturnType.Type, returnValue) == false)
                    {
                        continue;
                    }
                }
                return (type);
            }

            // Then each sub-interfaces
            foreach (ITypeInfo oneInterface in type.UnionOfInterfaces)
            {
                if (object.ReferenceEquals(type, oneInterface))
                {
                    // Already searched...
                    continue;
                }

                foreach (IMethodDeclaration method in oneInterface.TypeDeclaration.Methods)
                {
                    if (method.Name != name)
                    {
                        continue;
                    }
                    if (Util.CompareParameterDeclaration(method.Parameters, parameters, false) == false)
                    {
                        continue;
                    }

                    if (returnValue != null)
                    {
                        if (Util.CompareType(method.ReturnType.Type, returnValue) == false)
                        {
                            continue;
                        }
                    }
                    return (oneInterface);
                }
            }
            return (null);
        }

        public static IPropertyDeclaration FindProperty(ITypeInfo type, string name, IParameterDeclarationCollection parameters)
        {
            foreach (IPropertyDeclaration property in type.TypeDeclaration.Properties)
            {
                if (property.Name != name)
                {
                    continue;
                }
                if (Util.CompareParameterDeclaration(property.Parameters, parameters, false) == false)
                {
                    continue;
                }
                return (property);
            }

            // Didn't find it, try the base implementations
            if (type.BaseType != null)
            {
                IPropertyDeclaration baseProperty = FindProperty(type.BaseType, name, parameters);
                if (baseProperty != null)
                {
                    return (baseProperty);
                }
            }

            // Still not, try now the interfaces
            foreach (ITypeInfo oneInterface in type.UnionOfInterfaces)
            {
                if (object.ReferenceEquals(type, oneInterface))
                {
                    // Don't search recursively for itself
                    continue;
                }
                // Potentially here we might parse several time the same interfaces
                // if interfaces are deriving from other interfaces
                IPropertyDeclaration baseProperty = FindProperty(oneInterface, name, parameters);
                if (baseProperty != null)
                {
                    return (baseProperty);
                }
            }

            return (null);
        }

        public static ITypeInfo FindInterfaceDefiningProperty(ITypeInfo type, string name, IParameterDeclarationCollection parameters)
        {
            foreach (IPropertyDeclaration property in type.TypeDeclaration.Properties)
            {
                if (property.Name != name)
                {
                    continue;
                }
                if (Util.CompareParameterDeclaration(property.Parameters, parameters, false) == false)
                {
                    continue;
                }
                return (type);
            }

            // Then each sub-interfaces
            foreach (ITypeInfo oneInterface in type.UnionOfInterfaces)
            {
                if (object.ReferenceEquals(type, oneInterface))
                {
                    // Already searched...
                    continue;
                }
                foreach (IPropertyDeclaration property in oneInterface.TypeDeclaration.Properties)
                {
                    if (property.Name != name)
                    {
                        continue;
                    }
                    if (Util.CompareParameterDeclaration(property.Parameters, parameters, false) == false)
                    {
                        continue;
                    }
                    return (oneInterface);
                }
            }

            return (null);
        }

        public static bool CanImplicitCast(ITypeInfo srcType, ITypeInfo dstType)
        {
            if (srcType.IsBaseType(dstType))
            {
                return (true);
            }

            // Look for the interfaces...
            if (dstType.Type != ObjectType.INTERFACE)
            {
                // The destination is not an interface, no implicit cast possible
                return (false);
            }

            foreach (ITypeInfo oneInterface in srcType.UnionOfInterfaces)
            {
                if (oneInterface == dstType)
                {
                    // The destination is one of the base interface
                    return (true);
                }
            }
            // Not one of the base interface...
            return (false);
        }

        // It was using ITypeInfo before, now it is using LocalType...
        // The main reason is that the conversion table use all the predefined types that might not be resolved yet (EmbeddedType == null).
        // That happens for example if the assembly doesn't use the type Decimal for example.
        // So instead of having to handle unfinished type / structure (which is never good), we are using LocalType which is always defined
        // correctly.
        // The nice side effect is that LocalType can handle correctly pointers and reference with unsafe code, in case we need this later
        // ITypeInfo can just handle straight type (safe code).

        public static IMethodDeclaration FindConversion(LocalType sourceTypeInfo, LocalType destinationTypeInfo)
        {
            if (destinationTypeInfo.IsPrimitiveType == false)
            {
                // The destination type is not even a predefined type, as such, there is no known conversion...
                return (FindNonStandardConversion(sourceTypeInfo, destinationTypeInfo));
            }

            IList<LocalType> conversionList = GetConversionList(destinationTypeInfo);
            if (conversionList != null)
            {
                return (FindConversion(sourceTypeInfo, conversionList));
            }
            return (null);
        }

        private static IMethodDeclaration FindNonStandardConversion(LocalType sourceType, LocalType destinationType)
        {
            ITypeInfo sourceTypeInfo = sourceType.GetTypeInfo();
            ITypeInfo destinationTypeInfo = destinationType.GetTypeInfo();

            // Parse each function for each possible conversion...
            // TODO: Add a special collection to have just the implicit and explicit conversion method...
            //          If we were using a dictionary, we would just have 1 loop instead of 2 loops...
            //          Also the look up will be instantaneous and we sould not have to look at _all_ the methods
            //          It would be between 10 to 100 times faster...
            if ((sourceTypeInfo != null) && (sourceTypeInfo.TypeDeclaration != null))
            {
                foreach (IMethodDeclaration method in sourceTypeInfo.TypeDeclaration.Methods)
                {
                    if ((method.Name != "op_Implicit") && (method.Name != "op_Explicit"))
                    {
                        continue;
                    }

                    // That's an implicit or explicit conversion, see if it matches the return type...
                    if (Util.CompareType(method.ReturnType.Type, destinationType.EmbeddedType) == false)
                    {
                        continue;
                    }
                    return (method);
                }
            }

            if ((destinationTypeInfo != null) && (destinationTypeInfo.TypeDeclaration != null))
            {
                foreach (IMethodDeclaration method in destinationTypeInfo.TypeDeclaration.Methods)
                {
                    if ((method.Name != "op_Implicit") && (method.Name != "op_Explicit"))
                    {
                        continue;
                    }

                    // That's an implicit or explicit conversion, see if it matches the return type...
                    if (Util.CompareType(method.ReturnType.Type, destinationType.EmbeddedType) == false)
                    {
                        continue;
                    }
                    return (method);
                }
            }

            // Didn't find it, try the base implementations
            if ((sourceTypeInfo != null) && (sourceTypeInfo.BaseType != null))
            {
                IMethodDeclaration baseMethod = FindConversion(sourceTypeInfo.BaseType.LocalType, destinationType);
                if (baseMethod != null)
                {
                    return (baseMethod);
                }
            }

            /*
             * Currently we don't try the interfaces...
             * TODO: Add implicit / explicit conversion for interface
             * 
                        // Still not, try now the interfaces
                        foreach (ITypeInfo oneInterface in type.UnionOfInterfaces)
                        {
                            if (object.ReferenceEquals(type, oneInterface))
                            {
                                // Don't search recursively for itself...
                                continue;
                            }

                            // Potentially here we might parse several time the same interfaces
                            // if interfaces are deriving from other interfaces
                            IMethodDeclaration baseMethod = FindMethod(oneInterface, name, parameters, returnType, nonExactComparison);
                            if (baseMethod != null)
                            {
                                return (baseMethod);
                            }
                        }
             */
            return (null);
        }

        private static IMethodDeclaration FindConversion(LocalType sourceType, IList<LocalType> conversionList)
        {
            // There can be only one conversion operator (it is either implicit or explicit,
            //  can't have two operators, one implict and one explicit...

            // Parse each conversion type with the provided order...
            ITypeInfo sourceTypeInfo = sourceType.GetTypeInfo();
            foreach (LocalType returnType in conversionList)
            {
                // Parse each function for each possible conversion...
                // TODO: Add a special collection to have just the implicit and explicit conversion method...
                //          If we were using a dictionary, we would just have 1 loop instead of 2 loops...
                //          Also the look up will be instantaneous and we sould not have to look at _all_ the methods
                //          It would be between 10 to 100 times faster...
                if ((sourceTypeInfo != null) && (sourceTypeInfo.TypeDeclaration != null))
                {
                    foreach (IMethodDeclaration method in sourceTypeInfo.TypeDeclaration.Methods)
                    {
                        if ((method.Name != "op_Implicit") && (method.Name != "op_Explicit"))
                        {
                            continue;
                        }

                        // That's an implicit or explicit conversion, see if it matches the return type...
                        if (Util.CompareType(method.ReturnType.Type, returnType.EmbeddedType) == false)
                        {
                            continue;
                        }
                        return (method);
                    }
                }
            }

            // Didn't find it, try the base implementations
            if ((sourceTypeInfo != null) && (sourceTypeInfo.BaseType != null))
            {
                IMethodDeclaration baseMethod = FindConversion(sourceTypeInfo.BaseType.LocalType, conversionList);
                if (baseMethod != null)
                {
                    return (baseMethod);
                }
            }

/*
 * Currently we don't try the interfaces...
 * TODO: Add implicit / explicit conversion for interface
 * 
            // Still not, try now the interfaces
            foreach (ITypeInfo oneInterface in type.UnionOfInterfaces)
            {
                if (object.ReferenceEquals(type, oneInterface))
                {
                    // Don't search recursively for itself...
                    continue;
                }

                // Potentially here we might parse several time the same interfaces
                // if interfaces are deriving from other interfaces
                IMethodDeclaration baseMethod = FindMethod(oneInterface, name, parameters, returnType, nonExactComparison);
                if (baseMethod != null)
                {
                    return (baseMethod);
                }
            }
 */
            return (null);
        }

        private static IList<LocalType> GetConversionList(LocalType destinationType)
        {
            if (sConversionTable != null)
            {
                IList<LocalType> list;
                sConversionTable.TryGetValue(destinationType, out list);
                if (list != null)
                {
                    return (list);
                }
                // The only case where we don't find the table is for enum...
                Debug.Assert(destinationType.GetTypeInfo().Type == ObjectType.ENUM);
                // For those types, create the list on the fly...
                // Just enable conversion to it's own type, we might want to manage Int32 for example...
                // TODO:    Revisit other conversion for enum...
                list = AddToConversionTable(destinationType);
                return (list);
            }

            // It has not been create yet, so go ahead and create it...
            sConversionTable = new Dictionary<LocalType, IList<LocalType>>();

            // First gather all the possible types so we don't access them all over the place
            ILocalTypeManager localTypeManager = LanguageManager.LocalTypeManager;
            LocalType tBoolean = localTypeManager.TypeBool;
            LocalType tByte = localTypeManager.TypeByte;
            LocalType tSByte = localTypeManager.TypeSByte;
            LocalType tInt16 = localTypeManager.TypeInt16;
            LocalType tUInt16 = localTypeManager.TypeUInt16;
            LocalType tChar = localTypeManager.TypeChar;
            LocalType tInt32 = localTypeManager.TypeInt32;
            LocalType tUInt32 = localTypeManager.TypeUInt32;
            LocalType tInt64 = localTypeManager.TypeInt64;
            LocalType tUInt64 = localTypeManager.TypeUInt64;
            LocalType tSingle = localTypeManager.TypeSingle;
            LocalType tDouble = localTypeManager.TypeDouble;
            LocalType tDecimal = localTypeManager.TypeDecimal;

            // Add all the possible conversion between a type toward a destination type (first parameter)
            // The types are also in order, so the 2nd parameter will be tested first before we test the third parameter.
            // Look at the "Type Conversion Table" in the C# documentation

            // Currently the order is from the closest type to the most different type... 
            // We add all the widening conversions that does and does not cause a loss of precision...
            // Looking at Mono tests, widening with loss of precision for single and double are before the other types...

            // When converting to floating, floating sources are first listed before integer numbers
            //  This might not be correct...

            // TODO:    Test that the order is correct...

            AddToConversionTable(tBoolean);
            AddToConversionTable(tByte);
            AddToConversionTable(tSByte);
            AddToConversionTable(tInt16, tSByte, tByte);
            AddToConversionTable(tUInt16, tChar, tByte);
            AddToConversionTable(tChar);
            AddToConversionTable(tInt32, tInt16, tUInt16, tChar, tSByte, tByte);
            AddToConversionTable(tUInt32, tUInt16, tChar, tByte);
            AddToConversionTable(tInt64, tInt32, tUInt32, tInt16, tUInt16, tChar, tSByte, tByte);
            AddToConversionTable(tUInt64, tUInt32, tUInt16, tChar, tByte);
            AddToConversionTable(tSingle, tDecimal, tInt64, tUInt64, tInt32, tUInt32, tInt16, tUInt16, tChar, tByte, tSByte);
            AddToConversionTable(tDouble, tDecimal, tSingle, tInt64, tUInt64, tInt32, tUInt32, tInt16, tUInt16, tChar, tSByte, tByte);
            AddToConversionTable(tDecimal, tDouble, tSingle, tInt64, tUInt64, tInt32, tUInt32, tInt16, tUInt16, tChar, tSByte, tByte);

            IList<LocalType> conversionList;
            sConversionTable.TryGetValue(destinationType, out conversionList);
            return (conversionList);
        }

        private static IList<LocalType> AddToConversionTable(LocalType localType, params LocalType[] listOfConversions)
        {
            IList<LocalType> table = new List<LocalType>();
            // In any case add the type we are looking for in first priority (as this is the obvious conversion)
            table.Add(localType);

            // Then add all the other conversions...
            foreach (LocalType conversion in listOfConversions)
            {
                table.Add(conversion);
            }

            // Once the list is done, add it to the global conversion table...
            sConversionTable.Add(localType, table);
            return (table);
        }

        private static IDictionary<LocalType, IList<LocalType>> sConversionTable = null;
    }
}
