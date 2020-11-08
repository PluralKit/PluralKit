#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using Dapper;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PluralKit.Core
{
    [JsonConverter(typeof(PartialConverter))]
    public struct Partial<T>: IEnumerable<T>, IPartial
    {
        public bool IsPresent { get; }
        public T Value { get; }
        public object? RawValue => Value;

        private Partial(bool isPresent, T value)
        {
            IsPresent = isPresent;
            Value = value;
        }

        public static Partial<T> Null() => new Partial<T>(true, default!);
        public static Partial<T> Present(T obj) => new Partial<T>(true, obj);
        public static Partial<T> Absent = new Partial<T>(false, default!);

        public IEnumerable<T> ToArray() => IsPresent ? new[] {Value} : new T[0];

        public IEnumerator<T> GetEnumerator() => ToArray().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ToArray().GetEnumerator();

        public static implicit operator Partial<T>(T val) => Present(val);
    }

    public interface IPartial
    {
        public bool IsPresent { get; }
        public object? RawValue { get; }
    }

    public class PartialConverter: JsonConverter
    {
        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
                                         JsonSerializer serializer)
        {
            var innerType = objectType.GenericTypeArguments[0];
            var innerValue = serializer.Deserialize(reader, innerType);

            return typeof(Partial<>)
                    .MakeGenericType(innerType)
                    .GetMethod(nameof(Partial<object>.Present))!
                .Invoke(null, new[] {innerValue});
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((IPartial) value!).RawValue);
        }
        
        public override bool CanConvert(Type objectType) => true;

        public override bool CanRead => true;
        public override bool CanWrite => true;
    }

    public static class PartialExt
    {
        public static bool TryGet<T>(this Partial<T> pt, out T value)
        {
            value = pt.IsPresent ? pt.Value : default!;
            return pt.IsPresent;
        }

        public static T Or<T>(this Partial<T> pt, T fallback) => pt.IsPresent ? pt.Value : fallback;
        public static T Or<T>(this Partial<T> pt, Func<T> fallback) => pt.IsPresent ? pt.Value : fallback.Invoke();

        public static Partial<TOut> Map<TIn, TOut>(this Partial<TIn> pt, Func<TIn, TOut> fn) =>
            pt.IsPresent ? Partial<TOut>.Present(fn.Invoke(pt.Value)) : Partial<TOut>.Absent;

        public static Partial<TOut> Then<TIn, TOut>(this Partial<TIn> pt, Func<TIn, Partial<TOut>> fn) =>
            (pt.IsPresent && pt.RawValue != null) ? fn.Invoke(pt.Value) : Partial<TOut>.Absent;

        public static void Apply<T>(this Partial<T> pt, DynamicParameters bag, QueryBuilder qb, string fieldName)
        {
            if (!pt.IsPresent) return;

            bag.Add(fieldName, pt.Value);
            qb.Variable(fieldName, $"@{fieldName}");
        }
    }

    public class PartialContractResolver: DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            
            // We want to always ignore Partial<T> where IsPresent = false 
            if (typeof(IPartial).IsAssignableFrom(property.PropertyType))
                property.DefaultValueHandling = DefaultValueHandling.Ignore;
            
            return property;
        }
    }
}