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

using Reflector.CodeModel;

using CrossNet.Common;

namespace CrossNet.Interfaces
{
    public interface ILocalTypeManager
    {
        StringData DoesNeedCast(LocalType destinationType, LocalType sourceType, StringData sourceValue);
        string DoesNeedCast(LocalType destinationType, LocalType sourceType);
        string DoesNeedCast(LocalType destinationType, LocalType sourceType, string sourceValue);
        LocalType GetLocalType(IType type);
        LocalType GetLocalType(IType type, string typeName);

        LocalType TypeUInt32
        {
            get;
        }
        LocalType TypeUInt16
        {
            get;
        }
        LocalType TypeInt16
        {
            get;
        }
        LocalType TypeInt64
        {
            get;
        }
        LocalType TypeUInt64
        {
            get;
        }
        LocalType TypeByte
        {
            get;
        }
        LocalType TypeSByte
        {
            get;
        }
        LocalType TypeBool
        {
            get;
        }
        LocalType TypeSingle
        {
            get;
        }
        LocalType TypeDouble
        {
            get;
        }
        LocalType TypeDecimal
        {
            get;
        }
        LocalType TypeChar
        {
            get;
        }
        LocalType TypeInt32
        {
            get;
        }
        LocalType TypeString
        {
            get;
        }
        LocalType TypeCharPointer
        {
            get;
        }
        LocalType TypeObject
        {
            get;
        }
        LocalType TypeNull
        {
            get;
        }
        LocalType TypeDefault
        {
            get;
        }
        LocalType TypeUnknown
        {
            get;
        }
        LocalType TypeArray
        {
            get;
        }
        LocalType TypePointer
        {
            get;
        }
        LocalType TypeOfType
        {
            get;
        }
        LocalType TypeDelegate
        {
            get;
        }
        LocalType TypeIEnumerable
        {
            get;
        }
        LocalType TypeIEnumerator
        {
            get;
        }
        LocalType TypeIDisposable
        {
            get;
        }
        LocalType TypeVoid
        {
            get;
        }

        // This type might not be needed actually,
        // It's a generic so it doesn't fit very well with the concept of generic
        LocalType TypeNullable
        {
            get;
        }
    }
}
