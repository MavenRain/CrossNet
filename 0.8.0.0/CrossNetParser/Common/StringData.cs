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

using CrossNet.Net;

namespace CrossNet.Common
{
    public class StringData
    {
        public StringData()
        {
            mText = new StringBuilder();
        }

        public StringData(string text)
        {
            mText = new StringBuilder(text);
        }

        public StringData(StringData data)
        {
            mText = new StringBuilder(data.Text);
        }

        /// <summary>
        /// On purpose there is no set Text property as it should happen rarely
        /// Either create a new StringData, or call Clear(), then AppendSameLine() with the text you want to set.
        /// </summary>
        public String Text
        {
            get
            {
                return (mText.ToString());
            }
        }

        public int Indentation
        {
            get
            {
                return (mIndentation);
            }
            set
            {
                Debug.Assert(value >= 0);
                mIndentation = value;
                mIndentationString = new String(' ', mIndentation * 4);
            }
        }

        public override string ToString()
        {
            return (mText.ToString());
        }

        public void Append(StringData data)
        {
            Append(data.Text);
        }

        public void AppendSameLine(StringData data)
        {
            mText.Append(data.Text);
        }

        public void AppendSameLine(string text)
        {
            mText.Append(text);
        }

        public void PrefixSameLine(StringData data)
        {
            mText.Insert(0, data.Text);
        }

        public void PrefixSameLine(string text)
        {
            mText.Insert(0, text);
        }

        public void TrimOneCharLeft(char oneChar)
        {
            if (mText.Length == 0)
            {
                return;
            }
            if (mText[mText.Length - 1] == oneChar)
            {
                mText.Length = mText.Length - 1;
            }
        }

        public void Replace(string newString)
        {
            mText.Length = 0;
            mText.Append(newString);
        }

        public void Append(string text)
        {
            if (mIndentation == 0)
            {
                mText.Append(text);
            }
            else
            {
                // We need to append everything but each line has to be indented
                if (text != "")
                {
                    String[] strs = text.Split('\n');
                    int count = strs.Length;
                    int i;
                    for (i = 0 ; i < count - 1 ; ++i)
                    {
                        mText.Append(mIndentationString).Append(strs[i]).Append('\n');
                    }
                    // for the last string, don't add another "\n" if there was none
                    if (strs[i] != "")
                    {
                        mText.Append(mIndentationString).Append(strs[i]);
                    }
                }
            }
        }

        public void Append(int localIndentation, StringData data)
        {
            Indentation += localIndentation;
            Append(data);
            Indentation -= localIndentation;
        }

        public void Append(int localIndentation, string text)
        {
            Indentation += localIndentation;
            Append(text);
            Indentation -= localIndentation;
        }

        public StringBuilder mText;
        public String mIndentationString = "";
        public int mIndentation = 0;

        public IType EmbeddedType
        {
            get
            {
                return (mReturnedType.EmbeddedType);
            }
            set
            {
                // Do the look up only when setting it...
                mReturnedType = LanguageManager.LocalTypeManager.GetLocalType(value);
            }
        }

        public LocalType LocalType
        {
            get
            {
                return (mReturnedType);
            }
            set
            {
                mReturnedType = value;
            }
        }

        private LocalType mReturnedType;
    }
}
