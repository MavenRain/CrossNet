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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Reflector;
using Reflector.CodeModel;

using CrossNet.Common;
using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.CppRuntime
{
    public class CppLocalTypeManager : ILocalTypeManager
    {
        public LocalType GetLocalType(IType type)
        {
            LocalType localType;
            IType resolvedType;
            ITypeReference typeReference = Util.GetCorrespondingTypeReference(type, out resolvedType);
            if (typeReference != null)
            {
                type = typeReference;
            }
            bool found = mAllLocalTypes.TryGetValue(type, out localType);
            if (found)
            {
                return (localType);
            }

            mUnusedParsingInfo.DebugTypeFullName = type.ToString();
            string typeName = LanguageManager.ReferenceGenerator.GenerateCodeTypeAsString(type, mUnusedParsingInfo);
            foreach (LocalType onePredefinedType in UnresolvedPredefinedTypes)
            {
                if (onePredefinedType.ToString() == typeName)
                {
                    localType = onePredefinedType;
                    break;
                }
            }

            if (localType == null)
            {
                // Not predefined, create it...
                localType = new LocalType(typeName, type);
                mAllLocalTypes[type] = localType;
            }
            else
            {
                // Add it first, so when refreshing EmbeddedType it's already there
                mAllLocalTypes[type] = localType;
                // Resolve the EmbeddedType
                localType.EmbeddedType = type;
                // Now it is resolved, remove from the list (so the search will be faster next time)
                UnresolvedPredefinedTypes.Remove(localType);
            }
            return (localType);
        }

        public LocalType GetLocalType(IType type, string typeName)
        {
            LocalType localType;
            IType resolvedType;
            ITypeReference typeReference = Util.GetCorrespondingTypeReference(type, out resolvedType);
            if (typeReference != null)
            {
                type = typeReference;
            }
            bool found = mAllLocalTypes.TryGetValue(type, out localType);
            if (found)
            {
                return (localType);
            }

            foreach (LocalType onePredefinedType in UnresolvedPredefinedTypes)
            {
                if (onePredefinedType.ToString() == typeName)
                {
                    localType = onePredefinedType;
                    break;
                }
            }

            if (localType == null)
            {
                // Not predefined, create it...
                localType = new LocalType(typeName, type);
                mAllLocalTypes[type] = localType;
            }
            else
            {
                // Add it first, so when refreshing EmbeddedType it's already there
                mAllLocalTypes[type] = localType;
                // Resolve the EmbeddedType
                localType.EmbeddedType = type;
                // Now it is resolved, remove from the list (so the search will be faster next time)
                UnresolvedPredefinedTypes.Remove(localType);
            }
            return (localType);
        }

        public string DoesNeedCast(LocalType destinationType, LocalType sourceType, string sourceValue)
        {
            string castNeeded = DoesNeedCast(destinationType, sourceType);
            if (castNeeded == "")
            {
                return (sourceValue);
            }
            castNeeded += "(" + sourceValue + ")";
            return (castNeeded);
        }

        public StringData DoesNeedCast(LocalType destinationType, LocalType sourceType, StringData sourceValue)
        {
            string castNeeded = DoesNeedCast(destinationType, sourceType);
            if (castNeeded == "")
            {
                return (sourceValue);
            }
            StringData data = new StringData(castNeeded);
            data.AppendSameLine("(");
            data.AppendSameLine(sourceValue);
            data.AppendSameLine(")");
            return (data);
        }

        public string DoesNeedCast(LocalType destinationType, LocalType sourceType)
        {
            if (destinationType == null)
            {
                Debug.Fail("Should not be here!");
                return ("");
            }
            if (sourceType == null)
            {
                Debug.Fail("Should not be here!");
                return ("");
            }

            if (destinationType.Same(sourceType))
            {
                // Same type, no cast needed...
                return ("");
            }

            // In some case, Same() won't return true even if conceptually the types are the same
            // This is mainly due to the fact that one type can represent the declaration,
            // where the other represents the usage...
            // At the end the strings can be the same though...
            // TODO: Improve this...

/*
            if (sourceType.EmbeddedType is IReferenceType)
            {
                IType refType = ((IReferenceType)sourceType.EmbeddedType).ElementType;
                sourceType = GetLocalType(refType);
            }
            if (destinationType.EmbeddedType is IReferenceType)
            {
                IType refType = ((IReferenceType)destinationType.EmbeddedType).ElementType;
                destinationType = GetLocalType(refType);
            }
 */

            // The types are not the same! Do we have to fix?
            string typeToCast = destinationType.ToString();

            // Check first simple cast from base type to base type
            if (TypeInt32.Same(sourceType))
            {
                if (TypeChar.Same(destinationType))
                {
                    // Force the cast from int to char
                    // TODO: The best would be to convert the value ;)
                    return ("(::System::Char)");
                }
                else if (TypeUInt16.Same(destinationType))
                {
                    return ("(::System::UInt16)");
                }
                else if (TypeInt16.Same(destinationType))
                {
                    return ("(::System::Int16)");
                }
            }
            else if (TypeNull.Same(sourceType))
            {
                if (destinationType.FullName.StartsWith("::System::Nullable__G1<"))
                {
                    // Specific case, we push NULL on a nullable type...
                    // What we want instead is to push a default value...
                    return (typeToCast + "::CreateDefault");
                }

                // If the source type is null, it means we are pushing a pointer
                // Do an unsafe cast... Otherwize if we put simply NULL, we could have 0 interpreted instead...
                if (typeToCast.EndsWith("*") == false)
                {
                    // This is something we should not have to do, but there is something wrong somewhere with some pointer based code...
                    // TODO: Investigate this more...
                    // In the meantime, add the pointer level if the first level is missing
                    typeToCast += " *";
                }
                // In that case do a plain static_cast
                // I tried to use a templated ::CrossNetRuntime::NullCast like the other casts, but there were two issues:
                //  - The NULL passed as parameter was actually converted as integer thus crating some issues
                //  - We can't have a template with no parameter returning NULL, as this method expect to always pass the destination between ()
                // The only solution was to have this template take 1 parameter that at the end we didn't care...
                // All of this to do a simple static_cast, so directly the static_cast instead...
                return ("static_cast<" + typeToCast + " >");
            }

            // Then check more complex types...
            // This method could be really simplified by determining the major cases
            // I.e. if destination is an interface, do an interface cast...
            // But the reason we are still doing separating each case, sub-case one by one,
            // is for maintainability...
            // By having each clearly separated out, we reduce the risk of incorrect behavior in unrelated cases
            // Also it becomes much simpler to see each case and modify them accordingly
            {
                IType resolvedTypeSrc;
                ITypeReference typeReferenceSrc = Util.GetCorrespondingTypeReference(sourceType.EmbeddedType, out resolvedTypeSrc);
                ITypeInfo typeInfoSrc = null;

                IType resolvedTypeDst;
                ITypeReference typeReferenceDst = Util.GetCorrespondingTypeReference(destinationType.EmbeddedType, out resolvedTypeDst);
                ITypeInfo typeInfoDst = null;

                if (typeReferenceSrc != null)
                {
                    typeInfoSrc = TypeInfoManager.GetTypeInfo(typeReferenceSrc);
                }

                if (typeReferenceDst != null)
                {
                    typeInfoDst = TypeInfoManager.GetTypeInfo(typeReferenceDst);
                }

                if ((typeInfoSrc != null) || (typeInfoDst != null))
                {
                    // We recognized the type, so there could be some operator...
                    // Try to see if we can find an implicit operator to the destination type

                    IMethodDeclaration method;
                    method = Util.FindConversion(sourceType, destinationType);
                    if (method != null)
                    {
                        string methodName = LanguageManager.NameFixup.GetConversionMethodName(method);
                        ITypeInfo methodTypeInfo = TypeInfoManager.GetTypeInfo(method.DeclaringType);
                        return (methodTypeInfo.FullName + "::" + methodName);
                    }
                }

                if ((typeInfoSrc == null) || (typeInfoDst == null))
                {
                    if (typeInfoDst != null)
                    {
                        if (typeInfoDst.Type == ObjectType.INTERFACE)
                        {
                            return ("::CrossNetRuntime::InterfaceCast<" + typeToCast + " >");
                        }
                        else if (typeInfoDst.Type == ObjectType.DELEGATE)
                        {
                            // For the moment, don't cast to delegates
                            // TODO: Improve this...
                            return ("");
                        }
                        else if ((typeInfoDst.Type == ObjectType.STRUCT) && (sourceType.EmbeddedType is IPointerType))
                        {
                            // This can happen when casting from int * to int for example... 
                            return ("::CrossNetRuntime::ReinterpretCast<" + typeToCast + " >");
                        }
                    }

                    if (sourceType.EmbeddedType is IGenericArgument)
                    {
                        // The source is a generic, it can either be a class, a struct or a base type
                        // There is no way to know ahead. So the cast depends of the destination...
                        if (typeInfoDst != null)
                        {
                            switch (typeInfoDst.Type)
                            {
                                case ObjectType.DELEGATE:
                                case ObjectType.CLASS:
                                    /*
                                        if (sourceType.IsBaseType)
                                        {
                                            typeToCast = "::CrossNetRuntime::BaseTypeWrapper<" + typeToCast + " >";
                                        }
                                     */
                                    return ("::CrossNetRuntime::Box<" + typeToCast + " >");

                                case ObjectType.INTERFACE:
                                    return ("::CrossNetRuntime::InterfaceCast<" + typeToCast + " >");

                                case ObjectType.ENUM:
                                case ObjectType.STRUCT:
                                    // Unbox, but in some case we might prefer actually an unsafe cast
                                    // TODO: Add support in ::CrossNetRuntime::Unbox for unsafe_cast
                                    if (destinationType.IsPrimitiveType)
                                    {
                                        // In that case, make sure we use the correct type...
                                        typeToCast = "::CrossNetRuntime::BaseTypeWrapper<" + typeToCast + " >::BoxeableType ";
                                    }
                                    return ("::CrossnetRuntime::Unbox<" + typeToCast + " >");
                            }
                        }
                        else
                        {
                            // We don't even know the destination type
                            // There is not much we can do...
                            // We do an unsafe cast in case we can work out this issues
                        }
                        return ("::CrossNetRuntime::UnsafeCast<" + typeToCast + " >");
                    }
                    else if (destinationType.EmbeddedType is IGenericArgument)
                    {
                        // The destination is a generic, it can either be a class, a struct or a base type
                        // There is no way to know ahead. So the cast depends of the source...
                        if (typeInfoSrc != null)
                        {
                            switch (typeInfoSrc.Type)
                            {
                                case ObjectType.DELEGATE:
                                case ObjectType.CLASS:
                                    typeToCast = "::CrossNetRuntime::BaseTypeWrapper<" + typeToCast + " >::BoxeableType ";
                                    return ("::CrossNetRuntime::Unbox<" + typeToCast + " >");

                                case ObjectType.INTERFACE:
                                    return ("::CrossNetRuntime::InterfaceCast<" + typeToCast + " >");

                                case ObjectType.ENUM:
                                case ObjectType.STRUCT:
                                    // Unbox, but in some case we might prefer actually an unsafe cast
                                    // TODO: Add support in ::CrossNetRuntime::Unbox for unsafe_cast
                                    if (sourceType.IsPrimitiveType)
                                    {
                                        // In that case, make sure we use the correct type...
                                        typeToCast = "::CrossNetRuntime::BaseTypeWrapper<" + typeToCast + " >::BoxeableType ";
                                    }
                                    return ("::CrossNetRuntime::Unbox<" + typeToCast + " >");
                            }
                        }
                        else
                        {
                            // We don't even know the source type
                            // There is not much we can do...
                            // We do an unsafe cast in case we can work out this issues
                        }
                        return ("::CrossNetRuntime::UnsafeCast<" + typeToCast + " >");
                    }

                    {
                        if (TypeArray.Same(sourceType))
                        {
                            // For the moment, don't cast from arrays
                            // TODO: Improve this...
                            return ("");
                        }
                        if (destinationType.FullName.StartsWith("::System::Array__G<"))
                        {
                            // Handle some cases where we are casting from System::Object to System::Array__G...
                            if (TypeNull.Same(sourceType))
                            {
                                // Special case, if the source is NULL, we do no cast...
                                // We don't want an unsafe cast with Int32 as type...
                                return ("");
                            }
                            // We do an unsafe cast in case we can work out this issues
                            if (typeToCast.EndsWith("&"))
                            {
                                // Special case for array reference...
                                // This can happen when arrays are pass as ref or out parameter
                                typeToCast = typeToCast.Substring(0, typeToCast.Length - 1) + "*";
                            }
                            return ("::CrossNetRuntime::UnsafeCast<" + typeToCast + " >");
                        }
                        else if (destinationType.EmbeddedType is IPointerType)
                        {
                            // This can happen when casting from int to int * for example... 
                            return ("::CrossNetRuntime::ReinterpretCast<" + typeToCast + " >");
                        }
                    }
                    return ("");
                }
                else
                {
                    // We found both types directly
                    bool valueTypeSrc = typeInfoSrc.IsValueType;
                    bool valueTypeDst = typeInfoDst.IsValueType;

                    if (valueTypeSrc && valueTypeDst)
                    {
                        if (typeInfoDst.Type == ObjectType.ENUM)
                        {
                            // struct to enum -> enum cast
                            // This is a special case, mostly due to enum arithmetics with integer
                            // (actually replaced by System::Int32 structure).
                            return ("::CrossNetRuntime::EnumCast<" + typeToCast + " >");
                        }
                        else
                        {
                            // struct to struct -> unsafe cast
                            return ("::CrossNetRuntime::UnsafeCast<" + typeToCast + " >");
                        }
                    }
                    else if ((valueTypeSrc | valueTypeDst) == false)
                    {
                        if (typeInfoDst.Type == ObjectType.INTERFACE)
                        {
                            // object to interface -> interface cast
                            // interface to interface -> interface cast is also supported
                            return ("::CrossNetRuntime::InterfaceCast<" + typeToCast + " >");
                        }

                        if ((typeInfoDst.Type == ObjectType.CLASS) && (typeInfoSrc.Type == ObjectType.INTERFACE))
                        {
                            // We convert from a class to an interface
#if DISABLED    // Assert deactivated for the moment until a pass is done on the cast code...
                            Debug.Assert(typeInfoDst.FullName == "::System::Object");   // The class should actually be an object...
                                                                                        // If it was not an object, we would have another cast first
                                                                                        // Interface can only implicitly convert to an object
#endif
                            return ("::CrossNetRuntime::InterfaceCast<" + typeToCast + " >");
                        }

                        // object to object -> standard cast
                        // There is no need to cast if the destination type is base type for the source type
//                        if (typeInfoSrc.IsBaseType(typeInfoDst) == false)
                        {
                            // Dst is not a base type, we have to cast...
                            return ("::CrossNetRuntime::Cast<" + typeToCast + " >");
                        }
                    }
                    else if (valueTypeSrc && (valueTypeDst == false))
                    {
                        // struct to object -> Box
                        /*
                            if (sourceType.IsBaseType)
                            {
                                typeToCast = "::CrossNetRuntime::BaseTypeWrapper<" + typeToCast + " >::BoxeableType ";
                            }
                         */
                        if (typeInfoSrc.Type == ObjectType.ENUM)
                        {
                            // Box but with a special case... The reason is that even if it is an enum
                            // The value passed is an integer...
                            // So when we try to unbox the value, it won't work as we are going to try to cast an int to an enum
                            // Pass the specific type for the source...

                            // Because of this specific case, this might not work with generic code...
                            // We'll have to come up with an unit-test
                            return ("::CrossNetRuntime::BoxEnum<" + typeToCast + ", " + typeInfoSrc.FullName + " >");

                        }
                        return ("::CrossNetRuntime::Box<" + typeToCast + " >");
                    }
                    else
                    {
                        Debug.Assert(valueTypeDst);
                        Debug.Assert(valueTypeSrc == false);

                        // object to struct -> Unbox
                        if (destinationType.IsPrimitiveType)
                        {
                            typeToCast = "::CrossNetRuntime::BaseTypeWrapper<" + typeToCast + " >::BoxeableType ";
                        }
                        return ("::CrossNetRuntime::Unbox<" + typeToCast + " >");
                    }
                }
            }

            // No cast needed...
            return ("");
        }

        internal class TypeFake : IType
        {
            public int CompareTo(object obj)
            {
                if (object.ReferenceEquals(this, obj))
                {
                    // Same reference, same object...
                    return (0);
                }
                // Otherwise always assume it is a different object...
                return (1);
            }
        }

        class TypeEqualityComparer : IEqualityComparer<IType>
        {
            public bool Equals(IType x, IType y)
            {
                return (Util.CompareType(x, y));
            }

            public int GetHashCode(IType obj)
            {
                return (obj.GetHashCode());
            }
        }

        public CppLocalTypeManager()
        {
            mUnusedParsingInfo = new ParsingInfo(null);
            mAllLocalTypes = new Dictionary<IType, LocalType>(new TypeEqualityComparer());

            mTypeUInt32 = new LocalType(typeof(UInt32), "::System::UInt32", true);
            mTypeUInt16 = new LocalType(typeof(UInt16), "::System::UInt16", true);
            mTypeInt16 = new LocalType(typeof(Int16), "::System::Int16", true);
            mTypeInt64 = new LocalType(typeof(Int64), "::System::Int64", true);
            mTypeUInt64 = new LocalType(typeof(UInt64), "::System::UInt64", true);
            mTypeByte = new LocalType(typeof(Byte), "::System::Byte", true);
            mTypeSByte = new LocalType(typeof(SByte), "::System::SByte", true);
            mTypeBool = new LocalType(typeof(bool), "::System::Boolean", true);
            mTypeSingle = new LocalType(typeof(Single), "::System::Single", true);
            mTypeDouble = new LocalType(typeof(Double), "::System::Double", true);
            mTypeDecimal = new LocalType(typeof(Decimal), "::System::Decimal", true);
            mTypeChar = new LocalType(typeof(char), "::System::Char", true);
            mTypeInt32 = new LocalType(typeof(Int32), "::System::Int32", true);
            mTypeString = new LocalType(typeof(String), "::System::String");
            mTypeCharPointer = new LocalType(null, "::System::Char *");
            mTypeObject = new LocalType(typeof(object), "::System::Object");

            TypeFake typeFake = new TypeFake();

            mTypeNull = new LocalType("!ERROR-NULL!", typeFake);
            mTypeDefault = new LocalType("!ERROR-NULL-DEFAULT!", typeFake);
            mTypeUnknown = new LocalType("!ERROR-UNKNOWN!", typeFake);

            mTypeArray = new LocalType("!ERROR-ARRAY!", typeFake);

            mTypePointer = new LocalType("!ERROR-POINTER!", typeFake);
            mTypeOfType = new LocalType("!ERROR-TYPE!", typeFake);
            mTypeDelegate = new LocalType(typeof(Delegate), "::System::Delegate");
            mTypeIEnumerable = new LocalType(typeof(IEnumerable), "::System::Collections::IEnumerable");
            mTypeIEnumerator = new LocalType(typeof(IEnumerator), "::System::Collections::IEnumerator");
            mTypeIDisposable = new LocalType(typeof(IDisposable), "::System::IDisposable");
            mTypeVoid = new LocalType(typeof(void), "::System::Void");
            mTypeNullable = new LocalType(typeof(Nullable<>), "::System::Nullable__G1");

            PredefinedTypes = new List<LocalType>();
            PredefinedTypes.AddRange(new LocalType[] {
                                                        TypeUInt32,
                                                        TypeUInt16,
                                                        TypeInt16,
                                                        TypeInt64,
                                                        TypeUInt64,
                                                        TypeByte,
                                                        TypeSByte,
                                                        TypeBool,
                                                        TypeSingle,
                                                        TypeDouble,
                                                        TypeDecimal,
                                                        TypeChar,
                                                        TypeInt32,
                                                        TypeString,
                                                        TypeCharPointer,
                                                        TypeObject,
/*
 * Don't add the types that will never be resolved on purpose
 * 
                                                        TypeNull,
                                                        TypeNullDefault,
                                                        TypeArray,
                                                        TypeUnknown,
                                                        TypePointer,
                                                        TypeOfType,
 */
                                                        TypeDelegate,
                                                        TypeIEnumerable,
                                                        TypeIEnumerator,
                                                        TypeIDisposable,
                                                        TypeVoid,
                                                        TypeNullable,
                                                    });

            UnresolvedPredefinedTypes = new List<LocalType>(PredefinedTypes);
        }

        public LocalType TypeUInt32
        {
            get
            {
                return (mTypeUInt32);
            }
        }

        public LocalType TypeUInt16
        {
            get
            {
                return (mTypeUInt16);
            }
        }

        public LocalType TypeInt16
        {
            get
            {
                return (mTypeInt16);
            }
        }

        public LocalType TypeInt64
        {
            get
            {
                return (mTypeInt64);
            }
        }

        public LocalType TypeUInt64
        {
            get
            {
                return (mTypeUInt64);
            }
        }

        public LocalType TypeByte
        {
            get
            {
                return (mTypeByte);
            }
        }

        public LocalType TypeSByte
        {
            get
            {
                return (mTypeSByte);
            }
        }

        public LocalType TypeBool
        {
            get
            {
                return (mTypeBool);
            }
        }

        public LocalType TypeSingle
        {
            get
            {
                return (mTypeSingle);
            }
        }

        public LocalType TypeDouble
        {
            get
            {
                return (mTypeDouble);
            }
        }

        public LocalType TypeDecimal
        {
            get
            {
                return (mTypeDecimal);
            }
        }

        public LocalType TypeChar
        {
            get
            {
                return (mTypeChar);
            }
        }

        public LocalType TypeInt32
        {
            get
            {
                return (mTypeInt32);
            }
        }

        public LocalType TypeString
        {
            get
            {
                return (mTypeString);
            }
        }

        public LocalType TypeCharPointer
        {
            get
            {
                return (mTypeCharPointer);
            }
        }

        public LocalType TypeObject
        {
            get
            {
                return (mTypeObject);
            }
        }

        public LocalType TypeNull
        {
            get
            {
                return (mTypeNull);
            }
        }

        public LocalType TypeDefault
        {
            get
            {
                return (mTypeDefault);
            }
        }

        public LocalType TypeUnknown
        {
            get
            {
                return (mTypeUnknown);
            }
        }

        public LocalType TypeArray
        {
            get
            {
                return (mTypeArray);
            }
        }

        public LocalType TypePointer
        {
            get
            {
                return (mTypePointer);
            }
        }

        public LocalType TypeOfType
        {
            get
            {
                return (mTypeOfType);
            }
        }

        public LocalType TypeDelegate
        {
            get
            {
                return (mTypeDelegate);
            }
        }

        public LocalType TypeIEnumerable
        {
            get
            {
                return (mTypeIEnumerable);
            }
        }

        public LocalType TypeIEnumerator
        {
            get
            {
                return (mTypeIEnumerator);
            }
        }

        public LocalType TypeIDisposable
        {
            get
            {
                return (mTypeIDisposable);
            }
        }

        public LocalType TypeVoid
        {
            get
            {
                return (mTypeVoid);
            }
        }

        public LocalType TypeNullable
        {
            get
            {
                return (mTypeNullable);
            }
        }

        class NameComparer
        {
            NameComparer(string nameToCompare)
            {
                mNameToCompare = nameToCompare; 
            }

            private bool SameName(LocalType localType)
            {
                return (mNameToCompare == localType.FullName);
            }

            private string mNameToCompare;
        }

        private LocalType mTypeUInt32;
        private LocalType mTypeUInt16;
        private LocalType mTypeInt16;
        private LocalType mTypeInt64;
        private LocalType mTypeUInt64;
        private LocalType mTypeByte;
        private LocalType mTypeSByte;
        private LocalType mTypeBool;
        private LocalType mTypeSingle;
        private LocalType mTypeDouble;
        private LocalType mTypeDecimal;
        private LocalType mTypeChar;
        private LocalType mTypeInt32;
        private LocalType mTypeString;
        private LocalType mTypeCharPointer;
        private LocalType mTypeObject;
        private LocalType mTypeNull;
        private LocalType mTypeDefault;
        private LocalType mTypeUnknown;
        private LocalType mTypeArray;
        private LocalType mTypePointer;
        private LocalType mTypeOfType;
        private LocalType mTypeDelegate;
        private LocalType mTypeIEnumerable;
        private LocalType mTypeIEnumerator;
        private LocalType mTypeIDisposable;
        private LocalType mTypeVoid;
        private LocalType mTypeNullable;

        private readonly List<LocalType> PredefinedTypes;
        private List<LocalType> UnresolvedPredefinedTypes;
        private ParsingInfo mUnusedParsingInfo;
        private IDictionary<IType, LocalType> mAllLocalTypes;
    }
}
