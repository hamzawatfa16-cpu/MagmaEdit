using MagmaEdit.Core.Projects;

namespace MagmaEdit.Core.Tests;

public sealed class ProjectNameRulesTests
{
    [Fact]
    public void NormalizeTrimsOuterWhitespace()
    {
        Assert.Equal("My Project", ProjectNameRules.Normalize("  My Project  "));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("folder/project")]
    [InlineData("folder\\project")]
    [InlineData("line\nname")]
    public void NormalizeRejectsUnsafeNames(string name)
    {
        Assert.Throws<ArgumentException>(() => ProjectNameRules.Normalize(name));
    }

    [Fact]
    public void NormalizeRejectsNamesLongerThanMaximum()
    {
        string name = new('x', ProjectNameRules.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => ProjectNameRules.Normalize(name));
    }

    [Fact]
    public void ProjectDocumentCreatePersistsNormalizedName()
    {
        ProjectDocument project = ProjectDocument.Create("  My Project  ");

        Assert.Equal("My Project", project.Name);
    }
}
