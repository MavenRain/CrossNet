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

namespace CrossNet.CppRuntime
{
    public class CppTypeInfo : ITypeInfo
    {
        internal CppTypeInfo(ITypeReference typeReference, string fullName)
        {
            string name = LanguageManager.NameFixup.GetSafeFullName(typeReference.Name);
            object owner = typeReference.Owner;
            while (owner is ITypeReference)
            {
                ITypeReference ownerType = (ITypeReference)owner;
                string ownerName = LanguageManager.NameFixup.GetSafeName(ownerType.Name);
                if (ownerType.GenericArguments.Count != 0)
                {
                    // That's a generic type, we add the generic postfix
                    ownerName += "__G" + ownerType.GenericArguments.Count.ToString();
                }

                name = ownerName + "__" + name;
                owner = ownerType.Owner;
            }

            // Why do we execute method name for the type name?
            // May the name of the method should not have method in it ;)
            // TODO: Change this
            mName = LanguageManager.NameFixup.UnmangleMethodName(name, true);
            if (typeReference.GenericArguments.Count != 0)
            {
                mName += "__G" + typeReference.GenericArguments.Count.ToString();
            }

/*
 * Deactivated for the moment as it seems it creates some issues with the generics...
 * 
            fullName = LanguageManager.NameFixup.UnmangleMethodName(fullName, true);
 */
            mFullName = LanguageManager.NameFixup.GetSafeFullName(fullName);
            mNamespace = "::" + LanguageManager.NameFixup.GetSafeFullName(typeReference.Namespace).Replace(".", "::");
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
                return (Namespace + "::" + Name);
            }
        }

        public string DotNetFullName
        {
            get
            {
                return (mTypeDeclaration.Namespace + "." + mTypeDeclaration.Name);
            }
        }

        public string NonScopedFullName
        {
            get
            {
                return (mFullName.Substring(2));
            }
        }

        // This is unfrtunately more complicated than I would like
        // The goal is to create a wrapper name that is simple (like "Wrapper__" + interfaceName)
        // So it is easy to manipulate and debug.
        // But the issue happens when a wrapper can have the same meaning for two different interfaces
        // When it's on separate classes that's fine, but when it collides on the same class that generates some issues

        // The most common case is class implementing two time the same generic interface with different generic parameter
        // Although it could happen as well with two interfaces named the same but in two different namespaces
        // This code associates for a given interface (and it's generic parameter) a unique wrapper name.
        // The first given name is the simple one.
        // If another interface (same name or different generic parameter) is wrapped, it will have the name + "__" + count++
        // So each version will be unique from now on...

        // Note that it potentially still can create some issues (although I didn't find one yet)
        // If an implementation uses wrapper from a base class defined by another assembly (or written in C++),
        // The collision might not match and the numbers will probably not be the same...
        // If that happens, I'll have to name wrapper with the fully qualified type (and generic parameter)
        // This is going to be less handy and though... (lenghty names...)
        // Another solution would be to keep the short name and append the CRC of the full name
        // It would work but wrapper in C++ would be painful...
 
        // I guess we'll have to see how it goes...

        class Key
        {
            public Key(string name, string declaringType)
            {
                Name = name;
                DeclaringType = declaringType;
            }

            public override bool Equals(object obj)
            {
                Key k = obj as Key;
                if (k == null)
                {
                    return (false);
                }
                if (Name != k.Name)
                {
                    return (false);
                }
                return (DeclaringType == k.DeclaringType);
            }

            public override int GetHashCode()
            {
                return (Name.GetHashCode() ^ DeclaringType.GetHashCode());
            }

            public string Name;
            public string DeclaringType;
        }

        public string GetWrapperName(string declaringType)
        {
            if (mWrapperName == null)
            {
                // Don't use the generic parameters when looking for unique wrapper
                Key fullNameKey = new Key(FullName, declaringType);
                // First let's find if we have a wrapper with the same full name
                if (sWrapperNameFromFullName.TryGetValue(fullNameKey, out mWrapperName))
                {
                    // Already registered nothing else to do...
                    return (mWrapperName);
                }

                // Not yet registered, we have to register one
                // First let's look at possible collision
                mWrapperName = "Wrapper__" + Name;

                Key wrapperNameKey = new Key(mWrapperName, declaringType);

                int count;
                if (sWrapperCount.TryGetValue(wrapperNameKey, out count))
                {
                    // There is already a known collision, we have to use another count
                    string newWrapperName = mWrapperName + "__" + count++;
                    sWrapperNameFromFullName[fullNameKey] = newWrapperName;
                    sWrapperCount[wrapperNameKey] = count;
                    mWrapperName = newWrapperName;
                    return (newWrapperName);
                }

                // Register for next collision later
                sWrapperNameFromFullName[fullNameKey] = mWrapperName;
                sWrapperCount[wrapperNameKey] = 0;
            }
            return (mWrapperName);
        }

        public string Namespace
        {
            get
            {
                return (mNamespace);
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
                if (IsValueType)
                {
                    return (".");
                }
                else
                {
                    return ("->");
                }
            }
        }

        public LocalType LocalType
        {
            get
            {
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
                    // This level is not even a generic
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
                if (mDependencies == null)
                {
                    return (0);
                }
                return (mDependencies.Count);
            }
#if !STANDARD_DEP
            set
            {
                mDependencies = null;
            }
#endif
        }

        public bool DependencyCleared
        {
            get
            {
                return (mDependencyCleared);
            }
            set
            {
                mDependencyCleared = value;
                if (value)
                {
                    // If the dependency have been cleared, them mDependencies should be null
                    Debug.Assert(mDependencies == null);
                }
            }
        }

        public string GetFullName(ParsingInfo info)
        {
            return (LanguageManager.ReferenceGenerator.GenerateCodeType(mTypeDeclaration, info).Text);
        }

        public string GetInstanceText(ParsingInfo info)
        {
            StringData data = LanguageManager.ReferenceGenerator.GenerateCodeType(mTypeDeclaration, info);
            data.AppendSameLine(GetInstancePostFix());
            return (data.Text);
        }

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

        /// <summary>
        /// Tells if a type is a sub-type of a passed type as parameter.
        /// </summary>
        /// <param name="baseType">Base-type to compare to.</param>
        /// <returns>True if baseType is a base type of "this" type. False otherwise.</returns>
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
            IDictionary<ITypeInfo, ITypeInfo> exclusiveInterfacesAlreadyIncluded = new Dictionary<ITypeInfo, ITypeInfo>();
/*
 * Don't add the interface as itself...
 * 
            if (Type == ObjectType.INTERFACE)
            {
                // This is an interface, add itself
                interfacesAlreadyIncluded[this] = this;
            }
 */

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
            mExclusiveInterfaces = new List<ITypeInfo>();
            foreach (ITypeReference baseInterface in typeDeclaration.Interfaces)
            {
                ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(baseInterface);
                // Don't add this interface, as that's the exclusive interfaces we want to detect
                // But add it to an associate list, so we don't have to search for them again
                baseInterfaces.Add(typeInfo);

                exclusiveInterfacesAlreadyIncluded[typeInfo] = typeInfo;

                // And all its parent interfaces...
                // It is expected that there is a lot of overlap as typeDeclaration.interfaces contain all the interfaces
                foreach (ITypeInfo subInterface in typeInfo.UnionOfInterfaces)
                {
                    interfacesAlreadyIncluded[subInterface] = subInterface;
                    exclusiveInterfacesAlreadyIncluded[subInterface] = subInterface;
                }
            }

            // Now we can determine which interface is exclusive
/*
 * 
            foreach (ITypeInfo baseInterface in baseInterfaces)
            {
                if (interfacesAlreadyIncluded.ContainsKey(baseInterface))
                {
                    // This interface is already part of the tree, skip it
                    continue;
                }
                ExclusiveInterfaces.Add(baseInterface);
            }
*/
            mExclusiveInterfaces = new List<ITypeInfo>();
            foreach (ITypeInfo eachExclusiveInterface in exclusiveInterfacesAlreadyIncluded.Keys)
            {
                mExclusiveInterfaces.Add(eachExclusiveInterface);
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

/*
            // Now look for the operators...
            foreach (IMethodDeclaration methodDeclaration in typeDeclaration.Methods)
            {
                MethodType methodType;
                LanguageManager.NameFixup.ConvertMethodName(methodDeclaration.Name, out methodType);
                if (methodType == MethodType.OPERATOR)
                {
                    mOperators.Add(methodDeclaration);
                }
                else if (methodType == MethodType.NORMAL)
                {
                    mOperators.Add(methodDeclaration);
                }
                else
                {
                    // For the moment don't add any of the other method types...
                }
            }
 */

            // Before we leave the function, initialize mLocalType
            // This will create the cached version (or initlaize the base type if needed)
            mLocalType = LanguageManager.LocalTypeManager.GetLocalType(TypeDeclaration);
        }

        public string GetPrefixedFullName(string prefix)
        {
            return (FullName + "::" + prefix);
        }



        public void DetectDependencies()
        {
            DetectDependencies(this, this);
        }

        private static void DetectDependencies(CppTypeInfo topOwner, ITypeInfo typeInfo)
        {
            // We have to detect if a class must be defined before

            // A class must be defined before, if it is not a top class.
            //  Structures must be defined before.
            //  Enums must be defined before.
            //  Nested classes must be defined before.

            // This rule needs to be applied on all derived class, interfaces
            // All method parameters and return values
            // All inner classes, all properties, all members...

            // Finally all the dependencies of the nested classes must be sent to the owner

            // First look for base type... This is a dependency regardless of the type
            if (typeInfo.BaseType != null)
            {
                topOwner.AddDependency(typeInfo.BaseType, true);
            }

            // Then all the interfaces... This is a dependency regardless of the type
            foreach (ITypeInfo oneInterface in typeInfo.UnionOfInterfaces)
            {
                topOwner.AddDependency(oneInterface, true);
            }

            // Add the fields
            ITypeDeclaration typeDeclaration = typeInfo.TypeDeclaration;
            foreach (IFieldDeclaration field in typeDeclaration.Fields)
            {
                ITypeInfo fieldTypeInfo = TypeInfoManager.GetTypeInfo(field.FieldType);
                topOwner.AddDependency(fieldTypeInfo, false);
            }

            // Look at the methods... By the same occasion, this should look at the properties as well
#if !STANDARD_DEP
            // It seems that if we don't implement the method, we just need to forward declare the type
            // There is no need to actually have the type defined...
            // At least that's the case on VC++8.0, it might be different with other compilers :(

            // Except that for delegates, it's a bit different
            // later we will differentiate between declaration and implementation
            // But for the moment, do the dependency tracking just for delegate methods...

            if (typeInfo.Type == ObjectType.DELEGATE)
#endif
            {
                foreach (IMethodDeclaration method in typeDeclaration.Methods)
                {
                    ITypeInfo returnTypeInfo = TypeInfoManager.GetTypeInfo(method.ReturnType.Type);
                    topOwner.AddDependency(returnTypeInfo, false);

                    foreach (IParameterDeclaration parameter in method.Parameters)
                    {
                        ITypeInfo parameterTypeInfo = TypeInfoManager.GetTypeInfo(parameter.ParameterType);
                        topOwner.AddDependency(parameterTypeInfo, false);
                    }
                }
            }

            // Finally parse the nested classes
#if false   // Deactivated with the un-nesting - now sub-types are ordered on their own...
            ITypeDeclarationCollection nestedTypes;
            try
            {
                // In some cases, like generic interface in mscorlib, this is going to throw an exception
                // So handle gracefully the exception
                nestedTypes = typeDeclaration.NestedTypes;
            }
            catch
            {
                nestedTypes = null;
            }
            if (nestedTypes != null)
            {
                foreach (ITypeDeclaration nestedClass in nestedTypes)
                {
                    ITypeInfo nestedTypeInfo = TypeInfoManager.GetTypeInfo(nestedClass);
                    DetectDependencies(topOwner, nestedTypeInfo);
                }
            }
#endif
        }

        private void AddDependency(ITypeInfo typeInfoToAdd, bool forced)
        {
            if (typeInfoToAdd == null)
            {
                // No type provided, nothing to add
                return;
            }

            if (mDependencies != null)
            {
                if (mDependencies.ContainsKey(typeInfoToAdd))
                {
                    // If it has been already added, don't add it again...
                    // This is the most efficient with standard value types (void, int, float, etc...),
                    // so 90% of the time we are not doing the full lookup...

                    // We maybe could improve a bit more by passing the values that we skipped as well?
                    return;
                }
            }

            bool isValue = typeInfoToAdd.IsValueType;

            // Handle the generic parameters (recursively)
            foreach (IType type in typeInfoToAdd.TypeReference.GenericArguments)
            {
                ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(type);
                AddDependency(typeInfo, forced);
            }

            // Then the corresponding generic type
            ITypeReference typeReference = typeInfoToAdd.TypeReference.GenericType;
            if (typeReference != null)
            {
                typeInfoToAdd = TypeInfoManager.GetTypeInfo(typeReference);
            }

#if false   // Disabled with un-nesting... We don't need the top owner anymore...
            // Get the top owner
            // Use the generic type to make sure it points to the exact same instance...
            ITypeReference owner = typeInfoToAdd.TypeReference.GenericType;
            if (owner == null)
            {
                // If it doesn't exist (i.e. it's not a generic), use the standard type reference
                owner = typeInfoToAdd.TypeReference;
            }
            bool wasTopOwner = true;
            for ( ; ; )
            {
                ITypeReference tempOwner = owner.Owner as ITypeReference;
                if (tempOwner == null)
                {
                    break;
                }
                owner = tempOwner;
                wasTopOwner = false;
            }

            if (object.ReferenceEquals(this.TypeReference, owner))
            {
                // Pointing to the same top owner, we don't want to add it... Even if it is forced...
                // Note: there might be some cases, where this code won't actually work...
                // We could have classes within the same owner, with one depending of the other
                // In that case, we would need to have a proper ordering...
                // See how we can improve this for later...
                return;
            }

            if (forced == false)
            {
                if (wasTopOwner && (isValue == false))
                {
                    // If it was a class, and is a top owner, it means that we were able to forward declare it...
                    // So no dependencies here...
                    return;
                }
            }

            // We actually add the owner and not the sub-class...
            typeInfoToAdd = TypeInfoManager.GetTypeInfo(owner);
            if (object.ReferenceEquals(this, typeInfoToAdd))
            {
                // We do the check another time, as we could have the same value here as well
                // It did happen, it could be related to generic like code...
                // TODO: Investigate...
                return;
            }
#else
            if ((forced == false) && (isValue == false))
            {
                // It's a not by value and not forced we were able to use the forward declaration
                return;
            }
            if (object.ReferenceEquals(this, typeInfoToAdd))
            {
                // Trying to add the same type
                return;
            }
#endif

            if (mDependencies == null)
            {
                mDependencies = new Dictionary<ITypeInfo, object>();
            }
            if (mDependencies.ContainsKey(typeInfoToAdd) == false)
            {
                // We change the dictionary (and thus create a new key value pair) only if necessary...
                mDependencies[typeInfoToAdd] = null;
            }
        }

        public bool RemoveSolvedDependencies()
        {
            if (mDependencies == null)
            {
                return (false);
            }
            Stack<ITypeInfo> toRemove = null;
            foreach (ITypeInfo dependency in mDependencies.Keys)
            {
                if (dependency.DependencyCleared)
                {
                    // Dependency cleared, so we can remove it...
                    if (toRemove == null)
                    {
                        toRemove = new Stack<ITypeInfo>();
                    }
                    toRemove.Push(dependency);
                }
            }

            if (toRemove != null)
            {
                while (toRemove.Count != 0)
                {
                    ITypeInfo keyToremove = toRemove.Pop();
                    mDependencies.Remove(keyToremove);
                }

                if (mDependencies.Count == 0)
                {
                    // Everything has been removed, set the member to null to free up some memory
                    // We use it as check as well to make sure we progressively remove types...
                    mDependencies = null;
                }

                // Retunr true as the internal state changed
                return (true);
            }

            // Nothing changed for this type
            return (false);
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

        public override string ToString()
        {
            return (mFullName);
        }

        private string mName;
        private string mFullName;
        private string mNamespace;
        private ObjectType mType;
        private ITypeInfo mBaseType;
        private IList<ITypeInfo> mUnionOfInterfaces;
        private IList<ITypeInfo> mExclusiveInterfaces;
/*
        private IList<IMethodDeclaration> mOperators = new List<IMethodDeclaration>();
        private IList<IMethodDeclaration> mMethods = new List<IMethodDeclaration>();
 */
        private ITypeReference mTypeReference;
        private ITypeDeclaration mTypeDeclaration;
        private LocalType mLocalType = null;
        private IDictionary<ITypeInfo, object> mDependencies = null;
        private bool mDependencyCleared = true;

        private string mWrapperName = null;

        private static Dictionary<Key, string> sWrapperNameFromFullName = new Dictionary<Key, string>();
        private static Dictionary<Key, int> sWrapperCount = new Dictionary<Key, int>();
    }
}
