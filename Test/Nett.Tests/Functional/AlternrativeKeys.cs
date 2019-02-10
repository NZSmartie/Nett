using Nett.Attributes;
using Nett.Tests.Util;
using Xunit;

namespace Nett.Tests.Functional
{
    public sealed class AlternrativeKeys
    {
        [Fact]
        public void UseCustomKey_WithLamdaForPropSelector_SerializationUsesThatKey()
        {
            // Arrange
            var cfg = TomlSettings.Create(c => c
                .ConfigureType<FObj>(tc => tc
                    .Map(fo => fo.X).ToKey("TheKey")));

            // Act
            var tml = Toml.WriteString(new FObj(), cfg);

            // Assert
            tml.ShouldBeSemanticallyEquivalentTo(@"
TheKey=1");
        }

        [Fact]
        public void UseCustomKey_WithStringPrivateFieldSelector_SerializationUsesThatKey()
        {
            // Arrange
            var cfg = TomlSettings.Create(c => c
                .ConfigureType<FObj>(tc => tc
                    .Map("ThatsMine").ToKey("TheKey")));

            // Act
            var tml = Toml.WriteString(new FObj(), cfg);

            // Assert
            tml.ShouldBeSemanticallyEquivalentTo(@"
X=1
TheKey=""Youfoundme""");
        }

        [Fact]
        public void UseCustomKey_WithStringPropSelector_SerializationUsesThatKey()
        {
            // Arrange
            var cfg = TomlSettings.Create(c => c
                .ConfigureType<FObj>(tc => tc
                    .Map(nameof(FObj.X)).ToKey("TheKey")));

            // Act
            var tml = Toml.WriteString(new FObj(), cfg);

            // Assert
            tml.ShouldBeSemanticallyEquivalentTo("TheKey=1");
        }


        [Fact]
        public void Include_WithMemberSelector_CanBeUsedToIncludeOtherwiseIgnoredMembers()
        {
            // Arrange
            var cfg = TomlSettings.Create(c => c
                .ConfigureType<FObj>(tc => tc
                    .Include(fo => fo.PubField)));

            // Act
            var tml = Toml.WriteString(new FObj(), cfg);

            // Assert
            tml.ShouldBeSemanticallyEquivalentTo(@"
X = 1
PubField = ""Serialize all the things""
");
        }

        [Fact]
        public void Include_WithStringSelector_CanBeUsedToIncludePrivateMembers()
        {
            // Arrange
            var cfg = TomlSettings.Create(c => c
                .ConfigureType<FObj>(tc => tc
                    .Include("ThatsMine")));

            // Act
            var tml = Toml.WriteString(new FObj(), cfg);

            // Assert
            tml.ShouldBeSemanticallyEquivalentTo(@"
X = 1
ThatsMine = ""You found me""");
        }

        [Fact]
        public void WriteAttributedClass_WritesAllThePropertiesWithCorrectKeys()
        {
            // Arrange
            var instance = new AObj();

            // Act
            var tml = Toml.WriteString(instance);

            // Assert
            tml.ShouldBeSemanticallyEquivalentTo(@"
TheKey = 1
PubField =""Serialize all the things""
ThatsMine=""You found me""");
        }

        public class FObj
        {
            public int X { get; set; } = 1;

            public string PubField = "Serialize all the things";

            private string ThatsMine = "You found me";
        }

        public class AObj
        {
            [TomlMember(Key = "TheKey")]
            public int X { get; set; } = 1;

            [TomlMember]
            public string PubField = "Serialize all the things";

            [TomlMember]
            private string ThatsMine = "You found me";
        }
    }
}
