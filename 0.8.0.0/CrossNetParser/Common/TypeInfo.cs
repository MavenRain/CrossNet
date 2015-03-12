using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Reflector;
using Reflector.CodeModel;
using Reflector.CodeModel.Memory;

using CrossNet.Interfaces;

namespace CrossNet.Common
{
    public class TypeInfo
    {
        internal TypeInfo(string name, string fullName)
        {
            Name = name;
            FullName = fullName;
        }

        public string Name;
        public string FullName;
        public string Namespace;
        public ObjectType Type;

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

        public TypeInfo BaseType;
        public IList<TypeInfo> UnionOfInterfaces;
        public IList<TypeInfo> ExclusiveInterfaces;
        public ITypeReference TypeReference = null;
        public ITypeDeclaration TypeDeclaration = null;

        public string GetInstancePostFix()
        {
            switch (Type)
            {
                case ObjectType.CLASS:
                case ObjectType.INTERFACE:
                case ObjectType.DELEGATE:
                    // For class, interface, delegate we are using pointer on the instance
                    return (" *");

                default:
                case ObjectType.STRUCT:
                case ObjectType.ENUM:
                    // For struct and enum, this is directly the value...
                    return ("");
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
                mLocalType = LocalTypeManager.GetLocalType(TypeDeclaration);
                return (mLocalType);
            }
        }

        public bool CanImplicityCast(TypeInfo dstType)
        {
            if (IsBaseType(dstType))
            {
                return (true);
            }

            // Look for the interfaces...
            if (dstType.Type != ObjectType.INTERFACE)
            {
                // The destination is not an interface, no implicit cast possible
                return (false);
            }

            foreach (TypeInfo oneInterface in UnionOfInterfaces)
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

        public bool IsBaseType(TypeInfo baseType)
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

            TypeInfo current = this;
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

        internal void FillInfo(ITypeReference typeReference)
        {
            if (TypeDeclaration != null)
            {
                return;
            }
            TypeReference = typeReference;
            ITypeDeclaration typeDeclaration = typeReference as ITypeDeclaration;
            if (typeDeclaration == null)
            {
                typeDeclaration = typeReference.Resolve();
            }
            FillInfo(typeDeclaration);
        }

        internal void FillInfo(ITypeDeclaration typeDeclaration)
        {
            if (TypeDeclaration != null)
            {
                // Already filled...
                return;
            }
            TypeDeclaration = typeDeclaration;
            TypeReference = typeDeclaration;

            // We have now to parse the various information

            // Type and baseType discovered
            string baseTypeFullName = "";
            BaseType = null;
            if (typeDeclaration.BaseType != null)
            {
                baseTypeFullName = typeDeclaration.BaseType.Namespace + "::" + typeDeclaration.BaseType.Name;
                if (baseTypeFullName == "System::MulticastDelegate")
                {
                    Type = ObjectType.DELEGATE;
                }
                else if (baseTypeFullName == "System::ValueType")
                {
                    Type = ObjectType.STRUCT;
                }
                else if (baseTypeFullName == "System::Enum")
                {
                    Type = ObjectType.ENUM;
                }
                else if (baseTypeFullName == "System::Object")
                {
                    Type = ObjectType.CLASS;
                }
                else
                {
                    // Another class
                    Type = ObjectType.CLASS;

                    // In this case, we have to go resolve the parent type as well
                    BaseType = TypeInfoManager.GetTypeInfo(typeDeclaration.BaseType);
                }
            }
            else
            {
                // No base type
                if (typeDeclaration.Interface)
                {
                    Type = ObjectType.INTERFACE;
                }
                else
                {
                    Type = ObjectType.CLASS;
                }
            }

            // Now find all the interfaces
            IDictionary<TypeInfo, TypeInfo> interfacesAlreadyIncluded = new Dictionary<TypeInfo, TypeInfo>();

            // Add unique interfaces from the base type
            if (BaseType != null)
            {
                foreach (TypeInfo oneInterface in BaseType.UnionOfInterfaces)
                {
                    interfacesAlreadyIncluded[oneInterface] = oneInterface;
                }
            }

            // Add the interfaces from the passed interface as well
            IList<TypeInfo> baseInterfaces = new List<TypeInfo>(typeDeclaration.Interfaces.Count);
            foreach (ITypeReference baseInterface in typeDeclaration.Interfaces)
            {
                TypeInfo typeInfo = TypeInfoManager.GetTypeInfo(baseInterface);
                // Don't add this interface, as that's the exclusive interfaces we want to detect
                // But add it to an associate list, so we don't have to search for them again
                baseInterfaces.Add(typeInfo);

                // And all its parent interfaces...
                // It is expected that there is a lot of overlap as typeDeclaration.interfaces contain all the interfaces
                foreach (TypeInfo subInterface in typeInfo.UnionOfInterfaces)
                {
                    interfacesAlreadyIncluded[subInterface] = subInterface;
                }
            }

            // Now we can determine what interface is exclusive
            ExclusiveInterfaces = new List<TypeInfo>();
            foreach (TypeInfo baseInterface in baseInterfaces)
            {
                if (interfacesAlreadyIncluded.ContainsKey(baseInterface))
                {
                    // This interface is already part of the tree, skip it
                    continue;
                }
                ExclusiveInterfaces.Add(baseInterface);
            }

            // Now we do the union (that is with the baseInterfaces)
            UnionOfInterfaces = new List<TypeInfo>();
            foreach (TypeInfo baseInterface in baseInterfaces)
            {
                interfacesAlreadyIncluded[baseInterface] = baseInterface;
            }
            foreach (TypeInfo baseInterface in interfacesAlreadyIncluded.Keys)
            {
                UnionOfInterfaces.Add(baseInterface);
            }
        }

        public string GetPrefixedFullName(string prefix)
        {
            return (FullName + "::" + prefix + Name);
        }

        // Find the corresponding method...
        // For the moment, use the number of parameters as signature...
        public IMethodDeclaration FindMethod(string name, int numParameters)
        {
            foreach (IMethodDeclaration method in TypeDeclaration.Methods)
            {
                if (method.Name != name)
                {
                    continue;
                }
                if (method.Parameters.Count == numParameters)
                {
                    return (method);
                }
            }

            // Didn't find it, try the base implementations
            if (BaseType != null)
            {
                IMethodDeclaration baseMethod = BaseType.FindMethod(name, numParameters);
                if (baseMethod != null)
                {
                    return (baseMethod);
                }
            }

            // Still not, try now the interfaces
            foreach (TypeInfo oneInterface in UnionOfInterfaces)
            {
                // Potentially here we might parse several time the same interfaces
                // if interfaces are deriving from other interfaces
                IMethodDeclaration baseMethod = oneInterface.FindMethod(name, numParameters);
                if (baseMethod != null)
                {
                    return (baseMethod);
                }
            }

            return (null);
        }

        public IPropertyDeclaration FindProperty(string name, int numParameters)
        {
            foreach (IPropertyDeclaration property in TypeDeclaration.Properties)
            {
                if (property.Name != name)
                {
                    continue;
                }
                if (property.Parameters.Count == numParameters)
                {
                    return (property);
                }
            }

            // Didn't find it, try the base implementations
            if (BaseType != null)
            {
                IPropertyDeclaration baseProperty = BaseType.FindProperty(name, numParameters);
                if (baseProperty != null)
                {
                    return (baseProperty);
                }
            }

            // Still not, try now the interfaces
            foreach (TypeInfo oneInterface in UnionOfInterfaces)
            {
                // Potentially here we might parse several time the same interfaces
                // if interfaces are deriving from other interfaces
                IPropertyDeclaration baseProperty = oneInterface.FindProperty(name, numParameters);
                if (baseProperty != null)
                {
                    return (baseProperty);
                }
            }

            return (null);
        }

        public override int GetHashCode()
        {
            return (FullName.GetHashCode());
        }

        public override bool Equals(object obj)
        {
            TypeInfo other = obj as TypeInfo;
            if (other == null)
            {
                return (false);
            }
            return (FullName == other.FullName);
        }

        private LocalType mLocalType = null;
    }
}
