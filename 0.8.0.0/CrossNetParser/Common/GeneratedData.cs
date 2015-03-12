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

namespace CrossNet.Common
{
    public class FileInfo
    {
        public string Type;
        public StringData Data;
    }

    public class GeneratedData
    {
        public GeneratedData()
        {
            // Do nothing...
        }

        public StringData this[string fileType]
        {
            get
            {
                return (mFiles[fileType]);
            }
            set
            {
                mFiles[fileType] = value;
            }
        }

        public IDictionary<string, StringData> Files
        {
            get
            {
                return (mFiles);
            }
        }

        public void AddFile(string fileType, StringData data)
        {
            mFiles.Add(fileType, data);
        }

        public void Append(string fileType, StringData data)
        {
            StringData file = mFiles[fileType];
            file.Append(data);
        }

        public void Append(GeneratedData data)
        {
            throw new Exception("Not implemented yet!");
        }

        private IDictionary<string, StringData> mFiles = new Dictionary<string, StringData>();

    }
}
