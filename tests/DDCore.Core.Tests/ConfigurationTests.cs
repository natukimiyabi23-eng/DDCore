using DDCore.Core.Configuration;
using DDCore.Core.Modes;

namespace DDCore.Core.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void DefaultConfigurationUsesObserver()
    {
        var configuration = new DDCoreConfiguration();

        Assert.Equal(DDCoreConfiguration.CurrentVersion, configuration.Version);
        Assert.Equal(OperationMode.Observer, configuration.Mode);
    }

    [Theory]
    [InlineData(OperationMode.Advisor)]
    [InlineData(OperationMode.Simulation)]
    [InlineData(OperationMode.Assisted)]
    [InlineData(OperationMode.Autonomous)]
    public void StartupMigrationAlwaysReturnsToObserver(OperationMode savedMode)
    {
        var configuration = new DDCoreConfiguration { Mode = savedMode };

        var result = ConfigurationMigrator.NormalizeForStartup(configuration);

        Assert.True(result.Changed);
        Assert.Equal(OperationMode.Observer, result.Configuration.Mode);
    }

    [Fact]
    public void ConfigurationServiceLoadsMigratesAndSaves()
    {
        var persistence = new FakePersistence
        {
            Stored = new DDCoreConfiguration
            {
                Version = 0,
                Mode = OperationMode.Autonomous,
                EmergencyStopLatched = true,
            },
        };

        var service = new ConfigurationService(persistence);

        Assert.Equal(OperationMode.Observer, service.Current.Mode);
        Assert.True(service.Current.EmergencyStopLatched);
        Assert.Equal(DDCoreConfiguration.CurrentVersion, service.Current.Version);
        Assert.Equal(1, persistence.SaveCount);

        service.Current.ShowSafetyDetails = false;
        service.Save();

        Assert.Equal(2, persistence.SaveCount);
        Assert.False(persistence.Stored!.ShowSafetyDetails);
    }

    private sealed class FakePersistence : IConfigurationPersistence
    {
        public DDCoreConfiguration? Stored { get; set; }
        public int SaveCount { get; private set; }

        public DDCoreConfiguration? Load() => Stored;

        public void Save(DDCoreConfiguration configuration)
        {
            Stored = configuration;
            SaveCount++;
        }
    }
}
