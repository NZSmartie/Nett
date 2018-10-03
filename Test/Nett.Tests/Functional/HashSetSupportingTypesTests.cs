using System.Collections.Generic;
using Nett.Tests.Util;
using Xunit;

namespace Nett.Tests.Functional
{
    public sealed class HashSetSupportingTypesTests
    {
        [Fact]
        public void Serializtion_WithDictionaryProperty_SerializedDictionaryDirectlyAsKeyValuePairs()
        {
            // Arrange
            var r = new Root();

            // Act
            var tml = Toml.WriteString(r);

            // Assert
            tml.ShouldBeSemanticallyEquivalentTo(@"
RootSetting = ""SomeVal""
[Sect]
Prop = ""PropValue""");
        }

        private class Root
        {
            public string RootSetting { get; set; } = "SomeVal";

            public DataWrapper<Data> Sect { get; set; } = new DataWrapper<Data>(new Data());
        }

        private sealed class DataWrapper<T> : Dictionary<string, object>
        {
            public T Data { get; set; }

            public DataWrapper(T data)
            {
                this.Data = data;

                // This dictionary would be build via reflection etc. in real
                this.Add("Prop", "PropValue");
            }
        }

        private sealed class Data
        {
            public string Prop { get; set; } = "PropValue";
        }
    }
}
