using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Reflector;
using Reflector.CodeModel;

using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.Common
{
    class LocalTypeManager
    {
        public static LocalType GetLocalType(IType type)
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

        public static LocalType GetLocalType(IType type, string typeName)
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

        public static string DoesNeedCast(LocalType destinationType, LocalType sourceType)
        {
            Debug.Assert(destinationType != null);
            Debug.Assert(sourceType != null);

            if (destinationType.Same(sourceType) == false)
            {
                // The types are not the same! Do we have to fix?

                if (LocalTypeManager.TypeChar.Same(destinationType)
                    && LocalTypeManager.TypeInt.Same(sourceType))
                {
                    // Force the cast from int to char
                    // TODO: The best would be to convert the value ;)
                    return ("(System::Char)");
                }
                else
                {
                    ITypeReference typeReferenceSrc = sourceType.EmbeddedType as ITypeReference;
                    TypeInfo typeInfoSrc = null;
                    ITypeReference typeReferenceDst = destinationType.EmbeddedType as ITypeReference;
                    TypeInfo typeInfoDst = null;
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
                                                            data = new StringData("(crossnet_unsafecast<");
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

        private static ParsingInfo mUnusedParsingInfo;
        private static IDictionary<IType, LocalType> mAllLocalTypes;

        public static readonly LocalType TypeUInt;
        public static readonly LocalType TypeUInt16;
        public static readonly LocalType TypeInt16;
        public static readonly LocalType TypeInt64;
        public static readonly LocalType TypeUInt64;
        public static readonly LocalType TypeByte;
        public static readonly LocalType TypeSByte;
        public static readonly LocalType TypeBool;
        public static readonly LocalType TypeFloat;
        public static readonly LocalType TypeDouble;
        public static readonly LocalType TypeChar;
        public static readonly LocalType TypeInt;
        public static readonly LocalType TypeString;
        public static readonly LocalType TypeCharPointer;
        public static readonly LocalType TypeObject;

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

        static LocalTypeManager()
        {
            mUnusedParsingInfo = new ParsingInfo(null);
            mAllLocalTypes = new Dictionary<IType, LocalType>();
/*
            TypeUInt = new LocalType("System.UInt32");
            TypeUInt16 = new LocalType("System.UInt16");
            TypeInt16 = new LocalType("System.Int16");
            TypeInt64 = new LocalType("System.Int64");
            TypeUInt64 = new LocalType("System.UInt64");
            TypeByte = new LocalType("System.Byte");
            TypeSByte = new LocalType("System.SByte");
            TypeBool = new LocalType("System.Boolean");
            TypeFloat = new LocalType("System.Float");
            TypeDouble = new LocalType("System.Double");
            TypeChar = new LocalType("System.Char");
            TypeInt = new LocalType("System.Int32");
            TypeString = new LocalType("System.String");
            TypeCharPointer = new LocalType("System.Char *");
*/
            TypeUInt = new LocalType("System::UInt32");
            TypeUInt16 = new LocalType("System::UInt16");
            TypeInt16 = new LocalType("System::Int16");
            TypeInt64 = new LocalType("System::Int64");
            TypeUInt64 = new LocalType("System::UInt64");
            TypeByte = new LocalType("System::Byte");
            TypeSByte = new LocalType("System::SByte");
            TypeBool = new LocalType("System::Boolean");
            TypeFloat = new LocalType("System::Float");
            TypeDouble = new LocalType("System::Double");
            TypeChar = new LocalType("System::Char");
            TypeInt = new LocalType("System::Int32");
            TypeString = new LocalType("System::String");
            TypeCharPointer = new LocalType("System::Char *");
            TypeObject = new LocalType("System::Object");

            TypeFake typeFake = new TypeFake();

            TypeNull = new LocalType("!ERROR-NULL!", typeFake);
            TypeUnknown = new LocalType("!ERROR-UNKNOWN!", typeFake);

            TypeArray = new LocalType("!ERROR-ARRAY!", typeFake);

            TypePointer = new LocalType("!ERROR-POINTER!", typeFake);
            TypeOfType = new LocalType("!ERROR-TYPE!", typeFake);
            TypeDelegate = new LocalType("!ERROR-DELEGATE!", typeFake);

            PredefinedTypes = new LocalType[]
            {
                TypeUInt,
                TypeUInt16,
                TypeInt16,
                TypeInt64,
                TypeUInt64,
                TypeByte,
                TypeSByte,
                TypeBool,
                TypeFloat,
                TypeDouble,
                TypeChar,
                TypeInt,
                TypeString,
                TypeCharPointer,
                TypeObject,

                TypeNull,
                TypeArray,
                TypeUnknown,
                TypePointer,
                TypeOfType,
                TypeDelegate,
            };
        }

        public static readonly LocalType TypeNull;
        public static readonly LocalType TypeUnknown;
        // TODO: Differentiate the different kind of arrays
        public static readonly LocalType TypeArray;
        // TODO: Differentiate the different kind of pointers
        public static readonly LocalType TypePointer;
        public static readonly LocalType TypeOfType;
        public static readonly LocalType TypeDelegate;

        public static readonly LocalType[] PredefinedTypes;
    }
}
