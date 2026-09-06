using SCANOVA.Infrastructure.FileSystem;
using Xunit;

namespace SCANOVA.Infrastructure.Tests;

public class TempFileManagerTests
{
    [Fact]
    public void Constructor_CreatesSessionFolder()
    {
        var sut = new TempFileManager();

        Assert.True(Directory.Exists(sut.SessionFolder));

        sut.CleanupCurrentSession();
    }

    [Fact]
    public void CreateTempFilePath_ReturnsUniquePathsInsideSessionFolder()
    {
        var sut = new TempFileManager();

        var a = sut.CreateTempFilePath(".tmp");
        var b = sut.CreateTempFilePath("tmp");

        Assert.NotEqual(a, b);
        Assert.StartsWith(sut.SessionFolder, a);
        Assert.EndsWith(".tmp", b);

        sut.CleanupCurrentSession();
    }

    [Fact]
    public void CleanupCurrentSession_RemovesSessionFolder()
    {
        var sut = new TempFileManager();
        var filePath = sut.CreateTempFilePath(".txt");
        File.WriteAllText(filePath, "teste");

        sut.CleanupCurrentSession();

        Assert.False(Directory.Exists(sut.SessionFolder));
    }

    [Fact]
    public void CleanupStaleSessions_RemovesOtherSessionFolders_ButKeepsCurrent()
    {
        var first = new TempFileManager();
        var second = new TempFileManager();

        second.CleanupStaleSessions();

        Assert.False(Directory.Exists(first.SessionFolder));
        Assert.True(Directory.Exists(second.SessionFolder));

        second.CleanupCurrentSession();
    }
}
