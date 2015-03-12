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
using System.Xml;

using Reflector;
using Reflector.CodeModel;
using Reflector.CodeModel.Memory;

using CrossNet.Common;
using CrossNet.Net;
using CrossNet.CppRuntime;
using CrossNet.CSharpRuntime;

namespace CrossNet.Net
{
    public class Provider
    {
        public static void Initialize()
        {
            sServiceProvider = null;
            foreach (Type type in typeof(Reflector.IPackage).Assembly.GetTypes())
            {
                Type[] interfaces = type.GetInterfaces();
                if (Array.IndexOf(interfaces, typeof(System.IServiceProvider)) != -1)
                {
                    object[] parameters = new object[] { null };
                    sServiceProvider = (IServiceProvider)Activator.CreateInstance(type, parameters);
                }
            }

            if (sServiceProvider != null)
            {
                sAssemblyManager = (IAssemblyManager)sServiceProvider.GetService(typeof(IAssemblyManager));
                sTranslatorManager = (ITranslatorManager)sServiceProvider.GetService(typeof(ITranslatorManager));
            }
        }

        public static void Initialize(IServiceProvider serviceProvider)
        {
            sServiceProvider = serviceProvider;
            sAssemblyManager = (IAssemblyManager)sServiceProvider.GetService(typeof(IAssemblyManager));
            sTranslatorManager = (ITranslatorManager)sServiceProvider.GetService(typeof(ITranslatorManager));
        }

        public static bool GenerateCodeFromConfigFile(string fileName)
        {
            string location1 = typeof(AppDomain).Assembly.Location;
            string location2 = typeof(Uri).Assembly.Location;

            IList<AssemblyData> assemblies = new List<AssemblyData>();
            List<string> dependencies = new List<string>();
            dependencies.Add(location1);
            dependencies.Add(location2);

            IList<string> excludedTypeNames = new List<string>();
            bool excludeDefaultTypes = true;
            bool playBeep = true;

            string language = "C++";
            string outputFolder = Path.GetTempPath();
            OutputMode outputMode = OutputMode.ThreeFilesPerAssembly;
            string outputSourceFolder = Path.GetTempPath();
            string outputHeaderFolder = Path.GetTempPath();
            bool generateImplementation = true;
            string mainInclude = Path.GetTempFileName();

            XmlReader reader = XmlReader.Create(fileName);
            XmlDocument doc = new XmlDocument();
            doc.Load(reader);

            XmlNode topNode = doc.ChildNodes[0];
            foreach (XmlNode childNode in topNode.ChildNodes)
            {
                switch (childNode.Name.ToLower())
                {
                    case DEPENDENCY:
                        dependencies.Add(childNode.InnerText);
                        break;

                    case EXPORT_LANGUAGE:
                        language = childNode.InnerText;
                        break;

                    case ASSEMBLY_TO_PARSE:
                        {
                            AssemblyData data = new AssemblyData(childNode.InnerText, outputFolder);
                            data.OutputSourceFolder = outputSourceFolder;
                            data.OutputHeaderFolder = outputHeaderFolder;
                            data.GenerateImplementation = generateImplementation;
                            data.Mode = outputMode;
                            data.MainInclude = mainInclude;
                            assemblies.Add(data);
                        }
                        break;

                    case EXCLUDE_TYPE:
                        excludedTypeNames.Add(childNode.InnerText);
                        break;

                    case EXCLUDE_DEFAULT_TYPES:
                        {
                            bool excludeDefault;
                            if (bool.TryParse(childNode.InnerText, out excludeDefault))
                            {
                                excludeDefaultTypes = excludeDefault;
                            }
                            else
                            {
                                Console.WriteLine("The XML tag " + childNode.Name + " doesn't have a correct value! It should be 'true' or 'false'.");
                                return (false);
                            }
                        }
                        break;

                    case OUTPUT_FOLDER:
                        outputFolder = childNode.InnerText;
                        break;

                    case OUTPUT_MODE:
                        {
                            try
                            {
                                outputMode = (OutputMode)Enum.Parse(typeof(OutputMode), childNode.InnerText, true);
                            }
                            catch
                            {
                                string text = "The XML tag " + childNode.Name + " cannot be converted. ";
                                text += "It should be '" + OutputMode.ThreeFilesPerAssembly.ToString() + "' ";
                                text += "or '" + OutputMode.TwoFilesPerClass.ToString() + "' .";
                                Console.WriteLine(text);
                                return (false);
                            }
                        }
                        break;

                    case OUTPUT_SOURCE_FOLDER:
                        outputSourceFolder = childNode.InnerText;
                        break;

                    case OUTPUT_HEADER_FOLDER:
                        outputHeaderFolder = childNode.InnerText;
                        break;

                    case GENERATE_IMPLEMENTATION:
                        {
                            bool result;
                            if (bool.TryParse(childNode.InnerText, out result))
                            {
                                generateImplementation = result;
                            }
                            else
                            {
                                Console.WriteLine("The XML tag " + childNode.Name + " cannot be converted to a boolean! It should be 'true' or 'false'.");
                                return (false);
                            }
                        }
                        break;

                    case MAIN_INCLUDE:
                        mainInclude = childNode.InnerText;
                        break;

                    case NO_BEEP:
                        playBeep = false;
                        break;

                    case COMMENT:
                        // It's a comment, do nothing...
                        break;

                    default:
                        Console.WriteLine("Unrecognized element " + childNode.Name + "! Skipped.");
                        Console.WriteLine("Elements expected:");
                        Console.WriteLine("\t" + DEPENDENCY);
                        Console.WriteLine("\t" + EXPORT_LANGUAGE + " = [C++|C#]");
                        Console.WriteLine("\t" + ASSEMBLY_TO_PARSE);
                        Console.WriteLine("\t" + EXCLUDE_TYPE);
                        Console.WriteLine("\t" + EXCLUDE_DEFAULT_TYPES + " = [true|false]");
                        Console.WriteLine("\t" + OUTPUT_FOLDER);
                        Console.WriteLine("\t" + NO_BEEP);
                        return (false);
                }
            }

            List<Type> excludedTypes = new List<Type>();

            if (excludeDefaultTypes)
            {
                // Primitive types
                excludedTypes.AddRange(new Type[]
                    {
                        typeof(Boolean),
                        typeof(SByte),
                        typeof(Byte),
                        typeof(Int16),
                        typeof(UInt16),
                        typeof(Char),
                        typeof(Int32),
                        typeof(UInt32),
                        typeof(Int64),
                        typeof(UInt64),
                        typeof(Single),
                        typeof(Double),
                        typeof(String),
                        typeof(Decimal),

                        // Base types
                        typeof(object),
                        typeof(ValueType),
                        typeof(Enum),
                        typeof(Array),
                        typeof(void),

                        // These interfaces are needed for the array
                        typeof(ICloneable),
                        typeof(System.Collections.IList),
                        typeof(System.Collections.ICollection),
                        typeof(System.Collections.IEnumerable),
                        typeof(System.Collections.IEnumerator),

                        typeof(System.Collections.Generic.IList<>),
                        typeof(System.Collections.Generic.ICollection<>),
                        typeof(System.Collections.Generic.IEnumerable<>),
                        typeof(System.Collections.Generic.IEnumerator<>),
                        typeof(System.IDisposable),

                        // These enums, classes are needed for the predefined types
                        typeof(System.Globalization.NumberStyles),
                        typeof(System.StringComparison),
                        typeof(System.CharEnumerator),
                        typeof(System.IFormattable),
                        typeof(System.IComparable),
                        typeof(System.IComparable<>),
                        typeof(System.IEquatable<>),
                        typeof(System.Text.StringBuilder),

                        // Also delegates are needed as well
                        typeof(System.Delegate),
                        typeof(System.MulticastDelegate)
                    });
            }

            // Now add the types defined by the user
            bool missedAType = false;
            foreach (string excludedTypeName in excludedTypeNames)
            {
                Type oneType = Type.GetType(excludedTypeName, false, true);
                if (oneType == null)
                {
                    Console.WriteLine("Type " + excludedTypeName + " not found! ");
                    missedAType = true;
                    continue;
                }
                excludedTypes.Add(oneType);
            }

            if (missedAType)
            {
                // We were not able to parse all the types
                Console.WriteLine("Could not find all the types, abort...");
                return (false);
            }

            switch (language.ToLower())
            {
                case "c++":
                    LanguageManager.Init(new CppTypeGenerator(excludedTypes.ToArray()), new CppStatementGenerator(),
                        new CppExpressionGenerator(), new CppReferenceGenerator(), new CppNameFixup(),
                        new CppLocalTypeManager(), new CppTypeInfoFactory());
                    break;

                case "c#":
                    LanguageManager.Init(new CSharpTypeGenerator(), new CSharpStatementGenerator(),
                        new CSharpExpressionGenerator(), new CSharpReferenceGenerator(), new CSharpNameFixup(),
                        new CSharpLocalTypeManager(), new CSharpTypeInfoFactory());
                    break;

                default:
                    Console.WriteLine("The element " + EXPORT_LANGUAGE + " doesn't have a correct value! It should be 'C++' or 'C#'. Export cancelled.");
                    return (false);
            }

            bool noError = true;
            Provider.Initialize();
            foreach (AssemblyData data in assemblies)
            {
                IAssembly assembly = Provider.LoadAssembly(data.AssemblyFileName, dependencies.ToArray());
                noError &= GenerateFiles(assembly, data);
            }

            if (playBeep)
            {
                if (noError)
                {
                    Console.Beep(100, 100);
                }
                else
                {
                    Console.Beep(1000, 500);
                }
            }
            return (noError);
        }

        public static IAssembly LoadAssembly(string assemblyLocation, string[] dependencies)
        {
            // First we read all the dependencies
            foreach (string oneDependency in dependencies)
            {
                // We could detect if the assembly is already loaded...
                // We let reflector handle that
                IAssembly oneAssembly = sAssemblyManager.LoadFile(oneDependency);
                sDependentsAssemblies.Add(oneAssembly);
            }

            IAssembly assembly = sAssemblyManager.LoadFile(assemblyLocation);

            // The translation in Reflector doesn't work recursively, nor doesn't work on some given types
            // (Like module). Try to reflect only what we need.
            // Simulate the translation of the whole assembly by translating some pieces of it

            IAssembly translatedAssembly = new Assembly();
            ITranslator translator = sTranslatorManager.CreateDisassembler("", "");

            foreach (IModule module in assembly.Modules)
            {
                IModule translatedModule = new Module();
                translatedModule.Name = module.Name;
                translatedAssembly.Modules.Add(translatedModule);

                foreach (ITypeDeclaration typeDeclaration in module.Types)
                {
                    ITypeDeclaration translatedTypeDeclaration;
                    translatedTypeDeclaration = TranslateTypeDeclaration(translator, typeDeclaration);
                    translatedModule.Types.Add(translatedTypeDeclaration);
                }
            }

            sLoadedAssemblies.Add(translatedAssembly);
            return (translatedAssembly);
        }

        public static IType GetEmbeddedType(Type type)
        {
            System.Reflection.Assembly a = type.Assembly;

            foreach (IAssembly oneAssembly in sDependentsAssemblies)
            {
                if (String.Compare(oneAssembly.Location, a.Location, true) != 0)
                {
                    continue;
                }

                foreach (IModule module in oneAssembly.Modules)
                {
                    foreach (ITypeDeclaration typeDeclaration in module.Types)
                    {
                        // Will have to add owner if needed...
                        string parsedType = typeDeclaration.Namespace + "." + typeDeclaration.Name;
                        if (type.FullName == parsedType)
                        {
                            return (typeDeclaration);
                        }
                    }
                }
            }
            return (null);
        }

        public static ITypeDeclaration TranslateTypeDeclaration(ITranslator translator, ITypeDeclaration type)
        {
            ITypeDeclaration translatedTypeDeclaration;
            // Translate the type without the body
            translatedTypeDeclaration = translator.TranslateTypeDeclaration(type, true, false);

            // Then translate the method
            // The reason is because during the translation of the type Reflector "forgets" some methods
            // Especially some constructor ans static constructors...
            translatedTypeDeclaration.Methods.Clear();      // Delete previous methods first...
            foreach (IMethodDeclaration method in type.Methods)
            {
                IMethodDeclaration translatedMethod = translator.TranslateMethodDeclaration(method);
                translatedTypeDeclaration.Methods.Add(translatedMethod);
            }

            int methodCount = type.Methods.Count;
            int translatedMethodCount = translatedTypeDeclaration.Methods.Count;

#if false   // Enable this code if you want to detect issue during the translation...
            {
                Trace.WriteLine("Type: " + type.Namespace + "." + type.Name);

                foreach (IMethodDeclaration m in type.Methods)
                {
                    Trace.WriteLine("\tBefore: " + m.Name);
                }

                foreach (IMethodDeclaration m in translatedTypeDeclaration.Methods)
                {
                    Trace.WriteLine("\tAfter:  " + m.Name);
                }

                Trace.WriteLine("");
            }
#endif

            // Also do the translation for nested types...
            translatedTypeDeclaration.NestedTypes.Clear();
            foreach (ITypeDeclaration nestedType in type.NestedTypes)
            {
                ITypeDeclaration translatedNestedType = TranslateTypeDeclaration(translator, nestedType);
                translatedTypeDeclaration.NestedTypes.Add(translatedNestedType);
            }

            return (translatedTypeDeclaration);
        }

        public static void UnloadAssembly(IAssembly assembly)
        {
            sAssemblyManager.Unload(assembly);

            sLoadedAssemblies.Remove(assembly);
            if (sLoadedAssemblies.Count == 0)
            {
                // Unloaded the last assembly, remove all dependencies
                // We could be smarter about it, but this should not create any issue
                sDependentsAssemblies.Clear();
            }
        }

        private static bool GenerateFiles(IAssembly assembly, AssemblyData data)
        {
            bool noError = true;
            GeneratedData generatedData = LanguageManager.GenerateCode(assembly, data);

            // At the end of the parsing write each file...
            foreach (KeyValuePair<string, StringData> oneFile in generatedData.Files)
            {
                string newFileName = data.OutputFolderName + oneFile.Key;
                try
                {
                    string text = oneFile.Value.Text;
                    // Remove all "\r"
                    text = text.Replace("\r", "");
                    // Resolve sequence "\n" to "\r\n"
                    text = text.Replace("\n", "\r\n");

                    Util.WriteFileIfChanged(newFileName, text);
                }
                catch
                {
                    Console.WriteLine("Cannot write the file '" + newFileName + "'.");
                    noError = false;
                }
            }
            return (noError);
        }

        public const string DEPENDENCY = "dependency";
        public const string EXPORT_LANGUAGE = "language";
        public const string ASSEMBLY_TO_PARSE = "assemblytoparse";
        public const string EXCLUDE_TYPE = "excludetype";
        public const string EXCLUDE_DEFAULT_TYPES = "excludedefaulttypes";
        public const string OUTPUT_FOLDER = "outputfolder";
        public const string NO_BEEP = "nobeep";
        public const string OUTPUT_MODE = "outputmode";
        public const string OUTPUT_SOURCE_FOLDER = "outputsourcefolder";
        public const string OUTPUT_HEADER_FOLDER = "outputheaderfolder";
        public const string GENERATE_IMPLEMENTATION = "generateimplementation";
        public const string MAIN_INCLUDE = "maininclude";
        public const string COMMENT = "#comment";

        static IServiceProvider sServiceProvider = null;
        static IAssemblyManager sAssemblyManager = null;
        static ITranslatorManager sTranslatorManager = null;
        static List<IAssembly> sDependentsAssemblies = new List<IAssembly>();
        static List<IAssembly> sLoadedAssemblies = new List<IAssembly>();
    }
}