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
using System.Text;

using Reflector;
using Reflector.CodeModel;
using Reflector.CodeModel.Memory;

using CrossNet.Common;
using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.CSharpRuntime
{
    public class CSharpTypeInfo : ITypeInfo
    {
        internal CSharpTypeInfo(ITypeReference typeReference, string fullName)
        {
            mName = typeReference.Name;
            mFullName = fullName;
        }

        public string Name
        {
            get
            {
                return (mName);
            }
        }
        public string FullName
        {
            get
            {
                return (mFullName);
            }
        }
        public string FullNameWithoutGeneric
        {
            get
            {
                throw new NotImplementedException();
            }
        }
        public string DotNetFullName
        {
            get
            {
                return (mFullName);
            }
        }
        public string NonScopedFullName
        {
            get
            {
                return (mFullName);
            }
        }
        public string GetWrapperName(string declaringType)
        {
            return ("Wrapper__" + Name);
        }

        public string Namespace
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ObjectType Type
        {
            get
            {
                return (mType);
            }
        }

        public ITypeInfo BaseType
        {
            get
            {
                return (mBaseType);
            }
        }

        public bool IsPrimitiveType
        {
            get
            {
                return (mLocalType.IsPrimitiveType);
            }
        }

        public IList<ITypeInfo> UnionOfInterfaces
        {
            get
            {
                return (mUnionOfInterfaces);
            }
        }
        public IList<ITypeInfo> ExclusiveInterfaces
        {
            get
            {
                return (mExclusiveInterfaces);
            }
        }
        public ITypeReference TypeReference
        {
            get
            {
                return (mTypeReference);
            }
        }
        public ITypeDeclaration TypeDeclaration
        {
            get
            {
                return (mTypeDeclaration);
            }
        }

        public bool IsValueType
        {
            get
            {
                switch (Type)
                {
                    case ObjectType.CLASS:
                    case ObjectType.INTERFACE:
                    case ObjectType.DELEGATE:
                        return (false);

                    default:
                    case ObjectType.STRUCT:
                    case ObjectType.ENUM:
                        return (true);
                }
            }
        }

        public string PointerToMember
        {
            get
            {
                return (".");
            }
        }

        public LocalType LocalType
        {
            get
            {
                if (mLocalType != null)
                {
                    return (mLocalType);
                }
                mLocalType = LanguageManager.LocalTypeManager.GetLocalType(TypeDeclaration);
                return (mLocalType);
            }
        }

        public bool IsGeneric
        {
            get
            {
                return (mTypeDeclaration.GenericArguments.Count != 0);
            }
        }

        public int NumGenericArguments
        {
            get
            {
                return (mTypeDeclaration.GenericArguments.Count);
            }
        }

        public bool IsFirstLevelGeneric
        {
            get
            {
                if (mTypeDeclaration.GenericArguments.Count == 0)
                {
                    // This level is even not a generic
                    return (false);
                }

                ITypeDeclaration owner = mTypeDeclaration.Owner as ITypeDeclaration;
                if (owner == null)
                {
                    // No owner, so this is indeed the first level
                    return (true);
                }
                // The owner must not be a generic either
                return (owner.GenericArguments.Count == 0);
            }
        }

        public int RemainingDependencies
        {
            get
            {
                // This is not needed for C#
                return (0);
            }
#if !STANDARD_DEP
            set
            {
                // Do nothing for C#
            }
#endif
        }

        public bool DependencyCleared
        {
            get
            {
                // Not needed for C#, dependencies are always cleared
                return (true);
            }
            set
            {
                // Not needed for C#
            }
        }

        public string GetFullName(ParsingInfo info)
        {
            return (mFullName);
        }

        public string GetInstanceText(ParsingInfo info)
        {
            return (mFullName);
        }

        public string GetInstancePostFix()
        {
            return ("");
        }

        public bool IsBaseType(ITypeInfo baseType)
        {
            // First we get rid of all the implicit types
            switch (Type)
            {
                case ObjectType.CLASS:
                case ObjectType.INTERFACE:
                    if (baseType.FullName == "System::Object")
                    {
                        return (true);
                    }
                    break;

                case ObjectType.DELEGATE:
                    if (baseType.FullName == "System::MulticastDelegate")
                    {
                        return (true);
                    }
                    if (baseType.FullName == "System::Object")
                    {
                        return (true);
                    }
                    break;

                case ObjectType.ENUM:
                    if (baseType.FullName == "System::Enum")
                    {
                        return (true);
                    }
                    break;

                case ObjectType.STRUCT:
                    if (baseType.FullName == "System::ValueType")
                    {
                        return (true);
                    }
                    break;
            }

            ITypeInfo current = this;
            while (current != null)
            {
                if (current.BaseType == baseType)
                {
                    return (true);
                }
                current = current.BaseType;
            }
            return (false);
        }

        public void FillInfo(ITypeReference typeReference)
        {
            if (TypeDeclaration != null)
            {
                return;
            }
            mTypeReference = typeReference;
            ITypeDeclaration typeDeclaration = typeReference as ITypeDeclaration;
            if (typeDeclaration == null)
            {
                typeDeclaration = typeReference.Resolve();
            }
            FillInfo(typeDeclaration);
        }

        public void FillInfo(ITypeDeclaration typeDeclaration)
        {
            if (TypeDeclaration != null)
            {
                // Already filled...
                return;
            }
            mTypeDeclaration = typeDeclaration;
            mTypeReference = typeDeclaration;

            // We have now to parse the various information

            // Type and baseType discovered
            string baseTypeFullName = "";
            mBaseType = null;
            if (typeDeclaration.BaseType != null)
            {
                baseTypeFullName = typeDeclaration.BaseType.Namespace + "::" + typeDeclaration.BaseType.Name;
                if (baseTypeFullName == "System::MulticastDelegate")
                {
                    mType = ObjectType.DELEGATE;
                }
                else if (baseTypeFullName == "System::ValueType")
                {
                    mType = ObjectType.STRUCT;
                }
                else if (baseTypeFullName == "System::Enum")
                {
                    mType = ObjectType.ENUM;
                }
                else if (baseTypeFullName == "System::Object")
                {
                    mType = ObjectType.CLASS;
                }
                else
                {
                    // Another class
                    mType = ObjectType.CLASS;

                    // In this case, we have to go resolve the parent type as well
                    mBaseType = TypeInfoManager.GetTypeInfo(typeDeclaration.BaseType);
                }
            }
            else
            {
                // No base type
                if (typeDeclaration.Interface)
                {
                    mType = ObjectType.INTERFACE;
                }
                else
                {
                    mType = ObjectType.CLASS;
                }
            }

            // Now find all the interfaces
            IDictionary<ITypeInfo, ITypeInfo> interfacesAlreadyIncluded = new Dictionary<ITypeInfo, ITypeInfo>();

            // Add unique interfaces from the base type
            if (BaseType != null)
            {
                foreach (ITypeInfo oneInterface in BaseType.UnionOfInterfaces)
                {
                    interfacesAlreadyIncluded[oneInterface] = oneInterface;
                }
            }

            // Add the interfaces from the passed interface as well
            IList<ITypeInfo> baseInterfaces = new List<ITypeInfo>(typeDeclaration.Interfaces.Count);
            foreach (ITypeReference baseInterface in typeDeclaration.Interfaces)
            {
                ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(baseInterface);
                // Don't add this interface, as that's the exclusive interfaces we want to detect
                // But add it to an associate list, so we don't have to search for them again
                baseInterfaces.Add(typeInfo);

                // And all its parent interfaces...
                // It is expected that there is a lot of overlap as typeDeclaration.interfaces contain all the interfaces
                foreach (ITypeInfo subInterface in typeInfo.UnionOfInterfaces)
                {
                    interfacesAlreadyIncluded[subInterface] = subInterface;
                }
            }

            // Now we can determine what interface is exclusive
            mExclusiveInterfaces = new List<ITypeInfo>();
            foreach (ITypeInfo baseInterface in baseInterfaces)
            {
                if (interfacesAlreadyIncluded.ContainsKey(baseInterface))
                {
                    // This interface is already part of the tree, skip it
                    continue;
                }
                ExclusiveInterfaces.Add(baseInterface);
            }

            // Now we do the union (that is with the baseInterfaces)
            mUnionOfInterfaces = new List<ITypeInfo>();
            foreach (ITypeInfo baseInterface in baseInterfaces)
            {
                interfacesAlreadyIncluded[baseInterface] = baseInterface;
            }
            foreach (ITypeInfo baseInterface in interfacesAlreadyIncluded.Keys)
            {
                UnionOfInterfaces.Add(baseInterface);
            }
        }

        public string GetPrefixedFullName(string prefix)
        {
            return (FullName + "::" + prefix + Name);
        }

        public void DetectDependencies()
        {
            // Not needed for C#
        }

        public bool RemoveSolvedDependencies()
        {
            // Not needed for C#, consider that good things always happened
            return (true);
        }

        public override int GetHashCode()
        {
            return (FullName.GetHashCode());
        }

        public override bool Equals(object obj)
        {
            ITypeInfo other = obj as ITypeInfo;
            if (other == null)
            {
                return (false);
            }
            return (FullName == other.FullName);
        }

        private string mName;
        private string mFullName;
//        private string mNamespace;
        private ObjectType mType;
        private ITypeInfo mBaseType;
        private IList<ITypeInfo> mUnionOfInterfaces;
        private IList<ITypeInfo> mExclusiveInterfaces;
        private ITypeReference mTypeReference;
        private ITypeDeclaration mTypeDeclaration;
        private LocalType mLocalType = null;
    }
}
