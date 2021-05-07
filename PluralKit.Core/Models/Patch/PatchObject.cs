using System;

namespace PluralKit.Core
{

    public class InvalidPatchException : Exception
    {
        public InvalidPatchException(string message) : base(message) {}
    }

    public abstract class PatchObject
    {
        public abstract UpdateQueryBuilder Apply(UpdateQueryBuilder b);

        public void CheckIsValid() {}
    }
}