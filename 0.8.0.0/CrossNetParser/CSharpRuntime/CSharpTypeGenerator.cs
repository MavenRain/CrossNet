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
using Reflector.CodeModel.Memory;

using CrossNet.Common;
using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.CSharpRuntime
{
    public class CSharpTypeGenerator : ITypeGenerator
    {
        public GeneratedData GenerateCode(IAssembly assembly, AssemblyData assemblyData)
        {
            string outputAssemblyName = assemblyData.AssemblyName;

            GeneratedData   outputFiles = new GeneratedData();
            mSourceData = new StringData();
            outputFiles.AddFile(assemblyData.AssemblyName + SOURCE_TYPE, mSourceData);

            foreach (IModule module in assembly.Modules)
            {
                GenerateCode(module, assemblyData);
            }
            return (outputFiles);
        }

        public void GenerateCode(IModule module, AssemblyData assemblyData)
        {
            foreach (ITypeDeclaration typeDeclaration in module.Types)
            {
                ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(typeDeclaration);
                GenerateCode(typeInfo, NestedType.NOT_NESTED, assemblyData);
            }
        }

        public void GenerateCode(ITypeInfo typeInfo, NestedType nested, AssemblyData assemblyData)
        {
            ITypeDeclaration typeDeclaration = typeInfo.TypeDeclaration;
            if (nested == NestedType.NOT_NESTED)
            {
                if (typeDeclaration.Namespace == "")
                {
                    // If not nested and there is no namespace, don't parse it
                    // This is another case of specific .NET internals
                    return;
                }

                // If not nested, have to add the namespace
                string nameSpace = "namespace " + typeDeclaration.Namespace + "\n";
                mSourceData.Append(nameSpace);
                mSourceData.Append("{\n");
                ++mSourceData.Indentation;
            }

            string text = "";
//            text += typeDeclaration.Visibility.ToString();
            switch (typeDeclaration.Visibility)
            {
                case TypeVisibility.Public:
                case TypeVisibility.NestedPublic:
                    text += "public ";
                    break;
                case TypeVisibility.Private:
                    // This doesn't seem correct, but that's what reflector returns
                    text += "internal ";
                    break;

                case TypeVisibility.NestedPrivate:
                    text += "private ";
                    break;

                case TypeVisibility.NestedFamily:
                    text += "protected ";
                    break;

                case TypeVisibility.NestedFamilyAndAssembly:
                // Don't know what this one gives, but assume same as NestedFamilyOrAssembly
                case TypeVisibility.NestedFamilyOrAssembly:
                    text += "protected internal ";
                    break;
                case TypeVisibility.NestedAssembly:
                    text += "internal ";
                    break;
            }

            string baseType = "";
            ObjectType objectType = ObjectType.CLASS;
            if (typeDeclaration.BaseType != null)
            {
                baseType = typeDeclaration.BaseType.Namespace + "." + typeDeclaration.BaseType.Name;
                if (baseType == "System.MulticastDelegate")
                {
                    objectType = ObjectType.DELEGATE;
                }
            }

            if (objectType == ObjectType.DELEGATE)
            {
                text += "delegate ";
            }
            else if (typeDeclaration.Interface)
            {
                text += "interface ";
                objectType = ObjectType.INTERFACE;
            }
            else
            {
                string lowerType = baseType.ToLower();
                if (lowerType == "system.valuetype")
                {
                    text += "struct ";
                    objectType = ObjectType.STRUCT;
                }
                else if (lowerType == "system.enum")
                {
                    text += "enum ";
                    objectType = ObjectType.ENUM;
                }
                else
                {
                    if (typeDeclaration.Abstract && typeDeclaration.Sealed)
                    {
                        // Special case here:
                        // If abstract and sealed, it is a static class (i.e. non-instanciable, can't be derived as well)
                        // abstract means not instanciable
                        // sealed means not deriveable...
                        text += "static ";
                    }
                    else if (typeDeclaration.Abstract)
                    {
                        text += "abstract ";
                    }
                    else if (typeDeclaration.Sealed)
                    {
                        text += "sealed ";
                    }
                    text += "class ";
                }
            }

            string declarationName = LanguageManager.NameFixup.UnmangleName(typeDeclaration.Name);
            declarationName += GenerateCodeGenericArguments(typeDeclaration.GenericArguments, nested);

            if (objectType == ObjectType.DELEGATE)
            {
                // Let's recognize the parameters for the delegate....
                // Need to look at the invoke method!!!

                IMethodDeclaration invokeMethod = null;
                foreach (IMethodDeclaration methodDeclaration in typeDeclaration.Methods)
                {
                    if (methodDeclaration.Name == "Invoke")
                    {
                        invokeMethod = methodDeclaration;
                    }
                }

                Debug.Assert(invokeMethod != null);

                ParsingInfo info = new ParsingInfo(typeDeclaration);
                StringData parameters = LanguageManager.ReferenceGenerator.GenerateCodeParameterDeclarationCollection(invokeMethod.Parameters, info);

                // Before adding the declaration name, add the return value
                StringData returnValue = LanguageManager.ReferenceGenerator.GenerateCodeType(invokeMethod.ReturnType.Type, info);

                text += returnValue.Text + " ";
                text += declarationName;
                text += "(" + parameters.Text + ");\n";
                mSourceData.Append(text);

                // For the delegate, we are not parsing more...
                // Later we might have top parse invoke so we can get the parameters...

                // Before we leave the method, we restore the indentation and namespace closure...
                if (nested == NestedType.NOT_NESTED)
                {
                    --mSourceData.Indentation;
                    mSourceData.Append("}\n");
                }
                return;
            }
            text += declarationName;
            if (objectType == ObjectType.ENUM)
            {
                GenerateCodeEnum(declarationName, typeInfo);
                // Before we leave the method, we restore the indentation and namespace closure...
                if (nested == NestedType.NOT_NESTED)
                {
                    --mSourceData.Indentation;
                    mSourceData.Append("}\n");
                }
                return;
            }
            if ((typeDeclaration.BaseType != null) || (typeDeclaration.Interfaces.Count != 0))
            {
                ParsingInfo fakeInfo = new ParsingInfo(typeDeclaration);
                bool prev = false;
                if (typeDeclaration.BaseType != null)
                {
                    string fullName = LanguageManager.ReferenceGenerator.GenerateCodeTypeReference(typeDeclaration.BaseType, fakeInfo).Text;
                    string fullNameToLower = fullName.ToLower();
                    if ((fullNameToLower != "system.object") && (fullNameToLower != "system.valuetype"))
                    {
                        // if it derives from system.object, no need to explicit it
                        if (prev == false)
                        {
                            text += " : ";
                        }
                        text += fullName;
                        prev = true;
                    }
                }

                foreach (ITypeReference oneInterface in typeDeclaration.Interfaces)
                {
                    if (prev)
                    {
                        text += ", ";
                    }
                    else
                    {
                        text += " : ";
                    }

                    text += LanguageManager.ReferenceGenerator.GenerateCodeTypeReference(oneInterface, fakeInfo);
                    prev = true;
                }
            }

            // After defining the class and its base class / interfaces, add the constraints for generic
            text += GenerateCodeGenericConstraints(typeDeclaration.GenericArguments, nested);

            text += "\n";
            mSourceData.Append(text);
            mSourceData.Append("{\n");
            ++mSourceData.Indentation;

            IDictionary removeMembers = new Hashtable();
            IDictionary removeMethods = new Hashtable();

            // List all the events
            // We have to do this first as it will invalidate some members and some methods
            foreach (IEventDeclaration eventDeclaration in typeDeclaration.Events)
            {
                // Are all the events public?
                ParsingInfo info = new ParsingInfo(typeDeclaration);

                text = "";
                if (objectType != ObjectType.INTERFACE)
                {
                    // Can't add public for interface
                    text = "public ";
                }
                text += LanguageManager.ReferenceGenerator.GenerateCodeEventDeclaration(eventDeclaration, info).Text;
                text += ";\n";
                mSourceData.Append(text);

                // Remove the name of the event (can't have a field named the same way...)
                removeMembers.Add(eventDeclaration.Name, null);

                // Remove the two methods associated with the event...
                removeMethods.Add(eventDeclaration.AddMethod, null);
                removeMethods.Add(eventDeclaration.RemoveMethod, null);
            }

            foreach (IPropertyDeclaration propertyDeclaration in typeDeclaration.Properties)
            {
                bool isInterface = (objectType == ObjectType.INTERFACE);
                GenerateProperty(isInterface, typeInfo, propertyDeclaration, objectType, removeMethods, false);
            }

            // List all the methods
            foreach (IMethodDeclaration methodDeclaration in typeDeclaration.Methods)
            {
                // Here we assume that method reference and method declaration are the same object
                // This is not necessarily true... But should be with Reflector
                if (removeMethods.Contains(methodDeclaration))
                {
                    // We have to skip this even related method...
                    continue;
                }
                ParsingInfo methodInfo = new ParsingInfo(typeDeclaration);
                GenerateCodeMethod(typeInfo, methodDeclaration, objectType, methodInfo);
            }

            // List all the members
            foreach (IFieldDeclaration fieldDeclaration in typeDeclaration.Fields)
            {
                if (removeMembers.Contains(fieldDeclaration.Name))
                {
                    // We have to skip this event related member...
                    continue;
                }
                text = "";
                switch (fieldDeclaration.Visibility)
                {
                    case FieldVisibility.Public:
                        text += "public ";
                        break;
                    case FieldVisibility.PrivateScope:
                    case FieldVisibility.Private:
                        text += "private ";
                        break;
                    case FieldVisibility.Family:
                        text += "protected ";
                        break;
                    case FieldVisibility.Assembly:
                        text += "internal ";
                        break;
                    case FieldVisibility.FamilyAndAssembly:
                    // Don't know what this one gives, but assume same as NestedFamilyOrAssembly
                    case FieldVisibility.FamilyOrAssembly:
                        text += "internal protected ";
                        break;
                }

                if (fieldDeclaration.Literal)
                {
                    text += "const ";
                }
                else
                {
                    if (fieldDeclaration.Static)
                    {
                        text += "static ";
                    }
                    // No else if here as variable can be static AND read only...
                    if (fieldDeclaration.ReadOnly)
                    {
                        text += "readonly ";
                    }
                }
                ParsingInfo info = new ParsingInfo(typeDeclaration);
                info.ParsingField = true;
                string field = LanguageManager.ReferenceGenerator.GenerateCodeFieldDeclaration(fieldDeclaration, info).Text;
                if (info.UnsafeMethod)
                {
                    field = "unsafe " + field;

                }
                text += field + ";\n";

                mSourceData.Append(text);
            }

            // List all the nested types
            NestedType localNestedType = nested;
            if (typeDeclaration.GenericArguments.Count != 0)
            {
                localNestedType = NestedType.NESTED_GENERIC;
            }
            else if (nested == NestedType.NOT_NESTED)
            {
                localNestedType = NestedType.NESTED_STANDARD;
            }

            foreach (ITypeDeclaration nestedTypeDeclaration in typeDeclaration.NestedTypes)
            {
                ITypeInfo nestedTypeInfo = TypeInfoManager.GetTypeInfo(nestedTypeDeclaration);
                GenerateCode(nestedTypeInfo, localNestedType, assemblyData);
            }

            --mSourceData.Indentation;
            mSourceData.Append("}\n");
            if (nested == NestedType.NOT_NESTED)
            {
                --mSourceData.Indentation;
                mSourceData.Append("}\n");
            }
        }

        public string GenerateCodeGenericArguments(ITypeCollection genericArguments, NestedType nested)
        {
            if (genericArguments.Count == 0)
            {
                return ("");
            }

            // There are some generic parameters... list them
            if (nested == NestedType.NESTED_GENERIC)
            {
                return ("");
            }

            // Add them only if not nested (as the child reproduces parent parameters)
            string text = "<";
            bool firstGeneric = true;
            foreach (IType typeReference in genericArguments)
            {
                if (firstGeneric == false)
                {
                    text += ", ";
                }
                firstGeneric = false;
                text += typeReference.ToString();
            }
            text += ">";
            return (text);
        }

        public string GenerateCodeGenericConstraints(ITypeCollection genericArguments, NestedType nested)
        {
            if (genericArguments.Count == 0)
            {
                return ("");
            }

            // There are some generic parameters... list them
            if (nested == NestedType.NESTED_GENERIC)
            {
                return ("");
            }

            // Add them only if not nested (as the child reproduces parent parameters)

            string text = "";
            foreach (IType typeReference in genericArguments)
            {
                IGenericParameter genericParameter = typeReference as IGenericParameter;
                if (genericParameter != null)
                {
                    if (genericParameter.Constraints.Count != 0)
                    {
                        text += " where " + typeReference.ToString() + " : ";
                        bool first = true;
                        foreach (IType constraint in genericParameter.Constraints)
                        {
                            if (first == false)
                            {
                                text += ", ";
                            }
                            first = false;
                            if (constraint is IDefaultConstructorConstraint)
                            {
                                text += "new()";
                            }
                            else if (constraint is IReferenceTypeConstraint)
                            {
                                text += "class";
                            }
                            else if (constraint is IValueTypeConstraint)
                            {
                                text += "struct";
                                // Struct is particular as no more constraint can be used after
                                // Those are forbidden: new(), base class, base struct...

                                // Il returns us System.TypeValue and new() as constraint
                                // break to skip them...
                                break;
                            }
                            else if (constraint is ITypeReference)
                            {
                                text += LanguageManager.ReferenceGenerator.GenerateCodeType(constraint, new ParsingInfo(null));
                            }
                            else
                            {
                                // Last case is for naked type constraints
                                // where U : T
                                // T being a template argument
                                text += constraint.ToString();
                            }
                        }
                    }
                }
            }
            return (text);
        }

        public void GenerateProperty(bool isInterface, ITypeInfo typeInfo, IPropertyDeclaration propertyDeclaration, ObjectType objectType, IDictionary removeMethods, bool wrapper)
        {
            ITypeDeclaration typeDeclaration = typeInfo.TypeDeclaration;
            IMethodReference methodReferenceSet = propertyDeclaration.SetMethod;
            IMethodDeclaration methodDeclarationSet = (IMethodDeclaration)methodReferenceSet;
            IMethodReference methodReferenceGet = propertyDeclaration.GetMethod;
            IMethodDeclaration methodDeclarationGet = (IMethodDeclaration)methodReferenceGet;
            ParsingInfo info = new ParsingInfo(typeDeclaration);

            IMethodDeclaration protectionDeclaration = methodDeclarationSet;
            if (protectionDeclaration == null)
            {
                // If we can't use the set method, use the get method for the protection
                protectionDeclaration = methodDeclarationGet;
            }
            // At least set or get is not null
            Debug.Assert(protectionDeclaration != null);

            string text = "";
            // For interface, we don't want method modifiers
            if ((isInterface == false) && (propertyDeclaration.Name.IndexOf(".") == -1))
            {
                // No protection nor abstract keyword for interface
                // Same if we don't override a specific method (in that case the method name contains a . for the fully qualified type)...
                // The first . is not even valid as there is not constructor as property...
                bool abstractMethod;
                text = GetMethodModifiers(typeInfo, protectionDeclaration, objectType, out abstractMethod);
            }

            // Append by string to avoid double tabulations.
            string returnValue;
            text += LanguageManager.ReferenceGenerator.GenerateCodePropertyDeclaration(propertyDeclaration, info, out returnValue).Text;

            StringData propertyCode = new StringData();
            if (methodDeclarationSet != null)
            {
                IBlockStatement setBody = methodDeclarationSet.Body as IBlockStatement;

                if (setBody != null)
                {
                    propertyCode.Append("set\n");

                    info.ReturnedType = LanguageManager.LocalTypeManager.TypeVoid;
                    string bodyText = LanguageManager.StatementGenerator.GenerateCodeForMethod(setBody, info).Text;
                    propertyCode.Append(bodyText);
                }
                else
                {
                    propertyCode.Append("set;\n");
                }
                removeMethods.Add(methodReferenceSet, null);
            }
            if (methodDeclarationGet != null)
            {
                IBlockStatement getBody = methodDeclarationGet.Body as IBlockStatement;

                if (getBody != null)
                {
                    propertyCode.Append("get\n");

                    info.ReturnedType = LanguageManager.LocalTypeManager.GetLocalType(propertyDeclaration.PropertyType);
                    string bodyText = LanguageManager.StatementGenerator.GenerateCodeForMethod(getBody, info).Text;

                    FixReflectorBug(getBody, bodyText, returnValue, propertyCode);
                }
                else
                {
                    propertyCode.Append("get;\n");
                }
                removeMethods.Add(methodReferenceGet, null);
            }

            if (info.UnsafeMethod)
            {
                text = "unsafe " + text;
            }

            mSourceData.Append(text);
            mSourceData.Append("\n{\n");
            mSourceData.Indentation++;
            mSourceData.Append(propertyCode.Text);
            mSourceData.Indentation--;
            mSourceData.Append("}\n");
        }

        public string GetMethodModifiers(ITypeInfo typeInfo, IMethodDeclaration methodDeclaration, ObjectType objectType, out bool abstractMethod)
        {
            ITypeDeclaration typeDeclaration = typeInfo.TypeDeclaration;
            string text = "";
            abstractMethod = false;

            switch (methodDeclaration.Visibility)
            {
                case MethodVisibility.Public:
                    text += "public ";
                    break;
                case MethodVisibility.PrivateScope:
                case MethodVisibility.Private:
                    text += "private ";
                    break;
                case MethodVisibility.Family:
                    text += "protected ";
                    break;
                case MethodVisibility.Assembly:
                    text += "internal ";
                    break;
                case MethodVisibility.FamilyAndAssembly:
                // Don't know what this one gives, but assume same as NestedFamilyOrAssembly
                case MethodVisibility.FamilyOrAssembly:
                    text += "internal protected ";
                    break;
            }

            if (methodDeclaration.Abstract)
            {
                text += "abstract ";
                abstractMethod = true;
            }
            else if (methodDeclaration.Virtual)
            {
                if (methodDeclaration.NewSlot)
                {
                    if (text == "private ")
                    {
                        // Special case here...

                        // "private" and "virtual" are incompatible in C#
                        // In some cases, they can be auto-generated by the compiler (see generic with IEnumerator<T>)
                        // But in any case, the user can't do the same thing as it will generate a compiler error...

                        if (typeDeclaration.Sealed)
                        {
                            // If it is sealed, then don't even talk about virtual...
                            text = "public ";
                        }
                        else
                        {
                            // assume that the function public virtual instead!
                            text = "public virtual";
                        }
                    }
                    else
                    {
                        if (objectType != ObjectType.STRUCT)
                        {
                            // In some case we can receive virtual method for a struct (this is actually not a valid condition).
                            if (typeDeclaration.Sealed)
                            {
                                // We can also get virtual for sealed method (the C# compiler doesn't like this)
                            }
                            else
                            {
                                text += "virtual ";
                            }
                        }
                    }
                }
                else
                {
                    text += "override ";
                }
            }
            else if (methodDeclaration.Static)
            {
                text += "static ";
            }

            return (text);
        }

        public void GenerateCodeMethod(ITypeInfo typeInfo, IMethodDeclaration methodDeclaration, ObjectType objectType, ParsingInfo info)
        {
            ITypeDeclaration typeDeclaration = typeInfo.TypeDeclaration;
            string text = "";

            if ((objectType != ObjectType.INTERFACE) && (methodDeclaration.Abstract == false) && (methodDeclaration.Body == null))
            {
                // For extern, we handle the attributes [in, out] differently
                // We are not using ref, not out keywords...
                info.InExtern = true;
            }
            else
            {
                info.InExtern = false;
            }

            string returnValue;
            // Even if this is not needed for constructors still do the parsing of the return value here
            // So the unsafe flag is updated accordingly...
            // Also we can get the returned type set correclty in case we have to patch the return statement (literal int -> char)
            returnValue = LanguageManager.ReferenceGenerator.GenerateCodeType(methodDeclaration.ReturnType.Type, info).Text;
            info.ReturnedType = LanguageManager.LocalTypeManager.GetLocalType(methodDeclaration.ReturnType.Type);

            // C# specific: Get the body before the other modifiers
            // so we can detect the "unsafe" state
            IBlockStatement body = methodDeclaration.Body as IBlockStatement;
            string bodyText = LanguageManager.StatementGenerator.GenerateCodeForMethod(body, info).Text;
            // Same for the parameters for the function
            StringData parameters = LanguageManager.ReferenceGenerator.GenerateCodeParameterDeclarationCollection(methodDeclaration.Parameters, info);
            bool unsafeAdded = false;

            foreach (ICustomAttribute attribute in methodDeclaration.Attributes)
            {
                text += "[";
                string attributeName = LanguageManager.ReferenceGenerator.GenerateCodeMethodReference(attribute.Constructor, info).Text;
                if (attributeName.EndsWith(ATTRIBUTE))
                {
                    attributeName = attributeName.Substring(0, attributeName.Length - ATTRIBUTE.Length);
                }
                text += attributeName;
                text += LanguageManager.ExpressionGenerator.GenerateCodeMethodInvokeArguments(attribute.Arguments, info);
                text += "] ";
            }
            if (methodDeclaration.Attributes.Count != 0)
            {
                text += "\n";
            }

/*
            text += methodDeclaration.CallingConvention.ToString();
            text += " ";
*/
//            text += methodDeclaration.Documentation;

            if (info.InExtern)
            {
                // The class is not an interface, the method is no abstract and there is no body,
                // the only reason why there can't be a body is because it's an extern method
                text += "extern ";
            }

            if (info.UnsafeMethod)
            {
                // There is some unsafe components in the method: Mark the method as unsafe
                text += "unsafe ";
                unsafeAdded = true;
            }

            if ((typeDeclaration.Interface == false) && (methodDeclaration.Name.IndexOf(".", 1) == -1))
            {
                // No protection nor abstract keyword for interface
                // Same if we don't override a specific method (in that case the method name contains a . for the fully qualified type)...
                // It's okay if we have the "." at the first position though...
                bool abstractMethod;
                text += GetMethodModifiers(typeInfo, methodDeclaration, objectType, out abstractMethod);
            }

            bool finalizer = false;

/*
 * We should actually use this type to detect the constructor...
 * Instead of using the string ".ctor" or ".cctor"
            IConstructorDeclaration constructorDeclaration = methodDeclaration as IConstructorDeclaration;
*/

            if (methodDeclaration.Name == ".ctor")
            {
                // If method name is ".ctor", this is the constructor
                ITypeReference type = (ITypeReference)methodDeclaration.DeclaringType;
                text += LanguageManager.NameFixup.UnmangleName(type.Name);
            }
            else if (methodDeclaration.Name == ".cctor")
            {
                // If method name is ".cctor" this is the static constructor
                //      In .Net, static constructor is called at the beginning so you can
                //      define the construction order of the static members

                // Static constructors are marked as private static
//  Valid assert commented as it was asserting every time we were doing a test
//                Debug.Assert((text == "private static ") || (text == "unsafe private static "));
                // Remove the private as C# doesn't want us to define it - the user could assume it can put something else
                if (unsafeAdded)
                {
                    text = "unsafe static ";
                }
                else
                {
                    text = "static ";
                }
                ITypeReference type = (ITypeReference)methodDeclaration.DeclaringType;
                text += LanguageManager.NameFixup.UnmangleName(type.Name);
            }
            else if (methodDeclaration.Name == "Finalize")
            {
                // Finalize is the "destructor" in .Net
                // It gets called before the object is destroyed by the GC

                // Finalizers are marked protected virtual
//  Valid assert commented as it was asserting every time we were doing a test
//                Debug.Assert((text == "protected override ") || (text == "unsafe protected override "));
                // Remove the modifiers as C# doesn't want us to define it - the user could assume it can put something else
                if (unsafeAdded)
                {
                    text = "unsafe ~";
                }
                else
                {
                    text = "~";
                }
                ITypeReference type = (ITypeReference)methodDeclaration.DeclaringType;
                text += LanguageManager.NameFixup.UnmangleName(type.Name);
                finalizer = true;
            }
            else
            {
                // Any other method
                string methodName = LanguageManager.NameFixup.UnmangleMethodName(methodDeclaration.Name, false);
                MethodType methodType;
                methodName = LanguageManager.NameFixup.ConvertMethodName(methodName, methodDeclaration, out methodType);

                switch (methodType)
                {
                    case MethodType.NORMAL:
                        text += returnValue;
                        text += " ";
                        text += methodName;
                        break;

                    case MethodType.OPERATOR:
                    case MethodType.OPERATOR_FALSE:
                    case MethodType.OPERATOR_TRUE:
                        text += returnValue;
                        text += " operator ";
                        text += methodName;
                        break;

                    case MethodType.OPERATOR_EXPLICIT:
                    case MethodType.OPERATOR_IMPLICIT:
                        // For implicit and explicit conversions, this is different
                        // static implicit operator ReturnType(ContainerType t)
                        // static explicit operator ReturnType(ContainerType t)
                        text += methodName + " operator " + returnValue;
                        break;

                    default:
                        Debug.Fail("Should not be here!");
                        break;
                }

//                text += methodDeclaration.Name;
                // For method, no notion of nested...
                text += GenerateCodeGenericArguments(methodDeclaration.GenericArguments, NestedType.NOT_NESTED);
            }

            mSourceData.Append(text);
            mSourceData.AppendSameLine("(");
            mSourceData.AppendSameLine(parameters.Text);
            mSourceData.AppendSameLine(")");

            IConstructorDeclaration constructorDeclaration = methodDeclaration as IConstructorDeclaration;
            if ((constructorDeclaration != null) && (constructorDeclaration.Initializer != null) && (constructorDeclaration.Initializer.Method != null))
            {
                StringData methodName = LanguageManager.ExpressionGenerator.GenerateCode(constructorDeclaration.Initializer.Method, info);

                bool baseCall = methodName.Text.StartsWith("base");
                if (baseCall && (constructorDeclaration.Initializer.Arguments.Count == 0))
                {
                    // The constructor calls its default base class constructor
                    // There is nothing to add here, it's done by default...
                }
                else
                {
                    // It means that there is a non-default base class constructor to call...

                    if (baseCall)
                    {
                        mSourceData.AppendSameLine(" : base");
                    }
                    else
                    {
//                        Debug.Assert(methodName.Text.StartsWith("this"));
// Deactivated for the moment as in some cases (like constructor or things like that, the value is incorrect)
                        mSourceData.AppendSameLine(" : this");
                    }

                    mSourceData.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCodeMethodInvokeArguments(constructorDeclaration.Initializer.Arguments, info).Text);
                }
            }

            // After defining the method, add the constraints for generic
            mSourceData.Append(GenerateCodeGenericConstraints(methodDeclaration.GenericArguments, NestedType.NOT_NESTED));

            if (body == null)
            {
                mSourceData.AppendSameLine(";\n");
                return;
            }
            mSourceData.AppendSameLine("\n");

            if (finalizer)
            {
                // During the finalizer, remove any call to base.Finalize()
                // usually in a try / catch - so if the finalizer code is failing the base.Finalize is still called
                // The C# compiler will add it back anyway...
                bodyText = bodyText.Replace("base.Finalize();", "");
            }

            FixReflectorBug(body, bodyText, returnValue);
        }

        private static string FixReflectorBugTryCatch(IBlockStatement blockStatement, string bodyText, string returnValue)
        {
            // Fixup incorrect reflector behavior...
            string bugFix = "";
            if ((blockStatement.Statements.Count != 0) && (returnValue != "void"))
            {
                // There is at least one statement, and there is a return value...
                ITryCatchFinallyStatement tryCatchFinallyStatement = blockStatement.Statements[blockStatement.Statements.Count - 1] as ITryCatchFinallyStatement;
                if (tryCatchFinallyStatement != null)
                {
                    // And the last statement IS a try catch... This is certainly a Reflector bug!
                    // Need to add a return of the default value...

                    switch (returnValue)
                    {
                        case "System.Boolean":
                            bugFix = "return (false);\n";
                            break;

                        case "System.Byte":
                        case "System.SByte":
                        case "System.Int32":
                        case "System.UInt32":
                        case "System.Int16":
                        case "System.UInt16":
                        case "System.Int64":
                        case "System.UInt64":
                            bugFix = "return (0);\n";
                            break;

                        case "System.Single":
                        case "System.Double":
                            bugFix = "return (0.0f);\n";
                            break;

                        default:
                            bugFix = "return (null);\n";
                            break;
                    }
                }
            }
            return (bugFix);
        }

        private void FixReflectorBug(IBlockStatement blockStatement, string bodyText, string returnValue)
        {
            // Fixup incorrect reflector behavior...
            string bugFix = FixReflectorBugTryCatch(blockStatement, bodyText, returnValue);

            if (bugFix == "")
            {
                mSourceData.Append(bodyText);
            }
            else
            {
                mSourceData.Append("{\n");
                ++mSourceData.Indentation;
                mSourceData.Append(bodyText);
                mSourceData.Append(bugFix);
                --mSourceData.Indentation;
                mSourceData.Append("}\n");
            }
        }

        private static void FixReflectorBug(IBlockStatement blockStatement, string bodyText, string returnValue, StringData data)
        {
            // Fixup incorrect reflector behavior...
            string bugFix = FixReflectorBugTryCatch(blockStatement, bodyText, returnValue);

            if (bugFix == "")
            {
                data.AppendSameLine(bodyText);
            }
            else
            {
                data.Append("{\n");
                ++data.Indentation;
                data.Append(bodyText);
                data.Append(bugFix);
                --data.Indentation;
                data.Append("}\n");
            }
        }

        public void GenerateCodeEnum(string passedText, ITypeInfo typeInfo)
        {
            ITypeDeclaration typeDeclaration = typeInfo.TypeDeclaration;

            // With enums, everything is done with fields...

            // The first field is the size of the enum, the other fields are each values of the enum...

            int count = typeDeclaration.Fields.Count;

            ParsingInfo info = new ParsingInfo(typeDeclaration);
            string text = LanguageManager.ReferenceGenerator.GenerateCodeType(typeDeclaration.Fields[0].FieldType, info).Text;

            passedText += " : ";
            switch (text.ToLower())
            {
                case "system.byte":
                    passedText += "byte";
                    break;

                case "system.sbyte":
                    passedText += "sbyte";
                    break;

                case "system.int16":
                    passedText += "short";
                    break;

                case "system.uint16":
                    passedText += "ushort";
                    break;

                case "system.int32":
                    passedText += "int";
                    break;

                case "system.uint32":
                    passedText += "uint";
                    break;

                case "system.int64":
                    passedText += "long";
                    break;

                case "system.uint64":
                    passedText += "ulong";
                    break;

                default:
                    Debug.Fail("Should not be here!");
                    break;
            }

            mSourceData.Append("enum " + passedText);
            mSourceData.Append("\n{\n");
            mSourceData.Indentation++;

            // Now parse each enum (skipping the base type)
            for (int i = 1; i < count; ++i)
            {
                IFieldDeclaration fieldDeclaration = typeDeclaration.Fields[i];

                text = LanguageManager.ReferenceGenerator.GenerateCodeEnumDeclaration(fieldDeclaration, info).Text;
                text += ",\n";
                mSourceData.Append(text);
            }

            mSourceData.Indentation--;
            mSourceData.Append("}\n");
        }

        public string CreateAnonymousClass(ITypeInfo declaringType)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }
        public string AddAnonymousMethod(string className, IMethodReturnType returnType, IParameterDeclarationCollection parameters, StringData methodBody, ParsingInfo info)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }
        public void AddFieldForAnonymousMethod(string className, string fieldName, string fieldDeclaration)
        {
            Debug.Fail("Not implemented!");
        }
        public void GenerateAnonymousClasses()
        {
            Debug.Fail("Not implemented!");
        }

        private const string ATTRIBUTE = "Attribute";
        private const string SOURCE_TYPE = ".cs";

        private StringData mSourceData;
    }
}
