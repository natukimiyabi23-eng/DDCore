using DDCore.Core.Configuration;
using DDCore.Core.Logging;
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

    [Fact]
    public void LoadFailureRecoversToObserverWithEmergencyStopAndPersistsSafeDefaults()
    {
        var persistence = new FakePersistence { LoadException = new InvalidDataException("broken") };
        var logger = new RecordingLogger();

        var service = new ConfigurationService(persistence, logger);

        Assert.True(service.RecoveredFromLoadFailure);
        Assert.Equal(OperationMode.Observer, service.Current.Mode);
        Assert.True(service.Current.EmergencyStopLatched);
        Assert.True(service.IsPersistenceHealthy);
        Assert.Equal(1, persistence.SaveCount);
        Assert.Single(logger.Errors);
        Assert.Contains(service.StartupChanges, change => change.Contains("安全設定へ復旧", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadAndRecoverySaveFailuresKeepSafeInMemoryConfiguration()
    {
        var persistence = new FakePersistence
        {
            LoadException = new InvalidDataException("broken"),
            SaveException = new IOException("read-only"),
        };
        var logger = new RecordingLogger();

        var service = new ConfigurationService(persistence, logger);

        Assert.Equal(OperationMode.Observer, service.Current.Mode);
        Assert.True(service.Current.EmergencyStopLatched);
        Assert.False(service.IsPersistenceHealthy);
        Assert.Contains("安全設定を保存", service.LastPersistenceError);
        Assert.Equal(2, logger.Errors.Count);
    }

    [Fact]
    public void RuntimeSaveFailureIsLoggedWithoutDiscardingInMemorySafetyState()
    {
        var persistence = new FakePersistence { Stored = new DDCoreConfiguration() };
        var logger = new RecordingLogger();
        var service = new ConfigurationService(persistence, logger);
        persistence.SaveException = new IOException("disk full");
        service.Current.Mode = OperationMode.Observer;
        service.Current.EmergencyStopLatched = true;

        var saved = service.Save();

        Assert.False(saved);
        Assert.False(service.IsPersistenceHealthy);
        Assert.Equal(OperationMode.Observer, service.Current.Mode);
        Assert.True(service.Current.EmergencyStopLatched);
        Assert.Contains("現在のセッション", service.LastPersistenceError);
        Assert.Single(logger.Errors);
    }

    private sealed class FakePersistence : IConfigurationPersistence
    {
        public DDCoreConfiguration? Stored { get; set; }
        public int SaveCount { get; private set; }
        public Exception? LoadException { get; set; }
        public Exception? SaveException { get; set; }

        public DDCoreConfiguration? Load()
        {
            if (LoadException is not null)
            {
                throw LoadException;
            }

            return Stored;
        }

        public void Save(DDCoreConfiguration configuration)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            Stored = configuration;
            SaveCount++;
        }
    }

    private sealed class RecordingLogger : IDDLogger
    {
        public List<(string Message, Exception? Exception)> Errors { get; } = [];

        public void Debug(string message)
        {
        }

        public void Information(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Error(string message, Exception? exception = null) =>
            Errors.Add((message, exception));
    }
}
