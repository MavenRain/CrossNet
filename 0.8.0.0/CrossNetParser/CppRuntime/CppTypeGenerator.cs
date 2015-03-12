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

#define USE_MACRO_FOR_ENUM
//#define GENERATE_ATTRIBUTE

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using Reflector;
using Reflector.CodeModel;
using Reflector.CodeModel.Memory;

using CrossNet.Common;
using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.CppRuntime
{
    public class CppTypeGenerator : ITypeGenerator
    {
        public CppTypeGenerator(Type[] dontGenerateTypes)
        {
            foreach (Type type in dontGenerateTypes)
            {
                mDontGenerateTypes[type.FullName] = type;
            }
        }

        public GeneratedData GenerateCode(IAssembly assembly, AssemblyData assemblyData)
        {
            string outputAssemblyName = assemblyData.AssemblyName;
            string safeOutputAssemblyName = outputAssemblyName.Replace('.', '_');

            StringPool.StartAssembly(safeOutputAssemblyName);
            mStaticConstructorList.Clear();
            mInterfaceWrapperList.Clear();
            mAssemblyTrace.Clear();

            GeneratedData   generatedData = new GeneratedData();
            mClassDeclarationData = new StringData();
            generatedData.AddFile(safeOutputAssemblyName + CLASS_DECLARATION_FILE, mClassDeclarationData);
            mClassDefinitionData = new StringData();
            generatedData.AddFile(safeOutputAssemblyName + CLASS_DEFINITION_FILE, mClassDefinitionData);
            mMethodDefinitionData = new StringData();
            generatedData.AddFile(safeOutputAssemblyName + METHOD_DEFINITION_FILE, mMethodDefinitionData);

            mIncludesData = new StringData();

            // Let's put some headers for each file...
            GenerateHeadersForAssembly(outputAssemblyName, safeOutputAssemblyName);

            StringData backupClassDeclaration = new StringData(mClassDeclarationData);
            StringData backupClassDefinition = new StringData(mClassDefinitionData);
            StringData backupMethodDefinition = new StringData(mMethodDefinitionData);

            // First: Parse all the types...
            IDictionary<ITypeInfo, object> allTypeInfos = new Dictionary<ITypeInfo, object>(1000);  // 1000 types to start
            foreach (IModule module in assembly.Modules)
            {
                ParseTypes(allTypeInfos, module);
            }

            // Now we can generate the code for each type, although it's is a bit tricky
            // as we have to resolve the dependencies at the same time

            // First we detect dependencies only in the passed assembly
            // It means that any other type will _not_ have dependencies set
            foreach (ITypeInfo parsedType in allTypeInfos.Keys)
            {
                parsedType.DetectDependencies();
                parsedType.DependencyCleared = false;
            }

            // Then resolve the dependencies, and write the resolved type as soon as we detect them...

#if !STANDARD_DEP
            bool firstTime = true;
#endif
            for (; ; )
            {
                bool stateChanged = false;
                Stack<ITypeInfo> toWrite = new Stack<ITypeInfo>();

                // Do a 3 step process
                // First detect...
#if !STANDARD_DEP
                if (firstTime)
                {
                    // The first time, we only do the enums so they get out of the way
                    foreach (ITypeInfo parsedType in allTypeInfos.Keys)
                    {
                        if (parsedType.Type != ObjectType.ENUM)
                        {
                            // Not an enum, skip this one...
                            continue;
                        }
                        parsedType.RemainingDependencies = 0;
                        toWrite.Push(parsedType);
                    }
                    // In any case, mark it as changed even if there is no enum, so we can actually update the other types
                    stateChanged = true;
                    firstTime = false;
                }
                else
#endif
                {
                    // The other times, we do all the other types...
                    foreach (ITypeInfo parsedType in allTypeInfos.Keys)
                    {
                        int numDependencies = parsedType.RemainingDependencies;
                        if (numDependencies == 0)
                        {
                            // This type is clear in term of dependency, we can write it
                            toWrite.Push(parsedType);
                            stateChanged = true;
                        }
                    }
                }

                // Then write, clear their dependency status and remove them from the main list
                while (toWrite.Count != 0)
                {
                    ITypeInfo parsedType = toWrite.Pop();
                    GenerateCode(parsedType, NestedType.NOT_NESTED, assemblyData);

                    allTypeInfos.Remove(parsedType);
                    parsedType.RemoveSolvedDependencies();
                    parsedType.DependencyCleared = true;
                }

                if (allTypeInfos.Count == 0)
                {
                    // All the types have been written, we are done!
                    break;
                }

                // Finally on the remaining types, we try to clean a little bit their dependencies
                // It will speed up a little bit the search and free some memory during the process
                foreach (ITypeInfo parsedType in allTypeInfos.Keys)
                {
                    if (parsedType.RemoveSolvedDependencies())
                    {
                        // Dependencies changed, it means that there is some progress...
                        stateChanged = true;
                    }
                }

                if (stateChanged == false)
                {
                    // TODO: Add a real error message...
                    //Debug.Fail("Something bad happened here! It means that the remaining types have a cyclic dependency!");

                    // For example, in System.dll it happens because two classes are using enums of the other class
                    //  One way to solve this would be to generate differently nested types (but is it really worthwhile?)

                    // In any case, if we reach this, we are going to generate the types anyway...
                    // They won't compile in C++ but at least we'll see the other errors before that

                    foreach (ITypeInfo parsedType in allTypeInfos.Keys)
                    {
                        GenerateCode(parsedType, NestedType.NOT_NESTED, assemblyData);
                    }
                    break;
                }
            }

            if (assemblyData.Mode == OutputMode.TwoFilesPerClass)
            {
/*
                mClassDeclarationData = backupClassDeclaration;
                generatedData[safeOutputAssemblyName + CLASS_DECLARATION_FILE] = mClassDeclarationData;
*/
                mClassDefinitionData = backupClassDefinition;
                generatedData[safeOutputAssemblyName + CLASS_DEFINITION_FILE] = mClassDefinitionData;

                mMethodDefinitionData = backupMethodDefinition;
                generatedData[safeOutputAssemblyName + METHOD_DEFINITION_FILE] = mMethodDefinitionData;

                mClassDefinitionData.Append("");
                mClassDefinitionData.Append(mIncludesData);
            }

            GenerateStaticConstructorCalls(safeOutputAssemblyName, assemblyData);
            GenerateInterfaceMaps(safeOutputAssemblyName, assemblyData);
            GenerateAssemblyTrace(safeOutputAssemblyName, assemblyData);
            GenerateAssemblySetupTeardown(safeOutputAssemblyName);
            GenerateAssemblyStringPools(safeOutputAssemblyName);

            // And finally the footers
            GenerateFootersForAssembly(safeOutputAssemblyName);

            return (generatedData);
        }

        private void GenerateAssemblySetupTeardown(string outputAssemblyName)
        {
            string setupMethod = "void " + outputAssemblyName + "__Setup()";
            string teardownMethod = "void " + outputAssemblyName + "__Teardown()";

            // Generate Setup for the assembly...
            mClassDefinitionData.Append(setupMethod + ";\n");

            mMethodDefinitionData.Append(setupMethod + "\n");
            mMethodDefinitionData.Append("{\n");
            mMethodDefinitionData.Indentation++;
            mMethodDefinitionData.Append(outputAssemblyName + "__PopulateInterfaceMaps();\n");
            mMethodDefinitionData.Append(outputAssemblyName + "__RegisterStringPools();\n");
            mMethodDefinitionData.Append(outputAssemblyName + "__StaticConstructors();\n");
            mMethodDefinitionData.Indentation--;
            mMethodDefinitionData.Append("}\n");

            // Generate Teardown for the assembly
            // Currently it doesn't do anything, but it should be calling static destructors (if there is some),
            // and clean the interface map...
            mClassDefinitionData.Append(teardownMethod + ";\n");

            mMethodDefinitionData.Append(teardownMethod + "\n");
            mMethodDefinitionData.Append("{\n");
            mMethodDefinitionData.Indentation++;
            mMethodDefinitionData.Indentation--;
            mMethodDefinitionData.Append("}\n");
        }

        private void GenerateStaticConstructorCalls(string outputAssemblyName, AssemblyData assemblyData)
        {
            // Note: That this function is outside any namespace and is global
            //  There is absolutely no particular reason for that, we might want to find a better name ;)
            string assemblyConstructorCall = "void " + outputAssemblyName + "__StaticConstructors()";

            // Declare the function in the class definitition file
            mClassDefinitionData.Append(assemblyConstructorCall + ";\n");

            // Define the functino in the method definition file
            mMethodDefinitionData.Append(assemblyConstructorCall + "\n");
            mMethodDefinitionData.Append("{\n");
            mMethodDefinitionData.Indentation++;

            if (assemblyData.Mode == OutputMode.TwoFilesPerClass)
            {
                // Add the call to each static constructor
                foreach (StringTypeInfo oneStaticConstructor in mStaticConstructorList)
                {
                    string define = GetCrossNetDefine(oneStaticConstructor.TypeInfo);
                    mMethodDefinitionData.Append("#ifndef " + define + "\n");
                    mMethodDefinitionData.Append(oneStaticConstructor.Text + "();\n");
                    mMethodDefinitionData.Append("#endif\n");
                }
            }
            else
            {
                foreach (StringTypeInfo oneStaticConstructor in mStaticConstructorList)
                {
                    mMethodDefinitionData.Append(oneStaticConstructor.Text + "();\n");
                }
            }

            mMethodDefinitionData.Indentation--;
            mMethodDefinitionData.Append("}\n");
        }

        private void GenerateInterfaceMaps(string outputAssemblyName, AssemblyData assemblyData)
        {
            // Note: That this function is outside any namespace and is global
            //  There is absolutely no particular reason for that, we might want to find a better name ;)
            string assemblyInterfaceMap = "void " + outputAssemblyName + "__PopulateInterfaceMaps()";

            // Declare the function in the class definitition file
            mClassDefinitionData.Append(assemblyInterfaceMap + ";\n");

            // Define the functino in the method definition file
            mMethodDefinitionData.Append(assemblyInterfaceMap + "\n");
            mMethodDefinitionData.Append("{\n");
            mMethodDefinitionData.Indentation++;

            // Add the call to each static constructor
            if (assemblyData.Mode == OutputMode.TwoFilesPerClass)
            {
                foreach (StringTypeInfo oneInterfaceWrapper in mInterfaceWrapperList)
                {
                    string define = GetCrossNetDefine(oneInterfaceWrapper.TypeInfo);
                    mMethodDefinitionData.Append("#ifndef " + define + "\n");
                    mMethodDefinitionData.Append(oneInterfaceWrapper.Text + "();\n");
                    mMethodDefinitionData.Append("#endif\n");
                }
            }
            else
            {
                foreach (StringTypeInfo oneInterfaceWrapper in mInterfaceWrapperList)
                {
                    mMethodDefinitionData.Append(oneInterfaceWrapper.Text + "();\n");
                }
            }

            mMethodDefinitionData.Indentation--;
            mMethodDefinitionData.Append("}\n");
        }

        private void GenerateAssemblyTrace(string outputAssemblyName, AssemblyData assemblyData)
        {
            // Note: That this function is outside any namespace and is global
            //  There is absolutely no particular reason for that, we might want to find a better name ;)
            string assemblyTrace = "void " + outputAssemblyName + "__AssemblyTrace(unsigned char currentMark)";

            // Declare the function in the class definitition file
            mClassDefinitionData.Append(assemblyTrace + ";\n");

            // Define the function in the method definition file
            mMethodDefinitionData.Append(assemblyTrace + "\n");
            mMethodDefinitionData.Append("{\n");
            mMethodDefinitionData.Indentation++;

            // Add the call to each static constructor
            if (assemblyData.Mode == OutputMode.TwoFilesPerClass)
            {
                foreach (StringTypeInfo oneTrace in mAssemblyTrace)
                {
                    string define = GetCrossNetDefine(oneTrace.TypeInfo);
                    mMethodDefinitionData.Append("#ifndef " + define + "\n");
                    mMethodDefinitionData.Append(oneTrace.Text);
                    mMethodDefinitionData.Append("#endif\n");
                }
            }
            else
            {
                foreach (StringTypeInfo oneTrace in mAssemblyTrace)
                {
                    mMethodDefinitionData.Append(oneTrace.Text);
                }
            }

            mMethodDefinitionData.Indentation--;
            mMethodDefinitionData.Append("}\n");
        }

        private void GenerateAssemblyStringPools(string outputAssemblyName)
        {
            // Note: That this function is outside any namespace and is global
            //  There is absolutely no particular reason for that, we might want to find a better name ;)
            string assemblyStringPools = "void " + outputAssemblyName + "__RegisterStringPools()";

            mClassDefinitionData.Append(assemblyStringPools + ";\n");
            StringData stringDeclarations = StringPool.GetStringDeclarations();
            mClassDefinitionData.Append(stringDeclarations);

            // Define the strings in the method definition file
            mMethodDefinitionData.Append(assemblyStringPools + "\n");
            mMethodDefinitionData.Append("{\n");
            mMethodDefinitionData.Indentation++;

            // Add now all the string initializations
            StringData stringInitializations = StringPool.GetStringInitializations();
            mMethodDefinitionData.Append(stringInitializations);

            mMethodDefinitionData.Indentation--;
            mMethodDefinitionData.Append("}\n");

            StringData stringDefinitions = StringPool.GetStringDefinitions();
            mMethodDefinitionData.Append(stringDefinitions);
        }

        private void GenerateHeadersForAssembly(string outputAssemblyName, string safeOutputAssemblyName)
        {
            // Try not to have randome numbers inside the header generation so if we regenerate two times the same assembly
            // We will have exactly the same generated file

            string includeGuard;

            mClassDeclarationData.Append("/*\n");
            mClassDeclarationData.Append("\tHeader generated by CrossNet\n");
            mClassDeclarationData.Append("\tClass declarations for the assembly " + outputAssemblyName + "\n");
            mClassDeclarationData.Append("*/\n");
            includeGuard = GetIncludeGuard(safeOutputAssemblyName, CLASS_DECLARATION_FILE);
            mClassDeclarationData.Append("#ifndef " + includeGuard + "\n");
            mClassDeclarationData.Append("#define " + includeGuard + "\n");
            mClassDeclarationData.Append("\n");

            mClassDefinitionData.Append("/*\n");
            mClassDefinitionData.Append("\tHeader generated by CrossNet\n");
            mClassDefinitionData.Append("\tClass definitions for the assembly " + outputAssemblyName + "\n");
            mClassDefinitionData.Append("*/\n");
            includeGuard = GetIncludeGuard(safeOutputAssemblyName, CLASS_DEFINITION_FILE);
            mClassDefinitionData.Append("#ifndef " + includeGuard + "\n");
            mClassDefinitionData.Append("#define " + includeGuard + "\n");
            mClassDefinitionData.Append("#include \"" + safeOutputAssemblyName + CLASS_DECLARATION_FILE + "\"\n");
            mClassDefinitionData.Append("\n");

            mMethodDefinitionData.Append("/*\n");
            mMethodDefinitionData.Append("\tHeader generated by CrossNet\n");
            mMethodDefinitionData.Append("\tMethod definitions for the assembly " + outputAssemblyName + "\n");
            mMethodDefinitionData.Append("*/\n");
            includeGuard = GetIncludeGuard(safeOutputAssemblyName, METHOD_DEFINITION_FILE);
            mMethodDefinitionData.Append("#ifndef " + includeGuard + "\n");
            mMethodDefinitionData.Append("#define " + includeGuard + "\n");
            mMethodDefinitionData.Append("#include \"" + safeOutputAssemblyName + CLASS_DEFINITION_FILE + "\"\n");
            mMethodDefinitionData.Append("\n");
        }

        private void GenerateHeadersForClass(ITypeInfo typeInfo)
        {
            // Try not to have randome numbers inside the header generation so if we regenerate two times the same assembly
            // We will have exactly the same generated file

            string includePath = GetPathName(typeInfo).Replace('.', '\\');
            includePath = includePath.TrimStart('\\');
            includePath = LanguageManager.NameFixup.UnmangleName(includePath);
            string includeGuard;
            string define = "#ifndef " + GetCrossNetDefine(typeInfo) + "\n";

            mClassDefinitionData.Append("/*\n");
            mClassDefinitionData.Append("\tHeader generated by CrossNet\n");
            mClassDefinitionData.Append("\tClass definitions for the class " + typeInfo.FullName + "\n");
            mClassDefinitionData.Append("*/\n");
            includeGuard = GetIncludeGuard(typeInfo, "_H");
            mClassDefinitionData.Append("#ifndef " + includeGuard + "\n");
            mClassDefinitionData.Append("#define " + includeGuard + "\n");
            mClassDefinitionData.Append("\n");
            mClassDefinitionData.Append(define);

            mMethodDefinitionData.Append("/*\n");
            mMethodDefinitionData.Append("\tHeader generated by CrossNet\n");
            mMethodDefinitionData.Append("\tMethod definitions for the class " + typeInfo.FullName + "\n");
            mMethodDefinitionData.Append("*/\n");
            includeGuard = GetIncludeGuard(typeInfo, "_CPP");
            mMethodDefinitionData.Append("#ifndef " + includeGuard + "\n");
            mMethodDefinitionData.Append("#define " + includeGuard + "\n");
            mMethodDefinitionData.Append("#include \"" + includePath + ".h" + "\"\n");
            mMethodDefinitionData.Append("\n");
            mMethodDefinitionData.Append(define);

            mIncludesData.Append(define);
            mIncludesData.Append("#include \"" + includePath + ".h" + "\"\n");
            mIncludesData.Append("#endif\n");
        }

        private static string GetCrossNetDefine(ITypeInfo typeInfo)
        {
            string fullName = GetPathName(typeInfo);
            string define = "CN_NO_" + LanguageManager.NameFixup.UnmangleName(fullName);
            return (define.ToUpper());
        }

        private void GenerateFootersForAssembly(string outputAssemblyName)
        {
            mClassDeclarationData.Append("\n");
            mClassDeclarationData.Append("#endif\n");

            mClassDefinitionData.Append("\n");
            mClassDefinitionData.Append("#endif\n");

            mMethodDefinitionData.Append("\n");
            mMethodDefinitionData.Append("#endif\n");
        }

        private void GenerateFootersForClass(string outputAssemblyName)
        {
            mClassDefinitionData.Append("\n");
            mClassDefinitionData.Append("#endif\n");
            mClassDefinitionData.Append("#endif\n");

            mMethodDefinitionData.Append("\n");
            mMethodDefinitionData.Append("#endif\n");
            mMethodDefinitionData.Append("#endif\n");
        }

        private string GetIncludeGuard(string outputAssemblyName, string postFix)
        {
            outputAssemblyName = outputAssemblyName.ToUpper();
            int assemblyHashCode = outputAssemblyName.GetHashCode();
            string headerName = (outputAssemblyName + postFix.ToUpper()).Replace('.', '_').Replace(':', '_');
            int headerNameHashCode = headerName.ToUpper().GetHashCode();

            string includeGuard = "__" + headerName + "__" + assemblyHashCode.ToString("X8");
            includeGuard += "__" + headerNameHashCode.ToString("X8") + "_H__";

            return (includeGuard);
        }

        private string GetIncludeGuard(ITypeInfo typeInfo, string postFix)
        {
            string includeGuard = (GetPathName(typeInfo) + postFix).ToUpper();
            includeGuard = "__" + LanguageManager.NameFixup.UnmangleName(includeGuard) + "__";
            return (includeGuard);
        }

        public void GenerateCode(IModule module, AssemblyData assemblyData)
        {
            foreach (ITypeDeclaration typeDeclaration in module.Types)
            {
                ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(typeDeclaration);
                GenerateCode(typeInfo, NestedType.NOT_NESTED, assemblyData);
            }
        }

        public void ParseTypes(IDictionary<ITypeInfo, object> allTypeInfos, IModule module)
        {
            foreach (ITypeDeclaration typeDeclaration in module.Types)
            {
                ITypeInfo typeInfo = ParseType(allTypeInfos, typeDeclaration, NestedType.NOT_NESTED);
            }
        }

        public ITypeInfo ParseType(IDictionary<ITypeInfo, object> allTypeInfos, ITypeDeclaration typeDeclaration, NestedType nested)
        {
            ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(typeDeclaration);

            string typeName = typeInfo.DotNetFullName;
            if (typeInfo.NumGenericArguments != 0)
            {
                typeName += "`" + typeInfo.NumGenericArguments.ToString();
            }

            if (mDontGenerateTypes.ContainsKey(typeName))
            {
                // This type should not be included...
                return (typeInfo);
            }

            // Add all types (nested or not) to the list of types to generate code
            // This way nested types can be generated before their owner type...
            allTypeInfos.Add(typeInfo, null);

            foreach (ITypeDeclaration nestedTypeDeclaration in typeDeclaration.NestedTypes)
            {
                ParseType(allTypeInfos, nestedTypeDeclaration, NestedType.NESTED_STANDARD);
            }
            return (typeInfo);
        }

        public void GenerateCode(ITypeInfo typeInfo, NestedType nested, AssemblyData assemblyData)
        {
            int numNamespaces = 0;
            ITypeDeclaration typeDeclaration = typeInfo.TypeDeclaration;

            if (nested == NestedType.NOT_NESTED)
            {
                // Some specific cases here...
                string nonScopedFullName = typeInfo.NonScopedFullName;
                if (nonScopedFullName == "_Module_")
                {
                    // <Module> is meaningless for us
                    return;
                }
                else if (nonScopedFullName.StartsWith("_PrivateImplementationDetails__"))
                {
                    // <PrivateImplementationDetails>{......} is meaningless for us
                    return;
                }

                if (assemblyData.Mode == OutputMode.TwoFilesPerClass)
                {
                    // When we have to generate 2 files per class, we have to start fresh each file
                    mClassDefinitionData.Replace("");
                    mMethodDefinitionData.Replace("");

                    GenerateHeadersForClass(typeInfo);
                }

                // If not nested, have to add the namespace
                string[] separator = new string[] { "::" };
                string[] namespaces = typeInfo.NonScopedFullName.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                int length = namespaces.Length - 1; // Don't use the class name...
                for (int index = 0 ; index < length ; ++index)
                {
                    string oneNamespace = namespaces[index];
                    string nameSpace = "namespace " + oneNamespace + "\n";
                    mClassDefinitionData.Append(nameSpace);
                    mClassDefinitionData.Append("{\n");
                    ++mClassDefinitionData.Indentation;

                    // Do the same for forward declaration
                    mClassDeclarationData.Append(nameSpace);
                    mClassDeclarationData.Append("{\n");
                    ++mClassDeclarationData.Indentation;
                }
                numNamespaces = length;
            }

            string text = "";

            // Type protection doesn't exist in C++

            // For C++, the template arguments are before the class keyword
            string genericTypeDeclaration = GenerateCodeGenericArguments(typeDeclaration.GenericArguments, nested);
            text += genericTypeDeclaration;

            string baseType = "";
            ObjectType objectType = typeInfo.Type;

            switch (objectType)
            {
                case ObjectType.CLASS:
                    text += "class ";
                    break;

                case ObjectType.DELEGATE:
                    // No delegate keyword in C++ - and anyway we are handling the delegates differently
                    break;

                case ObjectType.ENUM:
                    text += "struct ";  // Enums are actually managed as struct
                    break;

                case ObjectType.INTERFACE:
                    text += "class ";   // C++ doesn't know about interface
                    break;

                case ObjectType.STRUCT:
                    text += "struct ";
                    break;

            }

            if (typeInfo.BaseType != null)
            {
                baseType = typeInfo.BaseType.FullName;
            }

            string declarationName = LanguageManager.NameFixup.UnmangleName(typeInfo.Name);

            if (objectType == ObjectType.DELEGATE)
            {
                GenerateDelegate(typeInfo, declarationName, nested, numNamespaces);

                // Before we leave the method, we restore the indentation and namespace closure...
                if (nested == NestedType.NOT_NESTED)
                {
                    CloseNamespace(numNamespaces);
                }
                else
                {
                    CloseNotNested(objectType, declarationName);
                }

                if (assemblyData.Mode == OutputMode.TwoFilesPerClass)
                {
                    GenerateFootersForClass(typeInfo.DotNetFullName);
                    WriteFiles(typeInfo, assemblyData);
                }
                return;
            }

            text += declarationName;
#if false
            if (typeDeclaration.GenericArguments.Count != 0)
            {
                // It's a generic, add the generic postfix

/*
                // But only if it is the first level of generic though...
                ITypeDeclaration ownerDeclaration = typeDeclaration.Owner as ITypeDeclaration;
                if ((ownerDeclaration == null) || (ownerDeclaration.GenericArguments.Count == 0))
 */
                // With nested types, we always push the full set of generic arguments...
                {
/*
 * Update this comment...
                    // Either there is no owner, or the owner is not a generic itself
                    // So this class is assumed to be the first generic level...
 */
                    text += "__G" + typeInfo.NumGenericArguments.ToString();
                }
            }
#endif

            if (nested == NestedType.NOT_NESTED)
            {
                // Forward declare the class only if it is not nested... Even if it is a generic
                // Add just the class name (with no derivation information)...
                mClassDeclarationData.Append(text + ";\n");
            }

            if (objectType == ObjectType.ENUM)
            {
                GenerateCodeEnum(declarationName, typeInfo);
                // Before we leave the method, we restore the indentation and namespace closure...
                if (nested == NestedType.NOT_NESTED)
                {
                    CloseNamespace(numNamespaces);
                }
                else
                {
                    CloseNotNested(objectType, declarationName);
                }
                if (assemblyData.Mode == OutputMode.TwoFilesPerClass)
                {
                    GenerateFootersForClass(typeInfo.DotNetFullName);
                    WriteFiles(typeInfo, assemblyData);
                }
                return;
            }

            if (objectType == ObjectType.INTERFACE)
            {
                // If that's an interface, it derives from IInterface only
                // We will actually duplicate methods between interfaces (which might be more conbursome to generate)
                // The other solutino is to have virtual base class
                // Beside some possible performance issue, it might creates further issues later by not using the correct pointer.
                text += " : public ::CrossNetRuntime::IInterface";
            }
            else if (objectType == ObjectType.CLASS)
            {
                if (typeInfo.BaseType != null)
                {
                    text += " : public " + typeInfo.BaseType.FullName;
                }
                else
                {
                    text += " : public ::System::Object";
                }
            }

            // After defining the class and its base class / interfaces, add the constraints for generic
            text += GenerateCodeGenericConstraints(typeDeclaration.GenericArguments, nested);

            text += "\n";
            mClassDefinitionData.Append(text);
            mClassDefinitionData.Append("{\n");
            ++mClassDefinitionData.Indentation;

            if ((objectType == ObjectType.INTERFACE) || (objectType == ObjectType.CLASS) || (objectType == ObjectType.STRUCT))
            {
                // For an interface, class or struct, define the IID

                // Currently we define the IID only dynamically
                // later we might let the user define it's own IID...
                // I'm wondering if we could not use attribute for that, this way we could detect at parsing time
                // if they collide...
                //TODO: Investigate that...

                if (typeDeclaration.GenericArguments.Count != 0)
                {
                    // When the class, interface or struct is generic, we need to use the multiple dynamic ID
                    // It's a bit complicated for various reasons...
                    // We need to differentiate:
                    //  - Between interface and object (as they are not on the same range).
                    //  - Between deriving from interface or not (as we can't create an array of size zero in C++).
                    //  - between deriving from another type or not...

                    bool isInterface = (typeInfo.Type == ObjectType.INTERFACE);
                    bool inheritsInterface = false;

                    StringData data = new StringData();

                    // The wrapper types are being declared at the exclusive levels
                    // because we have to determine each specific specialization.

                    // But the wrapper instanciations must be at the union of the interfaces
                    // So each new type has its own wrappers completely overlapping those from the parents
                    // (Otherwise we would need at runtime to take the wrapper from the parent and duplicate them
                    // on each child - if we don't do this then some interfaces are not supported in child classes -
                    // we had this issue earlier when using ExclusiveInterfaces instead).
            
                    IList<ITypeInfo> interfaces = typeInfo.UnionOfInterfaces;

                    if (interfaces.Count != 0)
                    {
                        inheritsInterface = true;
                        StringCollection allWrappers = new StringCollection();
                        foreach (ITypeInfo typeInfoInterface in interfaces)
                        {
                            allWrappers.Add(typeInfoInterface.GetWrapperName(typeInfo.FullName));
                        }

                        data.Append("(\n");
                        ListInfo(typeInfo, allWrappers, data);
                        data.Append(")\n");

                        // 2 commas per interface, except the last one with only only one comma
                        // So fir 2 interfaces, 3 commas, but 4 parameters...
                        int numMacroParameters = 2 * allWrappers.Count;
                        StringData temp = new StringData("__W" + numMacroParameters.ToString() + "__\n");
                        temp.Append(data);
                        data = temp;
                    }

                    if (isInterface && (inheritsInterface == false))
                    {
                        mClassDefinitionData.Append("CN_MULTIPLE_DYNAMIC_INTERFACE_ID0()");
                    }
                    else if (isInterface)
                    {
                        Debug.Assert(inheritsInterface == true);
                        mClassDefinitionData.Append("CN_MULTIPLE_DYNAMIC_INTERFACE_ID(\n");
                        mClassDefinitionData.Append(data);
                        mClassDefinitionData.Append(")\n");
                    }
                    else
                    {
                        Debug.Assert(isInterface == false);

                        string sizeOfTypeName = typeInfo.FullName;
                        if (typeInfo.IsValueType)
                        {
                            sizeOfTypeName = "::CrossNetRuntime::BoxedObject<" + sizeOfTypeName + " >";
                        }

                        if (inheritsInterface)
                        {
                            mClassDefinitionData.Append("CN_MULTIPLE_DYNAMIC_OBJECT_ID(\n");
                            mClassDefinitionData.Append("sizeof(" + sizeOfTypeName + "),\n");
                            mClassDefinitionData.Append(data);
                            mClassDefinitionData.Append(",\n");
                        }
                        else
                        {
                            mClassDefinitionData.Append("CN_MULTIPLE_DYNAMIC_OBJECT_ID0(\n");
                            mClassDefinitionData.Append("sizeof(" + sizeOfTypeName + "),\n");
                        }

                        if (typeInfo.BaseType != null)
                        {
                            mClassDefinitionData.Append(typeInfo.BaseType.FullName + "::__GetInterfaceMap__()\n");
                        }
                        else
                        {
                            mClassDefinitionData.Append("NULL\n");
                        }
                        mClassDefinitionData.Append(")\n");
                    }
                }
                else
                {
                    mClassDefinitionData.Append("CN_DYNAMIC_ID()\n");
                }
            }

            IDictionary removeMembers = new Hashtable();
            IDictionary removeMethods = new Hashtable();

            // Don't list nested types are they are directly generated at the namespace level
/*
            // First list all the nested types, so events, properties, methods, fields can take advantage of them
            NestedType localNestedType = nested;
            if (typeDeclaration.GenericArguments.Count != 0)
            {
                localNestedType = NestedType.NESTED_GENERIC;
            }
            else if (nested == NestedType.NOT_NESTED)
            {
                localNestedType = NestedType.NESTED_STANDARD;
            }
 */

/*
 * Don't parse the nested types anymore as they are now generated at theglobal level...
 * 
            ITypeDeclarationCollection nestedTypes;
            try
            {
                nestedTypes = typeDeclaration.NestedTypes;
            }
            catch
            {
                nestedTypes = null;
            }

            if ((nestedTypes != null) && (nestedTypes.Count > 0))
            {
                // Nested types are public (as the private state is not defined per item like C#).
                mClassDefinitionData.Append("public:\n");
                foreach (ITypeDeclaration nestedTypeDeclaration in nestedTypes)
                {
                    GenerateCode(nestedTypeDeclaration, localNestedType);
                }
            }
 */

            // List all the events
            // We have to do this first as it will invalidate some members and some methods
            foreach (IEventDeclaration eventDeclaration in typeDeclaration.Events)
            {
                // Are all the events public?
                ParsingInfo info = new ParsingInfo(typeDeclaration);
#if DEBUG
                info.DebugMethodName = eventDeclaration.Name;
#endif

                string eventText = LanguageManager.ReferenceGenerator.GenerateCodeEventDeclaration(eventDeclaration, info).Text;
                if (eventText.IndexOf(".") != -1)
                {
                    // Reflector hack...
                    // When overriding explicitly an interface, Reflector will return the name of the event with the full interface scope
                    // with "." and sometime with "+" as well
                    // If that's the case, we just skip this event... The methods will be generated correctly anyway
                    // And the real event field has already been created added (just with a different name?).
                    continue;
                }

                text = "public:\n";
                text += eventText;
                text += ";\n";
                mClassDefinitionData.Append(text);

                // Remove the name of the event (can't have a field named the same way...)
                removeMembers.Add(eventDeclaration.Name, null);

/*
 * In the case of the C++ events, we actually want to keep the methods... The major reason is because 
                removeMethods[eventDeclaration.AddMethod] = null;
                removeMethods[eventDeclaration.RemoveMethod] = null;
 */
            }

/*
            foreach (IPropertyDeclaration propertyDeclaration in typeDeclaration.Properties)
            {
                bool isInterface = (objectType == ObjectType.INTERFACE);
                GenerateProperty(isInterface, typeDeclaration, propertyDeclaration, objectType, removeMethods, false);
            }
 */

            // List all the fields
            // The fields must be parsed BEFORE the methods so this way the constructor and static constructor
            // Can be initialized with the correct field initialization...
            StringData fieldInitialization = null;
            StringData staticFieldInitialization = null;

            StringData traceMethodImplementation = new StringData();

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
                        text += "public:\n";
                        break;
                    case FieldVisibility.PrivateScope:
                        // It could be used by nested classes, so we cannot use private
                        // (if they are not marked explicitly as public)
                        // Use public for the moment...
                        text += "public:\n";
                        break;
                    case FieldVisibility.Private:
                        text += "public:\n";
                        break;
                    case FieldVisibility.Family:
                        //text += "protected:\n";
                        // I'm not sure to know what faniliy means, but it could mean nested classes and sub-classes
                        // Assume it is public for the moment...
                        text += "public:\n";
                        break;
                    case FieldVisibility.Assembly:
                        //text += "internal ";
                        text += "public:\n";
                        break;
                    case FieldVisibility.FamilyAndAssembly:
                    // Don't know what this one gives, but assume same as NestedFamilyOrAssembly
                    case FieldVisibility.FamilyOrAssembly:
                        //text += "internal protected ";
                        // The notion of internal doesn't exist in C++, we'll then use public
                        text += "public:\n";
                        break;
                }

                if (fieldDeclaration.Literal)
                {
/*
 * 
 * We are actually not defining it as const (the C# compiler will take of the checks for us anyway).
 * This is open for further optimization. Note that this is valid only for integer types.
 * Floats for example can't be static const and defined inside the class definition.
 * 
 * TODO: Add the optimization for integer type...
 * 
                    text += "static const ";
 * 
 */
                    text += "static ";
                }
                else
                {
                    if (fieldDeclaration.Static)
                    {
                        text += "static ";
                    }
                    // Don't use "else if" here as variables can be static AND read only...
                    if (fieldDeclaration.ReadOnly)
                    {
                        /*
                         * readonly doesn't exist in C++...
                         * 
                        text += "readonly ";
                         */
                    }
                }
                ParsingInfo info = new ParsingInfo(typeDeclaration);
#if DEBUG
                info.DebugMethodName = fieldDeclaration.Name;
#endif

                info.ParsingField = true;

                // Initialize them to null, so we know when we want to add them or not...
                // We could directly appned them in plae, but then we would lose the finer control if needed
                info.FieldInitialization = null;
                info.StaticFieldInitialization = null;
                StringData field = LanguageManager.ReferenceGenerator.GenerateCodeFieldDeclaration(fieldDeclaration, info);

                // Start GC specific
                ITypeInfo fieldTypeInfo = field.LocalType.GetTypeInfo();
                if ((fieldTypeInfo != null) || (field.EmbeddedType is IArrayType))
                {
                    // If we can find a type info OR it's an array
                    string fieldName = LanguageManager.NameFixup.UnmangleName(fieldDeclaration.Name);
                    if (fieldDeclaration.Static)
                    {
                        // If the field is static, we have to prefix with the type name
                        fieldName = typeInfo.FullName + "::" + fieldName;
                    }

                    string callTrace;
                    if ((fieldTypeInfo != null) && (fieldTypeInfo.IsValueType))
                    {
                        if (fieldTypeInfo.IsPrimitiveType)
                        {
                            // We skip the primitive type (as they don't have Trace method)
                            callTrace = "";
                        }
                        else
                        {
                            // For value type, we are actually calling Trace directly without test, nor vtable
                            callTrace = fieldName + ".__Trace__(currentMark);\n";
                        }
                    }
                    else
                    {
                        // For non value type, we have to test an vtable
                        // GCManager does that for us though...
                        callTrace = "::CrossNetRuntime::GCManager::Trace(";
                        callTrace += fieldName + ", currentMark);\n";
                    }

                    if (callTrace != String.Empty)
                    {
                        if (fieldDeclaration.Static)
                        {
                            // If that's static, we add that to the assembly Trace method

                            mAssemblyTrace.Add(new StringTypeInfo(callTrace, typeInfo));
                        }
                        else
                        {
                            // If that's not static, we add that to the current implementation
                            traceMethodImplementation.Append(callTrace);
                        }
                    }
                }
                else if (field.EmbeddedType is IGenericArgument)
                {
                    // Tracing with generic argument
                    // We currently add non-static AND static parameters to the trace for each member...
                    // Later we will optimize the tracing of the static parameters
                    string fieldName = LanguageManager.NameFixup.UnmangleName(fieldDeclaration.Name);
                    string fieldType = field.LocalType.FullName;

//                    string callTrace = "::CrossNetRuntime::Tracer<" + fieldType + " >::DoTrace(";
                    string callTrace = "::CrossNetRuntime::Tracer::DoTrace(";
                    callTrace += "currentMark, " + fieldName + ");\n";
                    traceMethodImplementation.Append(callTrace);
                }
                // End GC specific

                if (info.FieldInitialization != null)
                {
                    StringData temp = info.FieldInitialization;
                    temp = Util.CombineStatements(temp, info);
                    if (fieldInitialization != null)
                    {
                        fieldInitialization.Append(temp);
                    }
                    else
                    {
                        fieldInitialization = temp;
                    }
                    info.FieldInitialization = null;    // Set it back to null to make sure we are not changing the global StringData without knowing it
                }

                if (info.StaticFieldInitialization != null)
                {
                    StringData temp = info.StaticFieldInitialization;
                    temp = Util.CombineStatements(temp, info);
                    if (staticFieldInitialization != null)
                    {
                        staticFieldInitialization.Append(temp);
                    }
                    else
                    {
                        staticFieldInitialization = temp;
                    }
                    info.StaticFieldInitialization = null;    // Set it back to null to make sure we are not changing the global StringData without knowing it
                }

                text += field.Text + ";\n";

                mClassDefinitionData.Append(text);
 
                if (fieldDeclaration.Static)
                {
                    string fieldType = LanguageManager.ReferenceGenerator.GenerateCodeTypeWithPostfix(fieldDeclaration.FieldType, info).Text;
/*
 * This code should be used for further optimization, for the moment, we are not supporting it...
 *
                    if (fieldDeclaration.Literal && (fieldType == "System::Int32"))
                    {
                        // If it is a const static int32, this is particular...
                        // We actually initialize the value during the declaration (or actually we try to ;)
                        // By consequence, declaration is definition as well, there is no need of another definition
                    }
                    else
 */
                    {
                        string fieldName = LanguageManager.NameFixup.GetSafeName(fieldDeclaration.Name);

                        mMethodDefinitionData.Append(genericTypeDeclaration);
                        mMethodDefinitionData.Append(fieldType + " " + typeInfo.NonScopedFullName + "::" + fieldName + ";\n");
                    }
                }
            }

            // After we got all the fields, we can now do the proper implementation for the Trace() method
            if (typeInfo.IsValueType)
            {
                // This method should mostly be inlined, unfortunately due to dependency reasons it is impossible 
                // We would have to know that each member derives from System::Object
                // For the moment we'll put in on the cpp file, and we'll let the compiler decide...
                mClassDefinitionData.Append("void __Trace__(unsigned char currentMark);\n");
            }
            else
            {
                mClassDefinitionData.Append("virtual void __Trace__(unsigned char currentMark);\n");
            }

            mMethodDefinitionData.Append(genericTypeDeclaration);
            mMethodDefinitionData.Append("void " + typeInfo.FullName + "::__Trace__(unsigned char currentMark)\n");
            mMethodDefinitionData.Append("{\n");
            mMethodDefinitionData.Indentation++;
            mMethodDefinitionData.Append(traceMethodImplementation);
            mMethodDefinitionData.Indentation--;
            mMethodDefinitionData.Append("}\n");

            // Finally list all the methods
            // It is important to list the methods after the fields as field initialization is pushed down
            //  To the constructor and the static constructor...
            IDictionary<string, string> methodsAdded = new Dictionary<string, string>();
            bool defaultConstructor = false;
            bool toStringDeclared = false;
            bool equalsDeclared = false;
            bool getHashCodeDeclared = false;
            bool staticConstructor = false;

            foreach (IMethodDeclaration methodDeclaration in typeDeclaration.Methods)
            {
                // Here we assume that method reference and method declaration are the same object
                // This is not necessarily true... But should be with Reflector
                if (removeMethods.Contains(methodDeclaration))
                {
                    // We have to skip this event related method...
                    continue;
                }
                ParsingInfo methodInfo = new ParsingInfo(typeDeclaration);
#if DEBUG
                methodInfo.DebugMethodName = methodDeclaration.Name;
#endif
                methodInfo.FieldInitialization = fieldInitialization;
                methodInfo.StaticFieldInitialization = staticFieldInitialization;

                string methodSignature;
                string fullSignature;
                methodSignature = GenerateCodeMethod(typeInfo, methodDeclaration, objectType, methodInfo, null, MethodGeneration.ParseAndGenerate, assemblyData.GenerateImplementation, out fullSignature);

                // In some case because of incorrect parsing, two methods will have the same signature
                // In this case we don't use Add but directly the indexer
                // That way the parser will still continue and we will have a C++ compiler error later
                // But at least this will let us investigate the issue...
                methodsAdded[fullSignature] = fullSignature;

                if (objectType == ObjectType.CLASS)
                {
                    if (defaultConstructor == false)
                    {
                        if (methodSignature == "__ctor__()")
                        {
                            // If that's a class and a default constructor is defined
                            // mark it
                            defaultConstructor = true;
                        }
                    }
                }
                else if (objectType == ObjectType.STRUCT)
                {
                    if (toStringDeclared == false)
                    {
                        if (methodSignature == "ToString()")
                        {
                            toStringDeclared = true;
                        }
                    }
                    if (equalsDeclared == false)
                    {
                        // Don't use simple string comparison as the parameter name can be different...
                        if (    (methodDeclaration.Name == "Equals")
                            &&  (methodDeclaration.Parameters.Count == 1))
                        {
                            LocalType param = LanguageManager.LocalTypeManager.GetLocalType(methodDeclaration.Parameters[0].ParameterType);
                            if (param.Same(LanguageManager.LocalTypeManager.TypeObject))
                            {
                                equalsDeclared = true;
                            }
                        }
                    }
                    if (getHashCodeDeclared == false)
                    {
                        if (methodSignature == "GetHashCode()")
                        {
                            getHashCodeDeclared = true;
                        }
                    }
                }

                if (staticConstructor == false)
                {
                    if (methodSignature == "Static__ctor__()")
                    {
                        staticConstructor = true;
                    }
                }
            }

            string beforeText, afterText;

            if ((staticFieldInitialization != null) && (staticFieldInitialization.Text != ""))
            {
                if (staticConstructor == false)
                {
                    // We have to create the static constructor (both in the declaration AND the definition)

                    beforeText = "void ";
                    string methodName = "Static__ctor__";
                    afterText = methodName + "()";

                    mClassDefinitionData.Append("public:\n");
                    mClassDefinitionData.Append("static " + beforeText + afterText + ";\n");

                    mMethodDefinitionData.Append(beforeText + typeInfo.NonScopedFullName + "::" + afterText + "\n");
                    mMethodDefinitionData.Append("{\n");
                    mMethodDefinitionData.Indentation++;

                    if (typeInfo.IsGeneric)
                    {
                        // If the type is generic, we actually call the static constructor differently (at each construction)
                        // And as such it can be called several times...
                        AddStaticConstructorCheckForGenerics(mMethodDefinitionData);
                    }
                    else
                    {
                        mStaticConstructorList.Add(new StringTypeInfo(typeInfo.FullName + "::" + methodName, typeInfo));
                    }

                    mMethodDefinitionData.Append(staticFieldInitialization);
                    mMethodDefinitionData.Indentation--;
                    mMethodDefinitionData.Append("}\n");
                }
            }
            else if (typeInfo.IsGeneric && (staticConstructor == false))
            {
                // If generic and no static constructor, create a fake one
                // The reason is that static constructors are _always_ called for each construction
                // (a flag make sure that actual code is executed only the first time)
                // But if there is no static constrcutor impleemntation, we have to provide one
                // Just do it in the header, as the implementation is empty in this case...

                mClassDefinitionData.Append("public:\n");
                mClassDefinitionData.Append("void Static__ctor__() {}\n");
            }

            if (objectType == ObjectType.STRUCT)
            {
                // If that's a structure, we need to add the default constructor!
                // Beside setting all the members to zero, it doesn't do anything special
                // In any case, the user can't declare it... So there won't be any conflict...
                // And this method seems to ne always public...
                mClassDefinitionData.Append("public:\n");
                mClassDefinitionData.Append("CROSSNET_FINLINE\n");

                afterText = typeInfo.Name;
//                if (typeInfo.IsFirstLevelGeneric)
/*
                if (typeInfo.NumGenericArguments != 0)
                {
                    afterText += "__G" + typeInfo.NumGenericArguments.ToString();
                }
 */
                afterText += "()";
                mClassDefinitionData.Append(afterText + ";\n");
                mClassDefinitionData.Append("void __ctor__();\n");

                mMethodDefinitionData.Append(genericTypeDeclaration);
                mMethodDefinitionData.Append(typeInfo.NonScopedFullName + "::" + afterText + "\n");
                mMethodDefinitionData.Append("{\n");
                mMethodDefinitionData.Indentation++;
                mMethodDefinitionData.Append("__ctor__();\n");
                mMethodDefinitionData.Indentation--;
                mMethodDefinitionData.Append("}\n");

                mMethodDefinitionData.Append(genericTypeDeclaration);
                mMethodDefinitionData.Append("void " + typeInfo.NonScopedFullName + "::__ctor__()\n");
                mMethodDefinitionData.Append("{\n");
                mMethodDefinitionData.Indentation++;
                mMethodDefinitionData.Append("__memclear__(this, sizeof(*this));\n");
                mMethodDefinitionData.Indentation--;
                mMethodDefinitionData.Append("}\n");

                ParsingInfo info = new ParsingInfo(typeDeclaration);
                string instanceText = typeInfo.GetInstanceText(info);

                // We also need to add an operator ->, so generics work fine with the wrapper code
                // If we had a way to differentiate struct from classes, we maybe would not have to do this... 
                // If we want to avoid boxing, we need to do this...
                // Otherwise the only other solution is to box but in that case, primitive types / structutres suffer a big performance hit
                mClassDefinitionData.Append("public:\n");
                mClassDefinitionData.Append("CROSSNET_FINLINE\n");
                mClassDefinitionData.Append(instanceText + "* ");  // Force the pointer, even on the struct
                mClassDefinitionData.AppendSameLine(" operator ->()\n");
                mClassDefinitionData.Append("{\n");
                mClassDefinitionData.Append(1, "return (this);\n");
                mClassDefinitionData.Append("}\n");

                if (toStringDeclared == false)
                {
                    // There is no ToString() method defined, so provide a basic implementation
                    // Classes don't have this issue as they derive from System::Object

                    // TODO: Change this...
                    //  In reality, we should call GetType()->get_FullName() or something like that...
                    mClassDefinitionData.Append("public:\n");
                    mClassDefinitionData.Append("CROSSNET_FINLINE\n");

                    beforeText = "::System::String * ";
                    afterText = "ToString()";

                    mClassDefinitionData.Append(beforeText + afterText + ";\n");

                    mMethodDefinitionData.Append(genericTypeDeclaration);
                    mMethodDefinitionData.Append(beforeText + typeInfo.NonScopedFullName + "::" + afterText + "\n");
                    mMethodDefinitionData.Append("{\n");
                    mMethodDefinitionData.Indentation++;
                    mMethodDefinitionData.Append("return (::System::String::__Create__(L\"");
                    mMethodDefinitionData.AppendSameLine(instanceText);
                    mMethodDefinitionData.AppendSameLine("\"));\n");
                    mMethodDefinitionData.Indentation--;
                    mMethodDefinitionData.Append("}\n");
                }

                if (equalsDeclared == false)
                {
                    mClassDefinitionData.Append("public:\n");
                    mClassDefinitionData.Append("CROSSNET_FINLINE\n");

                    beforeText = "::System::Boolean ";
                    afterText = "Equals(::System::Object * obj)";

                    mClassDefinitionData.Append(beforeText + afterText + ";\n");

                    mMethodDefinitionData.Append(genericTypeDeclaration);
                    mMethodDefinitionData.Append(beforeText + typeInfo.NonScopedFullName + "::" + afterText + "\n");
                    mMethodDefinitionData.Append("{\n");
                    mMethodDefinitionData.Indentation++;
                    mMethodDefinitionData.Append("return (::CrossNetRuntime::StructEquals(this, obj));\n");
                    mMethodDefinitionData.Indentation--;
                    mMethodDefinitionData.Append("}\n");
                }

                if (getHashCodeDeclared == false)
                {
                    mClassDefinitionData.Append("public:\n");
                    mClassDefinitionData.Append("CROSSNET_FINLINE\n");

                    beforeText = "::System::Int32 ";
                    afterText = "GetHashCode()";

                    mClassDefinitionData.Append(beforeText + afterText + ";\n");

                    mMethodDefinitionData.Append(genericTypeDeclaration);
                    mMethodDefinitionData.Append(beforeText + typeInfo.NonScopedFullName + "::" + afterText + "\n");
                    mMethodDefinitionData.Append("{\n");
                    mMethodDefinitionData.Indentation++;
                    mMethodDefinitionData.Append("return (::CrossNetRuntime::StructGetHashCode(this));\n");
                    mMethodDefinitionData.Indentation--;
                    mMethodDefinitionData.Append("}\n");
                }
            }
            else if (objectType == ObjectType.CLASS)
            {
                if ((defaultConstructor == false) && (typeDeclaration.Abstract == false))
                {
                    // It was a class and there was no default constructor...
                    // And the class is not abstract... We have to create one
                    mClassDefinitionData.Append("public:\n");
                    mClassDefinitionData.Append("static ");
                    ParsingInfo info = new ParsingInfo(typeDeclaration);

                    beforeText = typeInfo.GetInstanceText(info) + " ";
                    afterText = "__Create__()";

                    mClassDefinitionData.AppendSameLine(beforeText + afterText + ";\n");

                    mMethodDefinitionData.Append(genericTypeDeclaration);
                    mMethodDefinitionData.Append(beforeText + typeInfo.NonScopedFullName + "::" + afterText + "\n");
                    mMethodDefinitionData.Append("{\n");
                    mMethodDefinitionData.Indentation++;
                    mMethodDefinitionData.Append(typeInfo.GetInstanceText(info) + " __temp__ = new ");
                    mMethodDefinitionData.AppendSameLine(typeInfo.FullName);

                    mMethodDefinitionData.AppendSameLine(";\n");
                    mMethodDefinitionData.Append("CROSSNET_ASSERT(__GetInterfaceMap__() != NULL, \"Interface map not set correctly!\");\n");
                    mMethodDefinitionData.Append("__temp__->m__InterfaceMap__ = __GetInterfaceMap__();\n");
                    mMethodDefinitionData.Append("return (__temp__);\n");
                    mMethodDefinitionData.Indentation--;
                    mMethodDefinitionData.Append("}\n");
                }
            }

            if (objectType == ObjectType.INTERFACE)
            {
                // It's an interface, we have to look at each base interface, and add the method if it doesn't exist...

                IList<ITypeInfo> interfaces = new List<ITypeInfo>();
                interfaces.Add(typeInfo);

                // But we don't want a method that has been already added to be added again... (if for example the interface hides some methods of the base interfaces)

                // TODO:    Change this to remove the collision checks as this is not needed...
                IList<DiscoveredMethod> discoveredMethods = DiscoverInterfaceMethods(interfaces, null, fieldInitialization, staticFieldInitialization, methodsAdded);
                foreach (DiscoveredMethod oneMethod in discoveredMethods)
                {
                    ITypeInfo interfaceTypeInfo = oneMethod.TypeInfo;
                    IMethodDeclaration methodDeclaration = oneMethod.MethodDeclaration;
                    ParsingInfo methodInfo = oneMethod.ParsingInfo;
                    string fullSignature;
                    GenerateCodeMethod(interfaceTypeInfo, methodDeclaration, ObjectType.INTERFACE, methodInfo, null, MethodGeneration.ParseAndGenerate, assemblyData.GenerateImplementation, out fullSignature);
                }

                if (typeDeclaration.GenericArguments.Count == 0)
                {
                    // Define the static interface map _only_ for non templated types
                    // (as they are using multiple dynamic iids and therefore don't use one single global interface map).
                    mMethodDefinitionData.Append("void * * " + typeInfo.NonScopedFullName + "::s__InterfaceMap__ = NULL;\n");

                    mClassDefinitionData.Append("public:\n");
                    mClassDefinitionData.Append("static void __CreateInterfaceMap__();\n");

                    string interfaceWrapper = typeInfo.NonScopedFullName + "::" + "__CreateInterfaceMap__";
                    mMethodDefinitionData.Append("void " + interfaceWrapper + "()\n");
                    mMethodDefinitionData.Append("{\n");
                    mMethodDefinitionData.Indentation++;

                    // TODO: Add support for all the base interfaces...
                    mMethodDefinitionData.Append("void * * interfaceMap = ::CrossNetRuntime::InterfaceMapper::RegisterInterface();\n");
                    mMethodDefinitionData.Append(typeInfo.FullName + "::s__InterfaceMap__ = interfaceMap;\n");

                    mMethodDefinitionData.Indentation--;
                    mMethodDefinitionData.Append("}\n");

                    mInterfaceWrapperList.Add(new StringTypeInfo(interfaceWrapper, typeInfo));
                }
            }
            else
            {
                // If it's not an interface, we actually have to create the wrappers for each interface

                removeMethods = new Hashtable();

                StringCollection allWrappers = new StringCollection();

                // When listing the interfaces for the instanciation of the wrappers, we need to use the union of interfaces
                // This makes the runtime code simpler
                // TODO: Revisit this...
                foreach (ITypeInfo typeInfoInterface in typeInfo.UnionOfInterfaces)
                {
                    allWrappers.Add(typeInfoInterface.GetWrapperName(typeInfo.FullName));
                }

                // And now look at each exclusive interfaces and detect how many methods override the interface
                foreach (ITypeInfo typeInfoInterface in typeInfo.ExclusiveInterfaces)
                {
                    string wrapperName = typeInfoInterface.GetWrapperName(typeInfo.FullName);

                    text = "class " + wrapperName;
                    text += " : public ";

                    if ((typeInfo.BaseType != null) && (typeInfo.BaseType.UnionOfInterfaces.IndexOf(typeInfoInterface) >= 0))
                    {
                        // The base type already implements the interface, so derive from the base class wrapper to have the default implementation
                        text += typeInfo.BaseType.FullName + "::" + typeInfoInterface.GetWrapperName(typeInfo.FullName);
                        text += "\n";
                    }
                    else
                    {
                        text += typeInfoInterface.FullName + "\n";
                    }

                    mClassDefinitionData.Append(text);
                    mClassDefinitionData.Append("{\n");
                    mClassDefinitionData.Indentation++;
                    mClassDefinitionData.Append("public:\n");

                    ITypeDeclaration currentInterfaceTypeDeclaration = typeInfoInterface.TypeDeclaration;

                    // List all the methods, in that case we want to catch all the methods coming from the interfaces
                    // At the same time this is going to wrap all the properties and events directly

/*
 * This is actually wrong... We just look for the method in the interface itself... There is not even a collision to detect
 * 
                    // Here we need to discover all the methods from the base interfaces plus this particular interface
                    IList<ITypeInfo> interfaces = new List<ITypeInfo>(typeInfoInterface.UnionOfInterfaces);
                    interfaces.Add(typeInfoInterface);
 */

                    IList<ITypeInfo> interfaces = new List<ITypeInfo>();
                    interfaces.Add(typeInfoInterface);

                    // TODO: Change this to get rid of the method discovery...
                    //  Also in reality we want to add only the methods that can potentially override the interface 
                    IList<DiscoveredMethod> discoveredMethods = DiscoverInterfaceMethods(interfaces, typeInfoInterface, fieldInitialization, staticFieldInitialization, null);
                    foreach (DiscoveredMethod oneMethod in discoveredMethods)
                    {
                        ITypeInfo interfaceTypeInfo = oneMethod.TypeInfo;
                        IMethodDeclaration methodDeclaration = oneMethod.MethodDeclaration;
                        ParsingInfo methodInfo = oneMethod.ParsingInfo;

                        // Here we assume that method reference and method declaration are the same object
                        // This is not necessarily true... But should be with Reflector
                        if (removeMethods.Contains(methodDeclaration))
                        {
                            // We have to skip this event related method...
                            continue;
                        }

                        string methodName = FindMethodNameInWrapper(typeInfo, methodDeclaration);
                        if (methodName == "")
                        {
                            // We could not find the corresponding name, it means that this method is not overriden by the implementation
                            // There is nothing to generate here...
                            continue;
                        }

                        string methodSignature;
                        string fullSignature;

                        // If the interface is a specific implementation of a generic,
                        // template elements are going to be prefixed with "typename"
                        // Unfortunately this is invalid if the containing type is not templated
                        // So let's detect the case and invalidate "typename" as needed
                        // It is valid, as it would mean that everything should be specialized and no real generic type
                        if (typeInfo.IsGeneric == false)
                        {
                            methodInfo.EnableTypenameForCodeType = false;
                        }

                        methodSignature = GenerateCodeMethod(interfaceTypeInfo, methodDeclaration, objectType, methodInfo, typeDeclaration, MethodGeneration.ParseAndGenerate, assemblyData.GenerateImplementation, out fullSignature);

                        // Put it back to true as it's the normal value (to avoid side effects)
                        methodInfo.EnableTypenameForCodeType = true;
                    }

                    // Before we finish with the wrapper, we define the corresponding new / delete operator
                    mClassDefinitionData.Append("CN__NEW_DELETE_OPERATORS_FOR_WRAPPER\n");

                    mClassDefinitionData.Indentation--;
                    mClassDefinitionData.Append("};\n");
                }

                //if (allWrappers.Count != 0)
                {
                    // We have to generate the code to fill the interface map!

                    // Add the member for the interface map

/*
                    mClassDefinitionData.Append("public:\n");
                    mClassDefinitionData.Append("static void * * s__InterfaceMap__;\n");
*/

/*
                    mMethodDefinitionData.Append(genericArguments);
                    mMethodDefinitionData.Append("void * * " + typeInfo.FullName + "::s__InterfaceMap__ = NULL;\n");
*/

                    if (typeDeclaration.GenericArguments.Count == 0)
                    {
                        // Define the static interface map _only_ for non templated types
                        // (as they are using multiple dynamic iids and therefore don't use one single global interface map).
                        mMethodDefinitionData.Append("void * * " + typeInfo.NonScopedFullName + "::s__InterfaceMap__ = NULL;\n");

                        // now we can generate the method...

                        mClassDefinitionData.Append("public:\n");
                        mClassDefinitionData.Append("static void __CreateInterfaceMap__();\n");

                        string interfaceWrapper = typeInfo.NonScopedFullName + "::" + "__CreateInterfaceMap__";
                        mMethodDefinitionData.Append("void " + interfaceWrapper + "()\n");
                        mMethodDefinitionData.Append("{\n");
                        mMethodDefinitionData.Indentation++;

                        string initArray;
                        if (allWrappers.Count != 0)
                        {
                            mMethodDefinitionData.Append("::CrossNetRuntime::InterfaceInfo info[] = \n");
                            mMethodDefinitionData.Append("{\n");
                            ListInfo(typeInfo, allWrappers, mMethodDefinitionData);
                            mMethodDefinitionData.Append("};\n");
                            initArray = "info";
                        }
                        else
                        {
                            initArray = "NULL";
                        }

                        string baseTypeInterfaceMap = "";
                        if (typeInfo.BaseType != null)
                        {
                            baseTypeInterfaceMap = ", " + typeInfo.BaseType.FullName + "::__GetInterfaceMap__()";
                        }

                        string sizeOfTypeName = typeInfo.FullName;
                        if (typeInfo.IsValueType)
                        {
                            sizeOfTypeName = "::CrossNetRuntime::BoxedObject<" + sizeOfTypeName + " >";
                        }

                        mMethodDefinitionData.Append("void * * interfaceMap = ::CrossNetRuntime::InterfaceMapper::RegisterObject(sizeof(" + sizeOfTypeName + "), " + initArray + ", ");
                        mMethodDefinitionData.AppendSameLine(allWrappers.Count.ToString() + baseTypeInterfaceMap + ");\n");
                        mMethodDefinitionData.Append(typeInfo.FullName + "::s__InterfaceMap__ = interfaceMap;\n");

                        mMethodDefinitionData.Indentation--;
                        mMethodDefinitionData.Append("}\n");

                        mInterfaceWrapperList.Add(new StringTypeInfo(interfaceWrapper, typeInfo));
                    }
                }
            }

            // Before we close the class definition, create the operators...
            switch (typeInfo.Type)
            {
                case ObjectType.STRUCT:
                case ObjectType.ENUM:
                    // Struct and enum are on the stack and not on the heap
                    // Except when they are embedded in an array...
                    mClassDefinitionData.Append("CN__NEW_DELETE_OPERATORS_FOR_VALUE_TYPE\n");
                    break;

                default:
                    // All the other types are deriving from a base type
                    // So the new / delete policy is inherited
                    break;
            }

            // If there are some anonymous classes defined in this class, generate the corresponding code
            GenerateAnonymousClasses();

            --mClassDefinitionData.Indentation;
            mClassDefinitionData.Append("};\n");
            if (nested == NestedType.NOT_NESTED)
            {
                CloseNamespace(numNamespaces);
            }
            else
            {
                CloseNotNested(objectType, declarationName);
            }

            if (assemblyData.Mode == OutputMode.TwoFilesPerClass)
            {
                GenerateFootersForClass(typeInfo.DotNetFullName);
                WriteFiles(typeInfo, assemblyData);
            }
        }

        private static string GetPathName(ITypeInfo typeInfo)
        {
            string fullTypeName = typeInfo.DotNetFullName;
            if (typeInfo.TypeDeclaration.Owner is ITypeReference)
            {
                ITypeInfo ownerType = TypeInfoManager.GetTypeInfo((ITypeReference)(typeInfo.TypeDeclaration.Owner));
                while (ownerType.TypeDeclaration.Owner != null)
                {
                    string generic = "";
                    if (ownerType.NumGenericArguments != 0)
                    {
                        generic += "__G" + ownerType.NumGenericArguments.ToString();
                    }

                    if (ownerType.TypeDeclaration.Owner is ITypeReference == false)
                    {
                        fullTypeName = ownerType.TypeDeclaration.Namespace + "." + ownerType.TypeDeclaration.Name + generic + "." + fullTypeName;
                        break;
                    }
                    fullTypeName = ownerType.TypeDeclaration.Name + generic + "." + fullTypeName;
                    ownerType = TypeInfoManager.GetTypeInfo((ITypeReference)(ownerType.TypeDeclaration.Owner));
                }
            }

            if (typeInfo.NumGenericArguments != 0)
            {
                fullTypeName += "__G" + typeInfo.NumGenericArguments.ToString();
            }
            // We might be able to remove this replace step by making sure we add "." only when necessary (might be redundant as well ;)
            fullTypeName = fullTypeName.Replace("..", ".").Replace('<', '.').Replace('>', '.');
            return (fullTypeName);
        }

        private void WriteFiles(ITypeInfo typeInfo, AssemblyData assemblyData)
        {
            string fullTypeName = GetPathName(typeInfo);

            char[] delimiter = new char[] { '.' };
            string[] split = fullTypeName.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);

            string outputHeaderFolder = assemblyData.OutputHeaderFolder;
            string outputSourceFolder = assemblyData.OutputSourceFolder;

            int length = split.Length;
            for (int i = 0; i < length - 1; ++i)
            {
                outputHeaderFolder = Path.Combine(outputHeaderFolder, split[i]);
                outputSourceFolder = Path.Combine(outputSourceFolder, split[i]);
            }

            if (Directory.Exists(outputHeaderFolder) == false)
            {
                Directory.CreateDirectory(outputHeaderFolder);
            }
            if (Directory.Exists(outputSourceFolder) == false)
            {
                Directory.CreateDirectory(outputSourceFolder);
            }

            string className = split[length - 1];
            className = LanguageManager.NameFixup.UnmangleName(className);
            string headerFileName = Path.Combine(outputHeaderFolder, className + ".h");
            string sourceFileName = Path.Combine(outputSourceFolder, className + ".cpp");

            try
            {
                Util.WriteFileIfChanged(headerFileName, mClassDefinitionData.Text);
            }
            catch
            {
                Console.WriteLine("Cannot write the file '" + headerFileName + "'.");
            }

            try
            {
                Util.WriteFileIfChanged(sourceFileName, mMethodDefinitionData.Text);
            }
            catch
            {
                Console.WriteLine("Cannot write the file '" + sourceFileName + "'.");
            }
        }

        private void AddStaticConstructorCheckForGenerics(StringData data)
        {
            data.Append("static bool __firstTime__ = true;\n");
            data.Append("if (__firstTime__ == false)\n");
            data.Append("{\n");
            data.Append(1, "return;\t//  Static constructor already called for this type, skip it...\n");
            data.Append("}\n");
            data.Append("__firstTime__ = false;\n");
        }

        public void GenerateDelegate(ITypeInfo typeInfo, string declarationName, NestedType nested, int numNamespaces)
        {
            ITypeDeclaration typeDeclaration = typeInfo.TypeDeclaration;

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
            StringData parametersDeclaration = LanguageManager.ReferenceGenerator.GenerateCodeParameterDeclarationCollection(invokeMethod.Parameters, info);
            StringData parameters = LanguageManager.ReferenceGenerator.GenerateCodeParameterCollection(invokeMethod.Parameters, info);

            // Before adding the declaration name, add the return value
            StringData returnValue = LanguageManager.ReferenceGenerator.GenerateCodeTypeWithPostfix(invokeMethod.ReturnType.Type, info);

            /*
             * Use a macro for the delegate creation, the macro generates everything necessary and the functor as well
             * 
                            text += returnValue.Text + " ";
                            text += declarationName;
                            text += "(" + parameters.Text + ");\n";
                            mSourceData.Append(text);
            */

            string appendGenericName = "";
            string appendGenericMacro = "";
            int startingGenericIndex = 0;

            int delegateArg = typeDeclaration.GenericArguments.Count;
            if (delegateArg != 0)
            {
                // If there is a delegate, in any case we happend the generic post-fix for the macro
                appendGenericMacro = "__G";

/*
                ITypeDeclaration ownerTypeDeclaration = typeDeclaration.Owner as ITypeDeclaration;
                bool asGeneric = false;
                if (ownerTypeDeclaration != null)
                {
                    // delegate should have the same number or more generics arguments than the owner...
                    // so the number of generic arguments of the owner is the starting index of the delegate
                    startingGenericIndex = ownerTypeDeclaration.GenericArguments.Count;
                    Debug.Assert(startingGenericIndex <= delegateArg);

                    if (startingGenericIndex != delegateArg)
                    {
                        // Different number, it means that the delegate itself is a generic
                        asGeneric = true;
                    }

                    // Un-nested specific...
                    // Once we initialized correctly the asGeneric flag, we need to set the index to zero
                    // so all the generic parameters are passed...
                    startingGenericIndex = 0;
                }
                else
                {
                    // Not nested, so it is generic...
                    asGeneric = true;
                }
 */

/*
 * Not needed...
 * 
                if (asGeneric)
                {
                    // The delegate itself is generic (on top of the owner genericity)
                    // Use the generic post-fix to differentiate from a non-generic type
                    appendGenericName = "__G" + typeInfo.NumGenericArguments.ToString();
                }
 */
            }

            string delegateText = "CREATE_DELEGATE" + appendGenericMacro + "(" + declarationName + appendGenericName + ", ";
            if (returnValue.Text == "::System::Void")
            {
                delegateText += ", ::System::Void";
            }
            else
            {
                delegateText += "return, " + returnValue.Text;
            }

            int numParameters = invokeMethod.Parameters.Count;
            string wrapper = "__W" + numParameters.ToString() + "__";

            delegateText += ", " + wrapper + "(" + parametersDeclaration + ")";
            delegateText += ", " + wrapper + "(" + parameters + ")";

            string forwardDeclaration = "";

            if (delegateArg != 0)
            {
                string templateParametersDeclaration = "";
                string templateParameters = "";
                bool first = true;
                int numberTotalGenericsArgs = typeDeclaration.GenericArguments.Count;

                // Another code is doing similar things... Look for "template <"
                for (int i = startingGenericIndex; i < numberTotalGenericsArgs; ++i)
                {
                    IType argType = typeDeclaration.GenericArguments[i];
                    string argText = LanguageManager.ReferenceGenerator.GenerateCodeTypeAsString(argType, info);

                    if (first == false)
                    {
                        templateParametersDeclaration += ", ";
                        templateParameters += ", ";
                    }
                    first = false;

                    templateParametersDeclaration += "typename " + argText;
                    templateParameters += argText;
                }

                delegateText += ", ";

                int numGenParameters = numberTotalGenericsArgs - startingGenericIndex;
                wrapper = "__W" + numGenParameters.ToString() + "__";

                delegateText += wrapper + "(" + templateParametersDeclaration + "), ";
                delegateText += wrapper + "(" + templateParameters + ")";
                forwardDeclaration += "template <" + templateParametersDeclaration + " >\n";
            }

            delegateText += ")\n";
            mClassDefinitionData.Append(delegateText);

            forwardDeclaration += "class " + declarationName + appendGenericName + ";\n";
            mClassDeclarationData.Append(forwardDeclaration);

            // For the delegate, we are not parsing more...
        }

        private void ListInfo(ITypeInfo typeInfo, StringCollection allWrappers, StringData data)
        {
            data.Indentation++;
            bool first = true;
            foreach (string oneWrapper in allWrappers)
            {
                if (first == false)
                {
                    data.AppendSameLine(",\n");
                }
                first = false;
                data.Append("{\t");
                string wrapperFullName = typeInfo.FullName + "::" + oneWrapper;
                data.AppendSameLine(wrapperFullName + "::__GetId__(), new " + wrapperFullName);
                data.AppendSameLine("\t}");
            }
            data.AppendSameLine("\n");
            data.Indentation--;
        }

        private void CloseNamespace(int numNamespaces)
        {
            while (numNamespaces > 0)
            {
                --mClassDefinitionData.Indentation;
                mClassDefinitionData.Append("}\n");

                --mClassDeclarationData.Indentation;
                mClassDeclarationData.Append("}\n");

                --numNamespaces;
            }
        }

        private void CloseNotNested(ObjectType objectType, string declarationName)
        {
            // Do nothing...
        }

        // TODO: See if the member "InterfaceTypeDeclaration" is still necessary...
        private class DiscoveredMethod
        {
            public DiscoveredMethod(ITypeInfo typeInfo, ITypeDeclaration interfaceTypeDeclaration, IMethodDeclaration methodDeclaration, ParsingInfo parsingInfo)
            {
                TypeInfo = typeInfo;
                InterfaceTypeDeclaration = interfaceTypeDeclaration;
                MethodDeclaration = methodDeclaration;
                ParsingInfo = parsingInfo;
            }

            public ITypeInfo TypeInfo;
            public ITypeDeclaration InterfaceTypeDeclaration;
            public IMethodDeclaration MethodDeclaration;
            public ParsingInfo ParsingInfo;
        }

        private IList<DiscoveredMethod> DiscoverInterfaceMethods(ICollection<ITypeInfo> interfaces, ITypeInfo priorityType, StringData fieldInitialization, StringData staticFieldInitialization, IDictionary<string, string> methodsAdded)
        {
            IDictionary<string, IList<DiscoveredMethod>> baseMethodsAdded = new Dictionary<string, IList<DiscoveredMethod>>();

            int count = interfaces.Count;
            int i = 0;
            foreach (ITypeInfo oneInterface in interfaces)
            {
                if (object.ReferenceEquals(oneInterface, priorityType))
                {
                    Debug.Assert((i == count - 1), "A priority type has been passed and it's not the last interface!");
                }

                // First detect methods that are ambiguous
                // This actually should not be really necessary (as ambiguous methods are never called) but it gives us another level of check
                // (to make sure that our understanding of interface is correct, and maybe avoid incorrect side effects (like having to implement the ambiguous method)
                // Also the good side effect is that ambiguous method won't take any place in the virtual table...

                ITypeDeclaration interfaceTypeDeclaration = oneInterface.TypeDeclaration;
                foreach (IMethodDeclaration methodDeclaration in interfaceTypeDeclaration.Methods)
                {
                    ParsingInfo methodInfo = new ParsingInfo(interfaceTypeDeclaration);
                    methodInfo.FieldInitialization = fieldInitialization;
                    methodInfo.StaticFieldInitialization = staticFieldInitialization;

                    string methodSignature;
                    string fullSignature;
                    methodSignature = GenerateCodeMethod(oneInterface, methodDeclaration, ObjectType.INTERFACE, methodInfo, null, MethodGeneration.ParseOnly, true, out fullSignature);
                    if ((methodsAdded != null) && (methodsAdded.ContainsKey(fullSignature)))
                    {
                        // The method has already been added at the base interface level
                        // It hides any method with same signature from the base interface
                        continue;
                    }

                    IList<DiscoveredMethod> list;

                    if (object.ReferenceEquals(oneInterface, priorityType))
                    {
                        // This interface has the priority, it means that it will override anyway the methods on the other interfaces
                        // Do as if it was the first time we were adding it... (replace previous content)
                        list = new List<DiscoveredMethod>();
                        baseMethodsAdded[methodSignature] = list;
                    }
                    else
                    {
                        // The method has not been yet added in the main interface, add it to the base methods
                        if (baseMethodsAdded.ContainsKey(methodSignature))
                        {
                            // That's an ambiguous method, add it to the others...
                            list = baseMethodsAdded[methodSignature];
                        }
                        else
                        {
                            // First time that we add it
                            list = new List<DiscoveredMethod>();
                            baseMethodsAdded[methodSignature] = list;
                        }
                    }
                    // Add the method to the corresponding list...
                    DiscoveredMethod method = new DiscoveredMethod(oneInterface, interfaceTypeDeclaration, methodDeclaration, methodInfo);
                    list.Add(method);
                }
                ++i;
            }

            // Now that we have parsed all the methods, we just need to fill a single list of non ambiguous methods
            IList<DiscoveredMethod> methodsDiscovered = new List<DiscoveredMethod>();
            foreach (KeyValuePair<string, IList<DiscoveredMethod>> methodGroup in baseMethodsAdded)
            {
                if (methodGroup.Value.Count > 1)
                {
                    // This method was ambiguous, skip it...
                    continue;
                }
                Debug.Assert(methodGroup.Value.Count == 1); // Make sure it was exactly 1 and not 0

                methodsDiscovered.Add(methodGroup.Value[0]);
            }
            return (methodsDiscovered);
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

            // Another code is doing similiar things... Look tor "template <"
            string text = "template <";
            bool firstGeneric = true;
            foreach (IType typeReference in genericArguments)
            {
                if (firstGeneric == false)
                {
                    text += ", ";
                }
                firstGeneric = false;
                text += "typename ";
                text += LanguageManager.NameFixup.GetSafeName(typeReference.ToString());
            }
            text += " >\n";
            return (text);
        }

        public string GenerateCodeGenericConstraints(ITypeCollection genericArguments, NestedType nested)
        {
            // Constraints don't exist in C++
            return ("");
        }

        // It seems that this function is not called anymore...
        // We are directly calling the standard GenerateMethod function
        public void GenerateProperty(bool isInterface, ITypeInfo typeInfo, IPropertyDeclaration propertyDeclaration, ObjectType objectType, IDictionary removeMethods, bool wrapper)
        {
            Debug.Fail("");

#if false
            ITypeDeclaration typeDeclaration = typeInfo.TypeDeclaration;
            IMethodReference methodReferenceSet = propertyDeclaration.SetMethod;
            IMethodDeclaration methodDeclarationSet = (IMethodDeclaration)methodReferenceSet;
            IMethodReference methodReferenceGet = propertyDeclaration.GetMethod;
            IMethodDeclaration methodDeclarationGet = (IMethodDeclaration)methodReferenceGet;
            ParsingInfo info = new ParsingInfo(typeDeclaration);

#if DEBUG
            info.DebugMethodName = propertyDeclaration.Name;
#endif

            IMethodDeclaration protectionDeclaration = methodDeclarationSet;
            if (protectionDeclaration == null)
            {
                // If we can't use the set method, use the get method for the protection
                protectionDeclaration = methodDeclarationGet;
            }
            // At least set or get is not null
            Debug.Assert(protectionDeclaration != null);

            string text = "";
            // For interface, we don't want method modifiers - Except for C++. Interface don't exist, they are simple class
            // Also in C++ we don't care about explicit implementation, everything is implicit
            /*
            if ((isInterface == false) &&  (propertyDeclaration.Name.IndexOf(".") == -1))
             */
            {
                // No protection nor abstract keyword for interface
                // Same if we don't override a specific method (in that case the method name contains a . for the fully qualified type)...
                // The first :: is not even valid as there is not constructor as property...
                bool abstractMethod;
                text = GetMethodModifiers(typeInfo, protectionDeclaration, objectType, out abstractMethod);
            }

            // Append by string to avoid double tabulations.
            string propertyType = LanguageManager.ReferenceGenerator.GenerateCodeTypeWithPostfix(propertyDeclaration.PropertyType, info).Text;
            string  propertyName = LanguageManager.ReferenceGenerator.GenerateCodePropertyDeclarationName(propertyDeclaration, info).Text;

            // Because we don't have explicit implementation, remove the explict interface type as well...
            if (propertyName.IndexOf('.') != -1)
            {
                int index = propertyName.LastIndexOf('.');
                propertyName = propertyName.Substring(index + 1);
            }

            string parameters = LanguageManager.ReferenceGenerator.GenerateCodePropertyDeclarationParameters(propertyDeclaration, info).Text;

            StringData propertyCode = new StringData();
            if (methodDeclarationSet != null)
            {
                IBlockStatement setBody = methodDeclarationSet.Body as IBlockStatement;

                string signatureText = "set_" + propertyName + "(";
                if (parameters != "")
                {
                    signatureText += parameters + ", ";
                }
                signatureText += propertyType + " value)";

                // Actually for set property, there is a specific case
                // To handle "gracefully and transparently" returned value after a set property
                // We actually tells that the set properties return the passed value by reference

                // The most common case is something like this:
                // return (x.MyProperty = y.MyProperty);

                // Reflector won't return enough information regarding the property (In IL there is actually a DUP).
                // So usually the parsing will give you something like:

                // return (x.set_MyProperty(y.get_MyProperty());
                // This obviously won't compile very well as by standard definition set_MyProperty() returns void

                // We could change the parser to detect the case and do something very complicated like this:
                //  x.set_MyProperty(y.get_MyProperty());
                //  return (x.get_MyProperty());
                // But on top of not being simple, this would not be efficient (another function call),
                // and x.get_MyProperty() could have a side effect that is undesirable.
                // Remember that IL duplicates the value so x.get_MyProperty() is actually not called...

                // Or we could do something like this:
                // MyType * temp;
                // return (x.set_MyProperty(temp = y.get_MyProperty()), temp);
                // That would solve the efficiency, and x.get_MyProperty() would not be called - no side effect
                // But again the parsing is really not straightforward - imagine with several in a row...

                // So after a lot of thoughts and considerations (5 minutes), it seems that the best solution
                // is for set property to return a reference on the passed value.
                // There is no impact in term of parsing, and the impact in term of performance should be negligible
                // (even if the property type were a big structure, it would just pass the address...).
                // Finally there would not be any noticeable side-effect... (same thing as calling a dup).
                // The only potential trouble would be if somebody were modifying "value" and was using the assignment trick above
                // The chance of this hapenning is rather limited, and could be fixed by duplicating the variable and renaming it to something else

                // For this to work we need to add in the implementation a return (value); at the end
                // And patch all the return; statement in the same manner.

                // Old definition:
                //propertyCode.AppendSameLine("void " + signatureText + ";\n");
                // New definition:
                propertyCode.AppendSameLine(propertyType +  " & " + signatureText + ";\n");
                if (setBody != null)
                {
                    info.ReturnedType = LanguageManager.LocalTypeManager.TypeVoid;
                    info.ForcedReturnValue = "value";       // Change "return;" by "return (value);"
                    string bodyText = LanguageManager.StatementGenerator.GenerateCodeForMethod(setBody, info).Text;
                    info.ForcedReturnValue = null;

                    // Old definition:
                    //string methodDefinition = "void " + typeInfo.NonScopedFullName + "::" + signatureText + "\n";
                    // New definition:
                    string methodDefinition = propertyType + " & " + typeInfo.NonScopedFullName + "::" + signatureText + "\n";
                    mMethodDefinitionData.Append(methodDefinition);

                    // Old definition:
                    //mMethodDefinitionData.Append(bodyText);

                    // New definition:
                    mMethodDefinitionData.Append("{\n");
                    mMethodDefinitionData.Indentation++;
                    mMethodDefinitionData.Append(bodyText);
                    mMethodDefinitionData.Append("return (value);\n");
                    mMethodDefinitionData.Indentation--;
                    mMethodDefinitionData.Append("}\n");
                }

                removeMethods.Add(methodReferenceSet, null);
            }
            if (methodDeclarationGet != null)
            {
                IBlockStatement getBody = methodDeclarationGet.Body as IBlockStatement;

                string signatureText = "get_" + propertyName + "(" + parameters + ")";
                propertyCode.AppendSameLine(propertyType + " " + signatureText + ";\n");

                if (getBody != null)
                {
                    info.ReturnedType = LanguageManager.LocalTypeManager.GetLocalType(propertyDeclaration.PropertyType);
                    string bodyText = LanguageManager.StatementGenerator.GenerateCodeForMethod(getBody, info).Text;

                    mMethodDefinitionData.Append(propertyType + " " + typeInfo.NonScopedFullName + "::" + signatureText + "\n");

                    FixReflectorBug(getBody, bodyText, propertyType, propertyCode);
                }
                removeMethods.Add(methodReferenceGet, null);
            }

/*
 * Unsafe doesn't exist in C++
 * 
            if (info.UnsafeMethod)
            {
                text = "unsafe " + text;
            }
*/

            mClassDefinitionData.Append(text);
            mClassDefinitionData.Append(propertyCode.Text);
#endif
        }

        public string GetMethodModifiers(ITypeInfo typeInfo, IMethodDeclaration methodDeclaration, ObjectType objectType, out bool abstractMethod)
        {
            ITypeDeclaration typeDeclaration = typeInfo.TypeDeclaration;
            string text = "";
            abstractMethod = false;

            switch (methodDeclaration.Visibility)
            {
                case MethodVisibility.Public:
                    text += "public:\n";
                    break;
                case MethodVisibility.PrivateScope:
                case MethodVisibility.Private:
                    //text += "private:\n";
                    text += "public:\n";
                    break;
                case MethodVisibility.Family:
                    //text += "protected:\n";
                    text += "public:\n";
                    break;
                case MethodVisibility.Assembly:
                    //text += "internal ";
                    text += "public:\n";
                    break;
                case MethodVisibility.FamilyAndAssembly:
                // Don't know what this one gives, but assume same as NestedFamilyOrAssembly
                case MethodVisibility.FamilyOrAssembly:
                    //text += "internal protected ";
                    //text += "protected:\n";
                    text += "public:\n";
                    break;
            }

            // For method, no notion of nested...
            // Add the generics before the return values
            text += GenerateCodeGenericArguments(methodDeclaration.GenericArguments, NestedType.NOT_NESTED);

            if (methodDeclaration.Abstract)
            {
                // virtual and templated methods are incompatible with C++
                // In the case of abstract method it is even worse as we cannot recover...

                if (methodDeclaration.GenericArguments.Count == 0)
                {
                    // It is not a generic method, it's safe
                    text += "virtual ";
                    abstractMethod = true;
                }
                else
                {
                    // Generic method AND abstract, we cannot recover as the user must provide an implementation
                    CppError.DisplayAbstractGenericMethod(typeInfo, methodDeclaration);
                }
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
                            text = "public:\n ";
                        }
                        else
                        {
                            // assume that the function public virtual instead!
                            text = "public:\n";

                            if (methodDeclaration.GenericArguments.Count == 0)
                            {
                                text += "virtual ";
                            }
                            else
                            {
                                // Generic method AND virtual, we can recover by not providing the virtual keyword
                                //  This won't have the same behavior but at least it is going to compile...
                                CppError.DisplayVirtualGenericMethod(typeInfo, methodDeclaration);

                                // Put a comment in the C++ code so the user can understands the possible issue
                                text += "// virtual commented as templated methods cannot be virtual in C++.\n";
                                text += "// Expect a change in behavior in your code.\n";
                            }
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
                            else if (methodDeclaration.Final == false)
                            {
                                // If that's "final", the method is not really virtual
                                // So here it's not final, so enable virtual keyword...

                                if (methodDeclaration.GenericArguments.Count == 0)
                                {
                                    text += "virtual ";
                                }
                                else
                                {
                                    // Generic method AND virtual, we can recover by not providing the virtual keyword
                                    //  This won't have the same behavior but at least it is going to compile...
                                    CppError.DisplayVirtualGenericMethod(typeInfo, methodDeclaration);

                                    // Put a comment in the C++ code so the user can understands the possible issue
                                    text += "// virtual commented as templated methods cannot be virtual in C++.\n";
                                    text += "// Expect a change in behavior in your code.\n";
                                }
                            }
                        }
                    }
                }
                else
                {
                    /*
                     * Doesn't exist in C++
                     *
                    text += "override ";
                     */
                }
            }
            else if (methodDeclaration.Static)
            {
                text += "static ";
            }

            return (text);
        }

        public enum MethodGeneration
        {
            ParseOnly,
            ParseAndGenerate
        }

        public virtual string GenerateCodeMethod(ITypeInfo typeInfo, IMethodDeclaration methodDeclaration, ObjectType objectType, ParsingInfo info, ITypeDeclaration wrappedType, MethodGeneration generation, bool generateImplementation, out string fullSignature)
        {
            ITypeDeclaration typeDeclaration = typeInfo.TypeDeclaration;
            string text = "";
            string methodSignature;
            string methodSignatureForDefinition = "";

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
            returnValue = LanguageManager.ReferenceGenerator.GenerateCodeTypeWithPostfix(methodDeclaration.ReturnType.Type, info).Text;
            info.ReturnedType = LanguageManager.LocalTypeManager.GetLocalType(methodDeclaration.ReturnType.Type);

            // Don't use the return value as part of the method signature
            methodSignature = "";
            // But still keep it in the full signature...
            fullSignature = returnValue + " ";

            // C# specific: Get the body before the other modifiers
            // so we can detect the "unsafe" state
            IBlockStatement body = methodDeclaration.Body as IBlockStatement;

            // First the method parameters, so they can be added to the parsing info
            StringData parameters = LanguageManager.ReferenceGenerator.GenerateCodeParameterDeclarationCollection(methodDeclaration.Parameters, info);
//            bool unsafeAdded = false;

            ITypeDeclaration safeWrappedType = wrappedType;
            if (safeWrappedType == null)
            {
                safeWrappedType = typeDeclaration;
            }

#if GENERATE_ATTRIBUTE
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
#endif

            bool pureVirtual = false;
            string methodNameWithoutExplicitImplementation; 
            // Determine if that's an explicit implementation or a method within an interface...
            // If that's an explicit implementation, methodDeclaration.Name will have a "." in the name (from Reflector)

            if ((typeDeclaration.Interface == false) && (methodDeclaration.Name.IndexOf(".", 1) == -1))
            {
                // No protection nor abstract keyword for interface
                // Same if we don't override a specific method (in that case the method name contains a . for the fully qualified type)...
                // It's okay if we have the "::" at the first position though...
                bool abstractMethod;
                text += GetMethodModifiers(typeInfo, methodDeclaration, objectType, out abstractMethod);
                pureVirtual |= abstractMethod;
                methodNameWithoutExplicitImplementation = methodDeclaration.Name;
            }
            else
            {
                // Explicit implementation or interface method... We are using public so wrappers can access it...
                text += "public:\nvirtual ";
                if (typeDeclaration.Interface)
                {
                    // And if it is within an interface, this is a pure virtual...
                    // Same for abstract method
                    pureVirtual = true;
                }

                int lastIndex = methodDeclaration.Name.LastIndexOf('.');
                if (lastIndex >= 0)
                {
                    methodNameWithoutExplicitImplementation = methodDeclaration.Name.Substring(lastIndex + 1);
                }
                else
                {
                    methodNameWithoutExplicitImplementation = methodDeclaration.Name;
                }
            }

            // For method, no notion of nested...
            // Add the generics before the modifiers and return value
            // Reparse the generic arguments for the method definition
            // TODO: Revisit that to optimize a bit...
            string genericArguments = "";
            if (safeWrappedType.GenericArguments.Count != 0)
            {
                // If the type is generic, then pass generic parameters for the type
                // (this takes priority over method generic parameters as they are duplicating those from the type)
                // This will be overriden later below if we detect that both class an method are generic...
                genericArguments = GenerateCodeGenericArguments(safeWrappedType.GenericArguments, NestedType.NOT_NESTED);
            }
            else if (methodDeclaration.GenericArguments.Count != 0)
            {
                genericArguments = GenerateCodeGenericArguments(methodDeclaration.GenericArguments, NestedType.NOT_NESTED);
            }
            methodSignatureForDefinition += genericArguments;

            bool finalizer = false;
            bool setProperty = false;

/*
 * We should actually use this type to detect the constructor...
 * Instead of using the string ".ctor" or ".cctor"
            IConstructorDeclaration constructorDeclaration = methodDeclaration as IConstructorDeclaration;
*/

            ITypeInfo wrappedTypeInfo = typeInfo;
            if (wrappedType != null)
            {
                wrappedTypeInfo = TypeInfoManager.GetTypeInfo(wrappedType);
            }

            bool fieldInitialization = false;
            bool staticFieldInitialization = false;
            bool staticConstructor = false;
            string methodName;
            bool constructor = false;
//            IList<IMethodReference> overridenMethods = null;

            string nonScopedFullName = wrappedTypeInfo.NonScopedFullName;

            if (methodDeclaration.Name == ".ctor")
            {
#if false       // We don't do this anymore as it means we can't construct the object explictly anymore
                if (typeInfo.IsValueType)
                {
                    // Special case for the value type... We keep the constructor as is...
                    methodName = typeInfo.Name;
                    text += methodName;
                    methodSignatureForDefinition += nonScopedFullName + "::" + methodName;
                }
                else
#endif
                {
                    // If it is a collected object, we create a __ctor__ method instead...

                    // If method name is ".ctor", this is the constructor
                    methodName = "__ctor__";
                                            // Name it __ctor__ in order to not collide with the C++ constructor
                                            //  The reason being that we want to keep the standard C++ constructor as empty as possible
                                            //  As we do a two stage construction (the C++ constructor then the call to __ctor__).
                                            //  This solves two issues:
                                            //      The VTable issue inside the construction
                                            //      And the case where a constructor can actually call another constructor....

                    text += "void " + methodName;
                    methodSignature = "";   // For constructor, reset the existing values, constructor don't have return values!
                    fullSignature = "";
                    methodSignatureForDefinition += "void " + nonScopedFullName + "::" + methodName;

                }

                if (info.FieldInitialization != null)
                {
                    // There was some field initialization, add that before the constructor...
                    fieldInitialization = true;
                }
                constructor = true;
            }
            else if (methodDeclaration.Name == ".cctor")
            {
                // If method name is ".cctor" this is the static constructor
                //      In .Net, static constructor is called at the beginning so you can
                //      define the construction order of the static members

                // Static constructors are marked as private static
//  Valid assert commented as it was asserting every time we were changing slightly the syntax
//                Debug.Assert((text == "private static ") || (text == "unsafe private static "));
                // Remove the private as C# doesn't want us to define it - the user could assume it can put something else

                // unsafe doesn't exist in C++
                // static constructors don't exist in C++
                // Instead generate another kind of method (we'll have to find a way to call it...).
                methodName = "Static__ctor__";
                text = "public:\nstatic void " + methodName;
                staticConstructor = true;

                if (info.StaticFieldInitialization != null)
                {
                    // There was some field initialization, add that before the constructor...
                    staticFieldInitialization = true;
                }

                if (typeInfo.IsGeneric == false)
                {
                    // If the type is generic, we actually call the static constructor differently (at each construction)
                    // And as such it can be called several times...
                    mStaticConstructorList.Add(new StringTypeInfo(typeInfo.FullName + "::" + methodName, typeInfo));
                }
                methodSignatureForDefinition += "void " + nonScopedFullName + "::" + methodName;
            }
            else if ((methodDeclaration.Name == "Finalize") && (returnValue == "::System::Void") && (methodDeclaration.Parameters.Count == 0))
            {
                // Finalize is the "destructor" in .Net
                // It gets called before the object is destroyed by the GC
                // The signature must be "void Finalize()" to be considered as the finalizer
                // Unfortunately the user can select the same name, and not being the finalizer...
                // TODO: document that in the know issues...

                // Finalizers are marked protected virtual
//  Valid assert commented as it was asserting every time we were doing a test
//                Debug.Assert((text == "protected override ") || (text == "unsafe protected override "));
                // Remove the modifiers as C# doesn't want us to define it - the user could assume it can put something else

                // unsafe doesn't exist in C++

                methodName = "~" + typeInfo.Name;
                text = methodName;
                methodSignatureForDefinition += nonScopedFullName + "::" + methodName;

                finalizer = true;
            }
            else
            {
                // Any other method
                methodName = LanguageManager.NameFixup.UnmangleMethodName(methodDeclaration.Name, true);

                // Even if C++ doesn't support explicit implementation of interfaces, mimic it
                // This is mainly to resolve the issue when several methods have the same signature but used for different interfaces

                MethodType methodType;
                methodName = LanguageManager.NameFixup.ConvertMethodName(methodName, methodDeclaration, out methodType);

                // Specific case for set properties (see the comment in GenerateProperty)
                // There is a modification though... We do the trick _only_ if the property type is not a user structure
                // Also we are not returning by reference anymore to avoid a warning on local variables
                // (it seems passed parameters are considered as local or temp  variable).

                // I guess potentially the user could enable the trick for all types (at is own risk),
                // if the compiler does the proper thing... I believe that parameter are destroyed at the end of the full statement
                // and not at the end of the function so it should work. VC++ might be too cautious here...

                // BTW the only reason we are not using the trick for user structure is because it would copy construct
                // the user structure for every single call to set property with user structure as type.
                // Although this should be rare, the occurence of the issue that we are trying to fix is even rarer...
                // So with the current trade off, a possible compilation error should pretty much never happen (P(rare) * P(rarer)  ;)
                if (methodNameWithoutExplicitImplementation.StartsWith("set_"))
                {
                    // Return value should be void for a set property...
                    Debug.Assert(returnValue == "::System::Void");

                    // Last parameter is the property type
                    IParameterDeclaration paramDeclaration = methodDeclaration.Parameters[methodDeclaration.Parameters.Count - 1];
                    StringData propertyType = LanguageManager.ReferenceGenerator.GenerateCodeTypeWithPostfix(paramDeclaration.ParameterType, info);

                    ITypeInfo propertyTypeInfo = propertyType.LocalType.GetTypeInfo();
                    if (    (propertyTypeInfo == null)
                        ||  propertyTypeInfo.LocalType.IsPrimitiveType
                        || (propertyTypeInfo.Type != ObjectType.STRUCT) )
                    {
                        // If it's not a struct or a base type, do the trick...

                        // set property return property type (no reference anymore).
                        returnValue = propertyType.Text;    //  +" &";
                        // Change "return;" by "return (value);"
                        info.ForcedReturnValue = "value";

                        // Set the flag so we can patch the body
                        setProperty = true;
                    }
                }

                // TODO: Clean this a little bit...
                string wrapperText = "::";
                if (wrappedType != null)
                {
                    wrapperText = "::" + typeInfo.GetWrapperName(typeInfo.FullName);
                    wrapperText += "::";
                }

                switch (methodType)
                {
                    case MethodType.NORMAL:
                        text += returnValue + " " + methodName;
                        methodSignatureForDefinition += returnValue + " " + nonScopedFullName + wrapperText + methodName;
                        break;

                    case MethodType.OPERATOR:
                    case MethodType.OPERATOR_FALSE:
                    case MethodType.OPERATOR_TRUE:
                        text += returnValue + " " + methodName;
                        methodSignatureForDefinition += returnValue + " " + nonScopedFullName + wrapperText + methodName;
                        break;

                    case MethodType.OPERATOR_EXPLICIT:
                    case MethodType.OPERATOR_IMPLICIT:
                        // For implicit and explicit conversions, this is different
                        // static implicit operator ReturnType(ContainerType t)
                        // static explicit operator ReturnType(ContainerType t)
//                        text += methodName + " operator_" + returnValue;
                        text += returnValue + " " + methodName;
                        methodSignatureForDefinition += returnValue + " " + nonScopedFullName + wrapperText + methodName;
                        break;

                    default:
                        Debug.Fail("Should not be here!");
                        break;
                }
            }

            int numGemericArguments = methodDeclaration.GenericArguments.Count;

            methodSignature += methodName;
            if (numGemericArguments != 0)
            {
                // Add __G to the signature to differentiate methods...
                methodSignature += "__G" + numGemericArguments.ToString();
            }

            if (constructor && (methodDeclaration.Parameters.Count != 0) && (methodDeclaration.Parameters[0].ParameterType.Equals(typeDeclaration)) && (typeDeclaration.ValueType))
            {
                // Specific case, if default copy constructor on a struct, make sure that we actually use const reference...
                // Otherwise the copy constructor is invalid

                // TODO: Clean this hack and have a better code generation ;)
                string copyConstructorParameter = parameters.Text.Replace(" ", " & ");
                copyConstructorParameter = "const " + copyConstructorParameter;
                parameters.Replace(copyConstructorParameter);
            }
            methodSignature += "(" + parameters.Text + ")";

            fullSignature += methodSignature;

            if (generation == MethodGeneration.ParseOnly)
            {
                return (methodSignature);
            }

            string textToAdd = "";

            if (numGemericArguments != 0)
            {
                // The method is a generic method, add the generic postfix
                textToAdd += "__G" + numGemericArguments.ToString();
            }

            textToAdd += "(";
            if ((wrappedType != null) || typeDeclaration.Interface)
            {
                // If wrapper or an interface, we add the passed instance pointer (in order to decouple the impl from the interface)
                textToAdd += "void * " + PASSED_INSTANCE;
                if (parameters.Text != "")
                {
                    textToAdd += ", ";
                }
            }
            textToAdd += parameters.Text;
            textToAdd += ")";

            mClassDefinitionData.Append(text + textToAdd);

            if (pureVirtual && (wrappedType == null))
            {
                // " = 0" for pure virtual, wrappers are a real implementation
                mClassDefinitionData.AppendSameLine(" = 0");
            }

            // Parse the body now (there is no unsafe opcode in C++, so we don't have to do it earlier)
            string bodyText;

            if (wrappedType == null)
            {
                if (body != null)
                {
                    StringData bodyStringData = new StringData();
                    bool mustClose = false;
                    if (staticConstructor && typeInfo.IsGeneric)
                    {
                        // That's a generic and there is a static constructor
                        // The calling convention is different, the method can be called several times...
                        // We need to add a specific check
                        bodyStringData.Append("{\n");
                        bodyStringData.Indentation++;
                        AddStaticConstructorCheckForGenerics(bodyStringData);
                        mustClose = true;
                    }

                    if (generateImplementation)
                    {
                        bodyStringData.Append(LanguageManager.StatementGenerator.GenerateCodeForMethod(body, info));
                    }
                    else
                    {
                        bodyStringData.Append("CROSSNET_NOT_IMPLEMENTED();\n");
                    }

                    if (mustClose)
                    {
                        bodyStringData.Indentation--;
                        bodyStringData.Append("}\n");
                    }
                    bodyText = bodyStringData.Text;
                }
                else if (methodDeclaration.Body != null)
                {
                    // There is a body but this is not a IBlockStatement...
                    // This can happen for abstract classes that don't have default constructor defined
                    // So the C# compiler will generate a very simple protected constructor,
                    // that Reflector doesn't disassemble properly as empty block statement...
                    // Patch this...
                    bodyText = "{\n}\n";
                }
                else
                {
                    // Otherwise this is a pure abstract method without implementation (from abstract class, interface or potentially extern method?).
                    bodyText = "";
                }

                // Field initialization, static field initialization, set property patch is not needed for wrapper code
                if (fieldInitialization)
                {
                    bodyText = "{\n" + info.FieldInitialization + bodyText + "}\n";
                }
                else if (staticFieldInitialization)
                {
                    bodyText = "{\n" + info.StaticFieldInitialization + bodyText + "}\n";
                }
                else if (setProperty)
                {
                    bodyText = "{\n" + bodyText + "return (value);\n}\n";
                }
            }
            else
            {
                // It's a wrapper code for the interface implementation...
//#if STANDARD_DEP
                bodyText = "{\n";
                if (wrappedTypeInfo.Type == ObjectType.STRUCT)
                {
                    // For struct, we actually have to get the address from the boxed object
                    string boxedType = wrappedTypeInfo.FullName;
                    string instanceText = boxedType + " *";  // All the types even struct are passed as pointer...
                    bodyText += "\t" + instanceText + " " + INSTANCE + " = (static_cast< ::CrossNetRuntime::BoxedObject<" + boxedType + " > * >(" + PASSED_INSTANCE + "))->GetUnboxedAddress();\n";
                }
                else
                {
                    string instanceText = wrappedTypeInfo.FullName + " *";  // All the types even struct are passed as pointer...
                    bodyText += "\t" + instanceText + " " + INSTANCE + " = static_cast<" + instanceText + " >(" + PASSED_INSTANCE + ");\n";
                }

                string newMethodName = FindMethodNameInWrapper(wrappedTypeInfo, methodDeclaration);
                if (newMethodName != "")
                {
                    methodName = newMethodName;
                }
                else
                {
                    Debug.Fail("We should not be here!");
                    // We are going to use the old method name - it won't be good but might reduce the possiblity of compile error
                }

                string methodCall = INSTANCE + "->" + methodName + "(";
                methodCall += LanguageManager.ReferenceGenerator.GenerateCodeParameterCollection(methodDeclaration.Parameters, info);
                methodCall += ")";

                if (returnValue != "::System::Void")
                {
                    bodyText += "\treturn (" + methodCall + ");\n";
                }
                else
                {
                    bodyText += "\t" + methodCall + ";\n";
                }
                bodyText += "}\n";
//#else
                // Don't implement the wrapper code yet
//                bodyText = ";";
//#endif
            }

            // Before we go further, we remove the forced return value, in case info is used for another method...
            info.ForcedReturnValue = null;

            IConstructorDeclaration constructorDeclaration = methodDeclaration as IConstructorDeclaration;
            if ((constructorDeclaration != null) && (constructorDeclaration.Initializer != null) && (constructorDeclaration.Initializer.Method != null))
            {
                string baseMethodName = constructorDeclaration.Initializer.Method.ToString();

                bool baseCall = (baseMethodName == "base..ctor");       // Here we use directly Reflector result to determine the state
                                                                        //  We could have done it ourselves but it was lengthier...
                if (baseCall && (constructorDeclaration.Initializer.Arguments.Count == 0))
                {
                    // The constructor calls its default base class constructor (only for ref type).
                    if (typeInfo.IsValueType == false)
                    {
                        string baseConstructor;
                        if (typeInfo.BaseType != null)
                        {
                            // There is a base type - explicit it
                            baseConstructor = typeInfo.BaseType.GetPrefixedFullName("__ctor__();\n");
                        }
                        else
                        {
                            // There is no base type, it means that we are directly deriving from System.Object
                            // Still even if it doesn't do much, call System::Object::__ctor__();
                            // The main reason is that we can do some checks here...
                            baseConstructor = "::System::Object::__ctor__();\n";
                        }

                        if (typeInfo.IsGeneric)
                        {
                            // Special case for generic types...
                            // Static constructor is always called during a construction as we don't know how and when to initialize them...
                            // As the list can change depending of the code being compiled
                            baseConstructor += "Static__ctor__();\n";
                        }

                        bodyText = "{\n" + baseConstructor + bodyText + "}\n";
                    }
                }
                else
                {
                    // It means that there is a non-default class constructor to call...

                    // Call this function even if we don't care of the result, it will initialize in ParseInfo the member MethodReference
                    LanguageManager.ExpressionGenerator.GenerateCode(constructorDeclaration.Initializer.Method, info);
                    string constructParameters = LanguageManager.ExpressionGenerator.GenerateCodeMethodInvokeArguments(constructorDeclaration.Initializer.Arguments, info).Text;

                    if (baseCall)
                    {
                        // Find the base type and call the corresponding constructor...
                        string baseConstructor = typeInfo.BaseType.GetPrefixedFullName("__ctor__") + constructParameters + ";\n";
                        bodyText = "{\n" + baseConstructor + bodyText + "}\n";
                    }
                    else
                    {
                        // Find the corresponding constructor in the same class
                        string baseConstructor = typeInfo.GetPrefixedFullName("__ctor__") + constructParameters + ";\n";
                        bodyText = "{\n" + baseConstructor + bodyText + "}\n";
                    }
                }
            }

            // After defining the method, add the constraints for generic
            mClassDefinitionData.Append(GenerateCodeGenericConstraints(methodDeclaration.GenericArguments, NestedType.NOT_NESTED));

            // Something important here!
            // I'm not aware on how to define a template function outside of a template class...
            // Something like:

            //  template <typename T> struct S { template <typename U> void Foo(U o); }
            //  then:
            //  template <typename T> void S<T>::Foo(U o)   {}      <== U undefined
            // I tried different things, look ion the net and didn't find a way to do it...
            // So in that case we have to define the method directly within the class
            // Fortunately, templated function are not compiled if they are not used,
            // as such they won't generate a compile error if the code inside references a class not yet defined...

            StringData addMethodTo = mMethodDefinitionData;
            bool defineMethodInClassDefinition = false;

            int typeGenericArgs = safeWrappedType.GenericArguments.Count;
            int methodGenericArgs = methodDeclaration.GenericArguments.Count;
            if ((typeGenericArgs > 0) && (methodGenericArgs > 0))
            {
                defineMethodInClassDefinition = true;
            }
            else if (wrappedType != null)
            {
                // If that's a wrapped type (for an interface), define the method in the class definition
                // The reason is that it is actually not that trivial to do it in the method definition file
                // (Have to know the corresponding type, test if that's a template, etc...)
                // Also there is no downside as all the types used by the wrapper are known anyway at that time...

                // Actually we are changing the behavior here
                // TODO: Clean this...
#if STANDARD_DEP
                defineMethodInClassDefinition = true;
#endif
            }

            if (defineMethodInClassDefinition)
            {
                // Both type and method have generic parameters (the method on top of the base class).
                // So that's the case we were talking about...
                addMethodTo = mClassDefinitionData;

                if (bodyText == "")
                {
                    mClassDefinitionData.AppendSameLine(";\n");
                    return (methodSignature);
                }
                mClassDefinitionData.AppendSameLine("\n");
            }
            else
            {
                // Otherwise do the standard procedure...
                mClassDefinitionData.AppendSameLine(";\n");
                if (bodyText == "")
                {
                    return (methodSignature);
                }

                mMethodDefinitionData.Append(methodSignatureForDefinition + textToAdd + "\n");
            }

            if (finalizer)
            {
                // During the finalizer, remove any call to base.Finalize()
                // usually in a try / catch - so if the finalizer code is failing the base.Finalize is still called
                // The C# compiler will add it back anyway...

                // It seems this has been replaced by system.object.Finalize() now...
                //  TODO: Investigate what is the best here...
                string baseClassName = "::System::Object";  // By default System.Object
                if (typeInfo.BaseType != null)
                {
                    // If there is a base class, look it up instead...
                    baseClassName = typeInfo.BaseType.FullName;
                }
                bodyText = bodyText.Replace(baseClassName + "::Finalize();", "");
            }

            FixReflectorBug(body, bodyText, returnValue, addMethodTo, generateImplementation, methodDeclaration.ReturnType.Type);

            if (constructor && (typeDeclaration.Abstract == false))
            {
                // The class was not abstract 
                // And the method was actually a constructor, we have to create the factory method...

                if (typeInfo.IsValueType == false)
                {
                    // We do this only on allocated types,
                    // value type can't be derived / or have virtual methods so there is no point in having factory method
                    // On top of that we don't want to have copy construction...

                    string instanceText = typeInfo.GetInstanceText(info);
                    string textBefore;
                    string textAfter;
//                    string genericArguments = GenerateCodeGenericArguments(safeWrappedType.GenericArguments, NestedType.NOT_NESTED);

                    textBefore = instanceText + " ";

                    textAfter = "__Create__(" + parameters + ")";
                    mClassDefinitionData.Append("static " + textBefore + textAfter + ";\n");

                    mMethodDefinitionData.Append(genericArguments);
                    mMethodDefinitionData.Append(textBefore + nonScopedFullName + "::" + textAfter + "\n");
                    mMethodDefinitionData.Append("{\n");
                    mMethodDefinitionData.Indentation++;

                    mMethodDefinitionData.Append(instanceText);
                    mMethodDefinitionData.AppendSameLine("__temp__ = new ");
                    mMethodDefinitionData.AppendSameLine(typeInfo.FullName);

                    mMethodDefinitionData.AppendSameLine("();\n");

                    // During the creation, sets the interface map as well...
                    // We do this inside the __Create__ function instead of the constructor,
                    // the reason is that we want to set the interface map only one time
                    // (Opposed to the VTable pointer that can be set several times...)
                    mMethodDefinitionData.Append("CROSSNET_ASSERT(__GetInterfaceMap__() != NULL, \"Interface map not set correctly!\");\n");
                    mMethodDefinitionData.Append("__temp__->m__InterfaceMap__ = __GetInterfaceMap__();\n");

                    mMethodDefinitionData.Append("__temp__->__ctor__");
                    mMethodDefinitionData.AppendSameLine("(");
                    mMethodDefinitionData.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeParameterCollection(methodDeclaration.Parameters, info));
                    mMethodDefinitionData.AppendSameLine(");\n");

                    mMethodDefinitionData.Append("return (__temp__);\n");

                    mMethodDefinitionData.Indentation--;
                    mMethodDefinitionData.Append("}\n");
                }
                else
                {
                    // We do this only on allocated types,
                    // value type can't be derived / or have virtual methods so there is no point in having factory method
                    // On top of that we don't want to have copy construction...

                    string textBefore;
                    string textAfter;
                    //                    string genericArguments = GenerateCodeGenericArguments(safeWrappedType.GenericArguments, NestedType.NOT_NESTED);

                    textBefore = typeInfo.Name;

                    textAfter = "(" + parameters + ")";
                    mClassDefinitionData.Append(textBefore + textAfter + ";\n");

                    mMethodDefinitionData.Append(genericArguments);
                    mMethodDefinitionData.Append(typeInfo.FullName + "::" + textBefore + textAfter + "\n");
                    mMethodDefinitionData.Append("{\n");
                    mMethodDefinitionData.Indentation++;

                    mMethodDefinitionData.Append("__ctor__(");
                    mMethodDefinitionData.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeParameterCollection(methodDeclaration.Parameters, info));
                    mMethodDefinitionData.AppendSameLine(");\n");

                    mMethodDefinitionData.Indentation--;
                    mMethodDefinitionData.Append("}\n");
                }
            }

            return (methodSignature);
        }

        private string FindMethodNameInWrapper(ITypeInfo wrappedTypeInfo, IMethodDeclaration methodDeclaration)
        {
            // Try to find the corresponding method in the wrapped type, so we can determine its real name...
            // (esp. regarding explicit interface impl).
            // With the double loop, this implementation is really slow
            // TODO: Optimize this code...

            // Do the overriden methods first as normal methods could shadow some overriden methods
            // Do not merge the two loops together...
            foreach (IMethodDeclaration methodDeclarationWrappedType in wrappedTypeInfo.TypeDeclaration.Methods)
            {
                // For each method, look at each method overriding another method
                foreach (IMethodReference overridenMethod in methodDeclarationWrappedType.Overrides)
                {
                    // See if the methods are the same
                    if (overridenMethod.CompareTo(methodDeclaration) == 0)
                    {
                        // Found the corresponding method!
                        // Put the new name...
                        string methodName = LanguageManager.NameFixup.UnmangleMethodName(methodDeclarationWrappedType.Name, true);
                        MethodType methodType;
                        methodName = LanguageManager.NameFixup.ConvertMethodName(methodName, methodDeclarationWrappedType, out methodType);
                        return (methodName);
                    }
                }
            }

            // We tried the overriden methods and did not find the corresponding one, now try the standard method
            foreach (IMethodDeclaration methodDeclarationWrappedType in wrappedTypeInfo.TypeDeclaration.Methods)
            {
                // After comparing the overriden method, try to compare the standard method (as this is the second choice for a given interface)
                // Because we are comparing an implementation and a method from an interface, they won't match directly... We have to do the comparison by hand
                if (
                        (methodDeclarationWrappedType.Visibility == MethodVisibility.Public)
                    &&  (methodDeclarationWrappedType.Static == false)
                    &&  (methodDeclarationWrappedType.Name == methodDeclaration.Name)
                    &&  (Util.CompareParameterDeclaration(methodDeclarationWrappedType.Parameters, methodDeclaration.Parameters, false))
                    &&  (Util.CompareType(methodDeclarationWrappedType.ReturnType.Type, methodDeclaration.ReturnType.Type))
                    )
                {
                    // So the implementation is public, non-static, with the same name, parameters and return value... then it seems we found the candidate...
                    // This test might not be correct if some of the parameters / return value where template...
                    // TODO: Add unit-test covering that...
                    string methodName = LanguageManager.NameFixup.UnmangleMethodName(methodDeclarationWrappedType.Name, true);
                    MethodType methodType;
                    methodName = LanguageManager.NameFixup.ConvertMethodName(methodName, methodDeclarationWrappedType, out methodType);
                    return (methodName);
                }
            }

            // We did not find it!

            // If there is a base type let's try...
            // The reason is that when a class derive from an interface, if one of its base class actually implements the interface
            // the method of the base class will be the one chosen (and you thought .Net was simple...)
            if (wrappedTypeInfo.BaseType != null)
            {
                return (FindMethodNameInWrapper(wrappedTypeInfo.BaseType, methodDeclaration));
            }

            return ("");
        }

        private static string FixReflectorBugTryCatch(IBlockStatement blockStatement, string bodyText, string returnValue, bool generateImplementation, IType returnType)
        {
            // Fixup incorrect reflector behavior...
            string bugFix = "";
            if (blockStatement == null)
            {
                // No code, no return
                return (bugFix);
            }

            if (returnValue == "::System::Void")
            {
                // Void, no return
                return (bugFix);
            }

            // If don't generate implementation, provide the default return value
            bool generateReturnValue = (generateImplementation == false);
            if (blockStatement.Statements.Count != 0)
            {
                // There is a return value, if the last statement is a try ... catch ...
                // This is certainly a Reflector bug, generate the return value
                generateReturnValue |= blockStatement.Statements[blockStatement.Statements.Count - 1] is ITryCatchFinallyStatement;
            }

            if (generateReturnValue == false)
            {
                return (bugFix);
            }

            switch (returnValue)
            {
                case "::System::Boolean":
                    bugFix = "return (false);\n";
                    break;

                case "::System::Char":
                case "::System::Byte":
                case "::System::SByte":
                case "::System::Int32":
                case "::System::UInt32":
                case "::System::Int16":
                case "::System::UInt16":
                case "::System::Int64":
                case "::System::UInt64":
                    bugFix = "return (0);\n";
                    break;

                case "::System::Single":
                case "::System::Double":
                    bugFix = "return (0.0f);\n";
                    break;

                default:
                    // It's either a class or a struct...
                    {
                        ITypeInfo returnTypeInfo = TypeInfoManager.GetTypeInfo(returnType);
                        if ((returnTypeInfo != null) && (returnTypeInfo.IsValueType))
                        {
                            // For a value type, we return a copy
                            bugFix = "return (" + returnTypeInfo.FullName + "());\n";
                        }
                        else
                        {
                            // For reference type or unkwnown type, return a pointer
                            bugFix = "return (NULL);\n";
                        }
                    }
                    break;
            }
            return (bugFix);
        }

        private void FixReflectorBug(IBlockStatement blockStatement, string bodyText, string returnValue, StringData data, bool generateImplementation, IType returnType)
        {
            // Fixup incorrect reflector behavior...
            string bugFix = FixReflectorBugTryCatch(blockStatement, bodyText, returnValue, generateImplementation, returnType);

            if ((bugFix == "") && generateImplementation)
            {
                data.Append(bodyText);
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

            // Define the enums as struct
            // There are two reasons...
            // First; it enables to box enums (so ToString(), GetHashCode(), and Equals() is declared).
            // Second; it enables 64 bits enums, and solve more elegantly the name collision
            string type = text;

            // Find the default constructor
            string defaultValue = "0";              // 0 by default
            if (typeDeclaration.Fields.Count >= 1)
            {
                IFieldDeclaration fieldDeclaration = typeDeclaration.Fields[1];
                if (fieldDeclaration.Initializer != null)
                {
                    defaultValue = LanguageManager.ExpressionGenerator.GenerateCode(fieldDeclaration.Initializer, info).Text;
                }
            }

#if USE_MACRO_FOR_ENUM
            mClassDefinitionData.Append("BEGIN_DECLARE_ENUM(" + passedText + ", " + type + ", " + defaultValue + ")\n");
            mClassDefinitionData.Indentation++;
#else
            mClassDefinitionData.Append("struct " + passedText + " : public CrossNetCore::__StructEnum__<" + type + " >\n");
            mClassDefinitionData.Append("{\n");
            mClassDefinitionData.Indentation++;

            // Add the dynamic ID definition for this type so we can box / unbox it...
            mClassDefinitionData.Append("CN_DYNAMIC_ID()\n");

            mClassDefinitionData.Append("public:\n");
            mClassDefinitionData.Append("static void __CreateInterfaceMap__();\n");
#endif

            mInterfaceWrapperList.Add(new StringTypeInfo(typeInfo.NonScopedFullName + "::__CreateInterfaceMap__", typeInfo));

            // Add the implementation in the method definition file
            string interfaceWrapper = typeInfo.NonScopedFullName + "::" + "__CreateInterfaceMap__";
            mMethodDefinitionData.Append("void " + interfaceWrapper + "()\n");
            mMethodDefinitionData.Append("{\n");
            mMethodDefinitionData.Indentation++;

            string sizeOfTypeName = typeInfo.FullName;
            if (typeInfo.IsValueType)
            {
                sizeOfTypeName = "::CrossNetRuntime::BoxedObject<" + sizeOfTypeName + " >";
            }

            mMethodDefinitionData.Append("void * * interfaceMap = ::CrossNetRuntime::InterfaceMapper::RegisterObject(sizeof(" + sizeOfTypeName + "));\n");
            mMethodDefinitionData.Append(typeInfo.FullName + "::s__InterfaceMap__ = interfaceMap;\n");

            mMethodDefinitionData.Indentation--;
            mMethodDefinitionData.Append("}\n");

            // Add the definition of the interface map
            mMethodDefinitionData.Append("void * * " + typeInfo.NonScopedFullName + "::s__InterfaceMap__ = NULL;\n");

#if !USE_MACRO_FOR_ENUM
            // Add the construction code with the corresponding constant...
            mClassDefinitionData.Append(passedText + "(" + type + " value)\n");
            mClassDefinitionData.Append("\t: CrossNetCore::__StructEnum__<" + type + " >(value)\n");
            mClassDefinitionData.Append("{\n");
            mClassDefinitionData.Append("}\n");

            mClassDefinitionData.Append(passedText + "()\n");
            mClassDefinitionData.Append("\t: CrossNetCore::__StructEnum__<" + type + " >(" + defaultValue + ")\n");
            mClassDefinitionData.Append("{\n");
            mClassDefinitionData.Append("}\n");

            // Also add the conversion operator to the corresponding size
            mClassDefinitionData.Append("operator " + type + "()\n");
            mClassDefinitionData.Append("{\n");
            mClassDefinitionData.Append("\treturn (mValue);\n");
            mClassDefinitionData.Append("}\n");
#endif

            // Now parse each enum (skipping the base type)
            for (int i = 1; i < count; ++i)
            {
                IFieldDeclaration fieldDeclaration = typeDeclaration.Fields[i];

                text = "static const " + type + " ";
                text += LanguageManager.ReferenceGenerator.GenerateCodeEnumDeclaration(fieldDeclaration, info).Text;
                text += ";\n";
                mClassDefinitionData.Append(text);
            }

            mClassDefinitionData.Indentation--;
            mClassDefinitionData.Append("END_DECLARE_ENUM\n");
        }

        public string CreateAnonymousClass(ITypeInfo declaringType)
        {
            string className = CppUtil.GetNextAnonymousClass();
            mAnonymousClasses[className] = new AnonymousClass(declaringType);
            return (className);
        }

        public string AddAnonymousMethod(string className, IMethodReturnType returnType, IParameterDeclarationCollection parameters, StringData methodBody, ParsingInfo info)
        {
            AnonymousClass anonymousClass = mAnonymousClasses[className];
            ITypeInfo declaringType = anonymousClass.DeclaringType;

            string methodDeclaration;
            string methodDefinition;

            string methodName = CppUtil.GetNextAnonymousMethod();

            string methodReturnType = LanguageManager.ReferenceGenerator.GenerateCodeTypeWithPostfix(returnType.Type, info).Text;
            string methodParameters = "(" + LanguageManager.ReferenceGenerator.GenerateCodeParameterDeclarationCollection(parameters, info).Text + ")";

            methodDeclaration = methodReturnType + " " + methodName + methodParameters + ";\n";

            // Hmmm, we may need to add the template code here if the declaring type is generic...
            methodDefinition = methodReturnType + " " + declaringType.NonScopedFullName + "::" + className + "::" + methodName + methodParameters + "\n";
            methodDefinition += methodBody;

            anonymousClass.MethodDeclarations.Add(methodDeclaration);
            anonymousClass.MethodDefinitions.Add(methodDefinition);

            return (methodName);
        }

        public void AddFieldForAnonymousMethod(string className, string fieldName, string fieldDeclaration)
        {
            AnonymousClass anonymousClass = mAnonymousClasses[className];
            anonymousClass.AddFieldDeclaration(fieldName, fieldDeclaration);
        }

        public void GenerateAnonymousClasses()
        {
            foreach (KeyValuePair<string, AnonymousClass> kvp in mAnonymousClasses)
            {
                string className = kvp.Key;
                AnonymousClass classObject = kvp.Value;

                // Define the class 
                mClassDefinitionData.Append("class " + className + " : public ::System::Object\n");
                mClassDefinitionData.Append("{\n");
                mClassDefinitionData.Indentation++;

                mClassDefinitionData.Append("public:\n");
                mClassDefinitionData.Append("CN_DYNAMIC_OBJECT_ID0(sizeof(" + className + "))\n");
                mClassDefinitionData.Append("static " + className + " * __Create__();\n");
                mClassDefinitionData.Append("virtual void __Trace__(unsigned char currentMark);\n");

                foreach (string methodDeclaration in classObject.MethodDeclarations)
                {
                    mClassDefinitionData.Append(methodDeclaration + "\n");
                }

                foreach (string fieldDeclaration in classObject.FieldDeclarations)
                {
                    mClassDefinitionData.Append(fieldDeclaration + ";\n");
                }

                mClassDefinitionData.Indentation--;
                mClassDefinitionData.Append("};\n");

                // Define now the methods
                foreach (string methodDefinition in classObject.MethodDefinitions)
                {
                    mMethodDefinitionData.Append(methodDefinition);
                }

                // We have to define the constructor
                mMethodDefinitionData.Append(classObject.DeclaringType.FullName + "::" + className + " * " + classObject.DeclaringType.NonScopedFullName + "::" + className + "::__Create__()\n");
                mMethodDefinitionData.Append("{\n");
                mMethodDefinitionData.Indentation++;
                mMethodDefinitionData.Append(className + " * __temp__ = new " + className + ";\n");
                mMethodDefinitionData.Append("__temp__->m__InterfaceMap__ = __GetInterfaceMap__();\n");
                mMethodDefinitionData.Append("return (__temp__);\n");
                mMethodDefinitionData.Indentation--;
                mMethodDefinitionData.Append("}\n");

                // Then the trace method...
                mMethodDefinitionData.Append("void " + classObject.DeclaringType.NonScopedFullName + "::" + className + "::__Trace__(unsigned char currentMark)\n");
                mMethodDefinitionData.Append("{\n");
                mMethodDefinitionData.Indentation++;
                foreach (string fieldName in classObject.FieldNames)
                {
                    mMethodDefinitionData.Append("CrossNetRuntime::Tracer::DoTrace(currentMark, " + fieldName + ");\n");
                }
                mMethodDefinitionData.Indentation--;
                mMethodDefinitionData.Append("}\n");

                // And finally, define the interface map
                mMethodDefinitionData.Append("void * * " + classObject.DeclaringType.NonScopedFullName + "::" + className + "::s__InterfaceMap__ = NULL;\n");

                mInterfaceWrapperList.Add(new StringTypeInfo(classObject.DeclaringType.FullName + "::" + className + "::__RegisterId__", classObject.DeclaringType));
            }

            mAnonymousClasses.Clear();
        }

        private const string ATTRIBUTE = "Attribute";
        private const string PASSED_INSTANCE = "__passed_instance__";
        private const string INSTANCE = "__instance__";

        private const string CLASS_DECLARATION_FILE = "_class_declaration.h";
        private const string CLASS_DEFINITION_FILE = "_class_definition.h";
        private const string METHOD_DEFINITION_FILE = "_method_definition.cpp";

        private StringData mClassDeclarationData;
        private StringData mClassDefinitionData;
        private StringData mMethodDefinitionData;
        private StringData mIncludesData;

        struct StringTypeInfo
        {
            public StringTypeInfo(string text, ITypeInfo typeInfo)
            {
                Text = text;
                TypeInfo = typeInfo;
            }

            public string Text;
            public ITypeInfo TypeInfo;
        }

        private IList<StringTypeInfo> mStaticConstructorList = new List<StringTypeInfo>();
        private IList<StringTypeInfo> mInterfaceWrapperList = new List<StringTypeInfo>();
        private IList<StringTypeInfo> mAssemblyTrace = new List<StringTypeInfo>();
        private IDictionary<string, Type> mDontGenerateTypes = new Dictionary<string, Type>();

        class AnonymousClass
        {
            public AnonymousClass(ITypeInfo declaringType)
            {
                DeclaringType = declaringType;
            }

            public ITypeInfo DeclaringType;
            public IList<string> MethodDeclarations = new List<string>();
            public IList<string> MethodDefinitions = new List<string>();

            public string[] FieldNames
            {
                get
                {
                    return (mFieldNames.ToArray());
                }
            }

            public string[] FieldDeclarations
            {
                get
                {
                    return (mFieldDeclarations.ToArray());
                }
            }

            public void AddFieldDeclaration(string fieldName, string fieldDeclaration)
            {
                mFieldNames.Add(fieldName);
                mFieldDeclarations.Add(fieldDeclaration);
            }

            private List<string> mFieldNames = new List<string>();
            private List<string> mFieldDeclarations = new List<string>();
        }

        private IDictionary<string, AnonymousClass> mAnonymousClasses = new Dictionary<string, AnonymousClass>();
    }
}
