using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CrossNet.Common
{
    public enum OutputMode
    {
        ThreeFilesPerAssembly,
        TwoFilesPerClass
    }

    public class AssemblyData
    {
        public AssemblyData(string assemblyFileName, string outputFolderName)
        {
            AssemblyFileName = assemblyFileName;
            OutputFolderName = outputFolderName;
            AssemblyName = Path.GetFileNameWithoutExtension(assemblyFileName).ToLower();
        }

        public string AssemblyFileName;
        public string OutputFolderName;
        public string OutputSourceFolder;
        public string OutputHeaderFolder;
        public bool GenerateImplementation;
        public OutputMode Mode;
        public string MainInclude;
        public string AssemblyName;
    }
}
