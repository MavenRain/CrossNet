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

namespace CrossNet.CSharpRuntime
{
    public class CSharpLocalTypeManager : ILocalTypeManager
    {
        public LocalType GetLocalType(IType type)
        {
            LocalType localType;
            bool found = mAllLocalTypes.TryGetValue(type, out localType);
            if (found)
            {
                return (localType);
            }

            string typeName = LanguageManager.ReferenceGenerator.GenerateCodeType(type, mUnusedParsingInfo).Text;
            foreach (LocalType onePredefinedType in PredefinedTypes)
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
            }
            else
            {
                // Set the embedded type on the pre-defined local type
                if (object.ReferenceEquals(type, localType))
                {
                    // Should not be here, but it happens...
                    // TODO: Fix this...
                    return (localType);
                }
                if (localType.EmbeddedType == null)
                {
                    // Only if not set, otherwise we could point the localtype on itself in some cases...
                    localType.EmbeddedType = type;
                }
            }
            mAllLocalTypes[type] = localType;
            return (localType);
        }

        public LocalType GetLocalType(IType type, string typeName)
        {
            LocalType localType;
            bool found = mAllLocalTypes.TryGetValue(type, out localType);
            if (found)
            {
                return (localType);
            }

            foreach (LocalType onePredefinedType in PredefinedTypes)
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
            }
            else
            {
                // Set the embedded type on the pre-defined local type
                if (object.ReferenceEquals(type, localType))
                {
                    // Should not be here, but it happens...
                    // TODO: Fix this...
                    return (localType);
                }
                if (localType.EmbeddedType == null)
                {
                    // Only if not set, otherwise we could point the localtype on itself in some cases...
                    localType.EmbeddedType = type;
                }
            }
            mAllLocalTypes[type] = localType;
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
            Debug.Assert(destinationType != null);
            Debug.Assert(sourceType != null);

            if (destinationType.Same(sourceType) == false)
            {
                // The types are not the same! Do we have to fix?

                // Check first simple cast from base type to base type
                if (LanguageManager.LocalTypeManager.TypeInt32.Same(sourceType))
                {
                    if (LanguageManager.LocalTypeManager.TypeChar.Same(destinationType))
                    {
                        // Force the cast from int to char
                        // TODO: The best would be to convert the value ;)
                        return ("(System.Char)");
                    }
                    else if (LanguageManager.LocalTypeManager.TypeUInt16.Same(destinationType))
                    {
                        return ("(System.UInt16)");
                    }
                    else if (LanguageManager.LocalTypeManager.TypeInt16.Same(destinationType))
                    {
                        return ("(System.Int16)");
                    }
                }

                // Then check more complex types...
                {
                    ITypeReference typeReferenceSrc = sourceType.EmbeddedType as ITypeReference;
                    ITypeInfo typeInfoSrc = null;
                    ITypeReference typeReferenceDst = destinationType.EmbeddedType as ITypeReference;
                    ITypeInfo typeInfoDst = null;
                    string typeToCast = destinationType.ToString();

                    if (typeReferenceSrc != null)
                    {
                        typeInfoSrc = TypeInfoManager.GetTypeInfo(typeReferenceSrc);
                    }
                    if (typeReferenceDst != null)
                    {
                        typeInfoDst = TypeInfoManager.GetTypeInfo(typeReferenceDst);
                    }

                    if ((typeInfoDst == null) || (typeInfoSrc == null))
                    {
                        /*
                                                            // One type is not a simple type, just do an unsafe cast for the moment...
                                                            data = new StringData("(::CrossNetRuntime::UnsafeCast<");
                                                            data.AppendSameLine(typeToCast);
                                                            data.AppendSameLine(">");
                                                            casted = true;
                         */
                    }
                    else
                    {
                        // We found both types directly
                        bool valueTypeSrc = typeInfoSrc.IsValueType;
                        bool valueTypeDst = typeInfoDst.IsValueType;

                        if (valueTypeSrc && valueTypeDst)
                        {
                            // struct to struct -> unsafe cast
                            return ("crossnet_unsafecast<" + typeToCast + ">");
                        }
                        else if ((valueTypeSrc | valueTypeDst) == false)
                        {
                            // object to object -> standard cast
                            // There is no need to cast if the destination type is base type for the source type
                            if (typeInfoSrc.IsBaseType(typeInfoDst) == false)
                            {
                                // Dst is not a base type, we have to cast...

                                return ("crossnet_cast<" + typeToCast + ">");
                            }
                        }
                        else if (valueTypeSrc && (valueTypeDst == false))
                        {
                            // struct to object -> Box
                            return ("crossnet_box<" + typeToCast + ">");
                        }
                        else
                        {
                            Debug.Assert(valueTypeDst);
                            Debug.Assert(valueTypeSrc == false);

                            // object to struct -> Unbox
                            return ("crossnet_unbox<" + typeToCast + ">");
                        }
                    }
                }
            }

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

        public CSharpLocalTypeManager()
        {
            mUnusedParsingInfo = new ParsingInfo(null);
            mAllLocalTypes = new Dictionary<IType, LocalType>();

            mTypeUInt32 = new LocalType(typeof(UInt32), "System.UInt32");
            mTypeUInt16 = new LocalType(typeof(UInt16), "System.UInt16");
            mTypeInt16 = new LocalType(typeof(Int16), "System.Int16");
            mTypeInt64 = new LocalType(typeof(Int64), "System.Int64");
            mTypeUInt64 = new LocalType(typeof(UInt64), "System.UInt64");
            mTypeByte = new LocalType(typeof(byte), "System.Byte");
            mTypeSByte = new LocalType(typeof(sbyte), "System.SByte");
            mTypeBool = new LocalType(typeof(bool), "System.Boolean");
            mTypeSingle = new LocalType(typeof(float), "System.Float");
            mTypeDouble = new LocalType(typeof(double), "System.Double");
            mTypeDecimal = new LocalType(typeof(decimal), "System.Decimal");
            mTypeChar = new LocalType(typeof(char), "System.Char");
            mTypeInt32 = new LocalType(typeof(Int32), "System.Int32");
            mTypeString = new LocalType(typeof(string), "System.String");
            mTypeCharPointer = new LocalType(null, "System.Char *");
            mTypeObject = new LocalType(typeof(object), "System.Object");

            TypeFake typeFake = new TypeFake();

            mTypeNull = new LocalType("!ERROR-NULL!", typeFake);
            mTypeNullDefault = new LocalType("!ERROR-NULL-DEFAULT!", typeFake);
            mTypeUnknown = new LocalType("!ERROR-UNKNOWN!", typeFake);

            mTypeArray = new LocalType("!ERROR-ARRAY!", typeFake);

            mTypePointer = new LocalType("!ERROR-POINTER!", typeFake);
            mTypeOfType = new LocalType("!ERROR-TYPE!", typeFake);
            mTypeDelegate = new LocalType("!ERROR-DELEGATE!", typeFake);
            mTypeIEnumerable = new LocalType(typeof(IEnumerable), "System.Collections.IEnumerable");
            mTypeIEnumerator = new LocalType(typeof(IEnumerator), "System.Collections.IEnumerator");
            mTypeIDisposable = new LocalType(typeof(IDisposable), "System.IDisposable");
            mTypeVoid = new LocalType(typeof(void), "System.Void");
            mTypeNullable = new LocalType(typeof(Nullable<>), "System.Nullable");

            PredefinedTypes = new LocalType[]
            {
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

                TypeNull,
                TypeDefault,
                TypeArray,
                TypeUnknown,
                TypePointer,
                TypeOfType,
                TypeDelegate,
                TypeIEnumerable,
                TypeIEnumerator,
                TypeIDisposable,
                TypeVoid,
                TypeNullable,
            };
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
                return (mTypeNullDefault);
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
        private LocalType mTypeNullDefault;
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

        private readonly LocalType[] PredefinedTypes;
        private ParsingInfo mUnusedParsingInfo;
        private IDictionary<IType, LocalType> mAllLocalTypes;
    }
}
