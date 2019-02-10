using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nett.Attributes;
using Nett.Util;

namespace Nett
{
    internal static class StaticTypeMetaData
    {
        private static readonly ConcurrentDictionary<Type, MetaDataInfo> MetaData = new ConcurrentDictionary<Type, MetaDataInfo>();

        public static IEnumerable<SerializationInfo> GetSerializationMembers(Type type, IKeyGenerator keyGen)
        {
            EnsureMetaDataInitialized(type);

            var data = MetaData[type];

            return data.ImplicitMembers
                .Select(sm => new SerializationInfo(sm, new TomlKey(keyGen.GetKey(sm.MemberInfo))))
                .Concat(data.ExplicitMembers);
        }

        public static bool IsMemberIgnored(Type t, MemberInfo mi)
        {
            EnsureMetaDataInitialized(t);

            return MetaData[t].IgnoredMembes.Any(m => m.Is(mi));
        }

        private static void EnsureMetaDataInitialized(Type t)
            => Extensions.DictionaryExtensions.AddIfNeeded(MetaData, t, () => ProcessType(t));

        private static MetaDataInfo ProcessType(Type t)
        {
            var implicitMembers = ResolveImplicitMembers(t);
            var explicitMembers = ResolveExplicitMembers(t);
            var ignoredMembers = ResolveIgnoredMembers(t);

            return new MetaDataInfo(implicitMembers, explicitMembers, ignoredMembers);
        }

        private static IEnumerable<SerializationMember> ResolveImplicitMembers(Type t)
        {
            return t.GetProperties(TomlSettings.PropBindingFlags)
                .Where(IncludeMember)
                .Select(pi => new SerializationMember(pi));

            bool IncludeMember(PropertyInfo pi)
                => ReflectionUtil.GetCustomAttribute<TomlIgnoreAttribute>(pi, inherit: true) == null
                && ReflectionUtil.GetCustomAttribute<TomlMember>(pi, inherit: true) == null;
        }

        private static IEnumerable<SerializationMember> ResolveIgnoredMembers(Type t)
        {
            return t.GetProperties(TomlSettings.PropBindingFlags)
                .Where(pi => ReflectionUtil.GetCustomAttribute<TomlIgnoreAttribute>(pi, inherit: true) != null)
                .Select(pi => new SerializationMember(pi));
        }

        private static IEnumerable<SerializationInfo> ResolveExplicitMembers(Type t)
        {
            var members = t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var m in members)
            {
                var tm = ReflectionUtil.GetCustomAttribute<TomlMember>(m);
                if (tm != null)
                {
                    var key = string.IsNullOrWhiteSpace(tm.Key)

                        ? new TomlKey(m.Name, TomlKey.KeyType.Bare)
                        : new TomlKey(tm.Key);

                    yield return SerializationInfo.CreateFromMemberInfo(m, key);
                }
            }
        }

        private sealed class MetaDataInfo
        {

            public MetaDataInfo(
                IEnumerable<SerializationMember> implicitMembers,
                IEnumerable<SerializationInfo> explicitMembers,
                IEnumerable<SerializationMember> ignoredMembers)
            {
                this.ImplicitMembers = new HashSet<SerializationMember>(implicitMembers);
                this.ExplicitMembers = new HashSet<SerializationInfo>(explicitMembers);
                this.IgnoredMembes = new HashSet<SerializationMember>(ignoredMembers);
            }

            public HashSet<SerializationMember> ImplicitMembers { get; }

            public HashSet<SerializationInfo> ExplicitMembers { get; }

            public HashSet<SerializationMember> IgnoredMembes { get; }
        }


    }
}
