using System;

namespace PluralKit.Core
{
    public interface INumericId<T, out TInner>: IEquatable<T>, IComparable<T>
        where T: INumericId<T, TInner>
        where TInner: IEquatable<TInner>, IComparable<TInner>
    {
        public TInner Value { get; }
    }
}