using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
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
            tml.ShouldBeSemanticallyEquivalentTo("");
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
            tml.ShouldBeSemanticallyEquivalentTo("");
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
            tml.ShouldBeSemanticallyEquivalentTo("");
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
            tml.ShouldBeSemanticallyEquivalentTo("");
        }

        [Fact]
        public void Include_WithStringSelector_CanBeUsedToIncludeOtherwiseIgnoredMembers()
        {
            // Arrange
            var cfg = TomlSettings.Create(c => c
                .ConfigureType<FObj>(tc => tc
                    .Include("ThatsMine")));

            // Act
            var tml = Toml.WriteString(new FObj(), cfg);

            // Assert
            tml.ShouldBeSemanticallyEquivalentTo("");
        }

        public class FObj
        {
            public int X { get; set; } = 1;

            public string PubField = "Serialize all the things";

            private string ThatsMine = "You found me";
        }
    }
}
