using SCANOVA.Core.Models;
using SCANOVA.Infrastructure.Settings;
using Xunit;

namespace SCANOVA.Infrastructure.Tests;

public class JsonSettingsServiceTests
{
    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scanova-settings-{Guid.NewGuid():N}.json");
        var sut = new JsonSettingsService(filePath: path);

        await sut.LoadAsync();

        Assert.NotNull(sut.Current);
        Assert.Equal("pt-BR", sut.Current.Language);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scanova-settings-{Guid.NewGuid():N}.json");
        try
        {
            var sut = new JsonSettingsService(filePath: path);
            var settings = new AppSettings
            {
                Theme = "Dark",
                Automation = new AutomationSettings { AutoCrop = true, MinimumDetectionConfidence = 0.75 },
            };

            await sut.SaveAsync(settings);

            var reloaded = new JsonSettingsService(filePath: path);
            await reloaded.LoadAsync();

            Assert.Equal("Dark", reloaded.Current.Theme);
            Assert.True(reloaded.Current.Automation.AutoCrop);
            Assert.Equal(0.75, reloaded.Current.Automation.MinimumDetectionConfidence);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_RaisesSettingsChanged()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scanova-settings-{Guid.NewGuid():N}.json");
        try
        {
            var sut = new JsonSettingsService(filePath: path);
            AppSettings? raised = null;
            sut.SettingsChanged += (_, s) => raised = s;

            var settings = new AppSettings { Theme = "Light" };
            await sut.SaveAsync(settings);

            Assert.NotNull(raised);
            Assert.Equal("Light", raised!.Theme);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
