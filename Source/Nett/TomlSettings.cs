namespace Nett
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Nett.Collections;
    using static System.Diagnostics.Debug;

    internal enum TomlCommentLocation
    {
        Prepend,
        Append,
    }

    internal struct SerializationMember
    {
        private readonly PropertyInfo pi;
        private readonly FieldInfo fi;

        public SerializationMember(PropertyInfo pi)
        {
            this.fi = null;
            this.pi = pi;
        }

        public SerializationMember(FieldInfo fi)
        {
            this.pi = null;
            this.fi = fi;
        }

        public MemberInfo MemberInfo
            => this.pi ?? (MemberInfo)this.fi;

        public Type MemberType
            => this.pi?.PropertyType ?? this.fi.FieldType;

        public object GetValue(object instance)
        {
            Assert(this.pi != null || this.fi != null);

            if (this.pi != null)
            {
                return this.pi.GetValue(instance, null);
            }
            else
            {
                return this.fi.GetValue(instance);
            }
        }

        public TomlKey GetKey()
        {
            Assert(this.pi != null || this.fi != null);

            string keyString = this.pi?.Name ?? this.fi.Name;

            return new TomlKey(keyString, TomlKey.KeyType.Bare);
        }

        public bool Is(MemberInfo pi)
            => this.pi != null && this.pi == pi;

        public void SetValue(object instance, object value)
        {
            if (this.pi != null) { this.pi.SetValue(instance, value, null); }
            else { this.fi.SetValue(instance, value); }
        }
    }

    internal struct SerializationInfo
    {
        private SerializationMember member;

        public SerializationMember Member => this.member;

        public SerializationInfo(PropertyInfo pi, TomlKey key)
        {
            this.member = new SerializationMember(pi);
            this.Key = key;
        }

        public SerializationInfo(FieldInfo fi, TomlKey key)
        {
            this.member = new SerializationMember(fi);
            this.Key = key;
        }

        public SerializationInfo(SerializationMember si, TomlKey key)
        {
            this.member = si;
            this.Key = key;
        }

        public static SerializationInfo CreateFromMemberInfo(MemberInfo mi, TomlKey key)
        {
            if (mi is PropertyInfo pi) { return new SerializationInfo(pi, key); }
            else if (mi is FieldInfo fi) { return new SerializationInfo(fi, key); }
            else { throw new ArgumentException($"Cannot create serialization info from unsupported member info type '{mi.GetType()}'."); }
        }

        public TomlKey Key { get; }

        public object GetValue(object instance)
            => this.member.GetValue(instance);

        public bool Is(MemberInfo pi)
            => this.member.Is(pi);
    }

    public sealed partial class TomlSettings
    {
        internal const BindingFlags PropBindingFlags = BindingFlags.Public | BindingFlags.Instance;

        internal static readonly TomlSettings DefaultInstance = Create();

        private readonly Dictionary<Type, Func<object>> activators = new Dictionary<Type, Func<object>>();
        private readonly ConverterCollection converters = new ConverterCollection();
        private readonly HashSet<Type> inlineTableTypes = new HashSet<Type>();
        private readonly Dictionary<string, Type> tableKeyToTypeMappings = new Dictionary<string, Type>();
        private readonly Dictionary<Type, HashSet<SerializationMember>> ignoredMembers = new Dictionary<Type, HashSet<SerializationMember>>();
        private readonly Map<SerializationInfo, string> explicitMembers = new Map<SerializationInfo, string>();

        private IKeyGenerator keyGenerator = KeyGenerators.Instance.PropertyName;
        private ITargetPropertySelector mappingPropertySelector = TargetPropertySelectors.Instance.Exact;

        private TomlCommentLocation defaultCommentLocation = TomlCommentLocation.Prepend;

        private TomlSettings()
        {
        }

        public static TomlSettings Create() => Create(_ => { });

        public static TomlSettings Create(Action<ITomlSettingsBuilder> cfg)
        {
            var config = new TomlSettings();
            var builder = new TomlSettingsBuilder(config);
            cfg(builder);
            builder.SetupConverters();
            return config;
        }

        internal object GetActivatedInstance(Type t)
        {
            Func<object> a;
            if (this.activators.TryGetValue(t, out a))
            {
                return a();
            }
            else
            {
                try
                {
                    return Activator.CreateInstance(t);
                }
                catch (MissingMethodException exc)
                {
                    throw new InvalidOperationException(string.Format(
                        "Failed to create type '{1}'. Only types with a " +
                        "parameterless constructor or an specialized creator can be created. Make sure the type has " +
                        "a parameterless constructor or a configuration with an corresponding creator is provided.",
                        exc.Message,
                        t.FullName));
                }
            }
        }

        internal TomlCommentLocation GetCommentLocation(TomlComment c)
        {
            switch (c.Location)
            {
                case CommentLocation.Append: return TomlCommentLocation.Append;
                case CommentLocation.Prepend: return TomlCommentLocation.Prepend;
                default: return this.defaultCommentLocation;
            }
        }

        internal TomlTable.TableTypes GetTableType(Type valType)
        {
            if (this.inlineTableTypes.Contains(valType)
                || valType.GetCustomAttributes(false).Any((a) => a.GetType() == typeof(TreatAsInlineTableAttribute)))
            {
                return TomlTable.TableTypes.Inline;
            }

            return TomlTable.TableTypes.Default;
        }

        internal IEnumerable<SerializationInfo> GetSerializationMembers(Type t)
        {
            return StaticTypeMetaData.GetSerializationMembers(t, this.keyGenerator)
                .Where(si => IncludeMember(si.Member.MemberInfo))
                .Concat(this.explicitMembers.Forward.Keys);

            bool IncludeMember(MemberInfo mi)
            {
                return !this.IsMemberIgnored(t, mi)
                    && !this.explicitMembers.Forward.Keys.Any(si => si.Is(mi));
            }
        }

        internal IEnumerable<TomlComment> GetComments(Type type, SerializationMember m)
            => StaticTypeMetaData.GetComments(type, m);

        internal SerializationMember? TryGetMappedMember(Type t, string key)
        {
            if (this.explicitMembers.Reverse.TryGetValue(key, out var si))
            {
                return si.Member;
            }

            var pi = this.mappingPropertySelector.TryGetTargetProperty(key, t);
            return pi != null && !this.IsMemberIgnored(t, pi) ? new SerializationMember(pi) : (SerializationMember?)null;
        }

        internal ITomlConverter TryGetConverter(Type from, Type to) =>
            this.converters.TryGetConverter(from, to);

        internal Type TryGetMappedType(string key, SerializationMember? target)
        {
            bool targetCanHoldMappedTable = !target.HasValue || target.Value.MemberType == Types.ObjectType;
            if (targetCanHoldMappedTable && this.tableKeyToTypeMappings.TryGetValue(key, out var mapped))
            {
                return mapped;
            }

            return null;
        }

        internal ITomlConverter TryGetToTomlConverter(Type fromType) =>
            this.converters.TryGetLatestToTomlConverter(fromType);

        private bool IsMemberIgnored(Type ownerType, MemberInfo mi)
        {
            Assert(ownerType != null);
            Assert(mi != null);

            if (StaticTypeMetaData.IsMemberIgnored(ownerType, mi))
            {
                return true;
            }

            if (this.ignoredMembers.TryGetValue(ownerType, out var ignored))
            {
                return ignored.Any(m => m.Is(mi));
            }

            return false;
        }

        private sealed class ConverterCollection
        {
            private static readonly ToMatchingClrTypeConverter DirectConv = new ToMatchingClrTypeConverter();
            private readonly List<ITomlConverter> converters = new List<ITomlConverter>(64);

            public ConverterCollection()
            {
                this.converters.Add(DirectConv);
            }

            public void Add(ITomlConverter converter)
                => this.converters.Insert(1, converter);

            public void AddRange(IEnumerable<ITomlConverter> converters) => this.converters.InsertRange(1, converters);

            public ITomlConverter TryGetConverter(Type from, Type to) => this.converters.FirstOrDefault(c => c.CanConvertFrom(from) && c.CanConvertTo(to));

            public ITomlConverter TryGetLatestToTomlConverter(Type from) =>
                this.converters.FirstOrDefault(c => c.CanConvertFrom(from) && c.CanConvertToToml());

            private class ToMatchingClrTypeConverter : ITomlConverter<TomlObject, object>
            {
                public Type FromType
                    => typeof(TomlObject);

                public TomlObjectType? TomlTargetType => null;

                public bool CanConvertFrom(Type t)
                    => Types.TomlObjectType.IsAssignableFrom(t);

                public bool CanConvertTo(Type t)
                    => t == typeof(object);

                public bool CanConvertToToml()
                    => false;

                public object Convert(ITomlRoot root, TomlObject src, Type targetType)
                    => this.Convert(root, (object)src, targetType);

                public object Convert(ITomlRoot root, object value, Type targetType)
                {
                    switch (value)
                    {
                        case TomlValue val: return val.UntypedValue;
                        case TomlTable tbl: return tbl.ToDictionary();
                        case TomlTableArray tarr: return tarr.Items.Select(i => this.Convert(root, i, typeof(object)));
                        default: return value;
                    }
                }
            }
        }
    }
}
