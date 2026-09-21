using WindowsLab.Backup;
using WindowsLab.Core;
using Xunit;

namespace WindowsLab.Backup.Tests;

public sealed class FileBackupStoreTests
{
    [Fact]
    public void Create_Get_List_round_trip()
    {
        var root = Path.Combine(Path.GetTempPath(), "WindowsLab-backup-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileBackupStore(root);
            var manifest = store.Create(new BackupCreateRequest(
                "unit-test",
                ["explorer.show-extensions"],
                RiskLevel.Low,
                [
                    new BackupRegistryEntry(
                        "explorer.show-extensions",
                        "HKCU",
                        @"Software\Test",
                        "HideFileExt",
                        "DWord",
                        "1",
                        true)
                ],
                [],
                [],
                [],
                [new BackupLogicalEntry("explorer.show-extensions", "1", "0", false)],
                new BackupRestorePointInfo(true, false, null, "skipped in unit test")));

            Assert.False(string.IsNullOrWhiteSpace(manifest.BackupId));
            var loaded = store.GetManifest(manifest.BackupId);
            Assert.NotNull(loaded);
            Assert.Equal("unit-test", loaded!.Reason);
            Assert.Single(loaded.Registry);
            Assert.Single(store.List());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Restore_invokes_restorer_in_reverse()
    {
        var root = Path.Combine(Path.GetTempPath(), "WindowsLab-backup-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileBackupStore(root);
            var manifest = store.Create(new BackupCreateRequest(
                "restore-test",
                ["a", "b"],
                RiskLevel.Low,
                [
                    new BackupRegistryEntry("a", "HKCU", @"Software\A", "X", "DWord", "1", true),
                    new BackupRegistryEntry("b", "HKCU", @"Software\B", "Y", "DWord", "2", true)
                ],
                [],
                [],
                [],
                [],
                null));

            var fake = new FakeRestorer();
            var result = store.Restore(manifest.BackupId, fake);
            Assert.True(result.Succeeded);
            Assert.Equal(2, result.RestoredCount);
            Assert.Equal(["b", "a"], fake.RegistryOrder);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FakeRestorer : IBackupRestorer
    {
        public List<string> RegistryOrder { get; } = [];

        public void RestoreRegistry(BackupRegistryEntry entry) => RegistryOrder.Add(entry.TweakId);

        public void RestoreService(BackupServiceEntry entry)
        {
        }

        public void RestoreTask(BackupTaskEntry entry)
        {
        }

        public void RestorePower(BackupPowerEntry entry)
        {
        }
    }
}
