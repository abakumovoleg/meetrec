using Meetrec.Core.Models;

namespace Meetrec.Core.Tests;

public class ModelCatalogTests
{
    [Fact]
    public void All_Ids_AreUnique()
    {
        // Arrange
        var ids = ModelCatalog.All.Select(m => m.Id).ToList();

        // Act
        var distinct = ids.Distinct(StringComparer.OrdinalIgnoreCase).Count();

        // Assert
        Assert.Equal(ids.Count, distinct);
    }

    [Fact]
    public void WhisperModel_EveryCliName_HasBinaryAndCoreMlEncoderSideBySide()
    {
        // Arrange
        var names = ModelCatalog.WhisperNames;

        // Act
        var pairs = names.Select(n => (Bin: ModelCatalog.WhisperModel(n), CoreMl: ModelCatalog.WhisperCoreMlEncoder(n))).ToList();

        // Assert
        Assert.Contains(ModelCatalog.DefaultWhisper, names);
        Assert.All(pairs, p =>
        {
            Assert.Equal(p.Bin.InstallName.Replace(".bin", "-encoder.mlmodelc"), p.CoreMl.InstallName);
            Assert.Equal(ModelPackaging.ZippedDirectory, p.CoreMl.Packaging);
        });
    }

    [Fact]
    public void Find_UnknownId_Throws()
    {
        // Arrange
        var id = "whisper-huge";

        // Act
        var act = () => ModelCatalog.Find(id);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }
}
