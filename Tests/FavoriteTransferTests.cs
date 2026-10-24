using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class FavoriteTransferTests
{
    [Fact]
    public async Task ShellCompletionWaitsUntilTheWriterReleasesTheCompleteDestination()
    {
        string root = Path.Combine(Path.GetTempPath(), "BetterCM-completion-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string path = Path.Combine(root, "result.txt");
        try
        {
            var writer = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            Task<string> completion;
            try
            {
                completion = ShellOperationChecks.ReadCompletedAsync(path, "complete payload");
                await Task.Delay(80); Assert.False(completion.IsCompleted);
                await writer.WriteAsync(System.Text.Encoding.UTF8.GetBytes("complete payload"));
            }
            finally { await writer.DisposeAsync(); }
            Assert.Equal("complete payload", await completion.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void DateFolderUsesExecutionDateAndKeepsBasePath()
    {
        var favorite = new FavoriteDestination { Title = "归档", Path = @"C:\Archive", DateSubfolder = true };
        Assert.Equal(@"C:\Archive\2026-10-24", FavoriteTransfer.Destination(favorite, new(2026, 10, 24)));
        Assert.Equal(@"C:\Archive", favorite.Path); favorite.DateSubfolder = false;
        Assert.Equal(favorite.Path, FavoriteTransfer.Destination(favorite, new(2026, 10, 24)));
    }
    [Theory]
    [InlineData(@"C:\Source", @"C:\Source\inside")]
    [InlineData(@"C:\Source", @"C:\Source")]
    [InlineData(@"C:\Source", @"C:\")]
    public void SelfAndAlreadyContainedDestinationsAreRejected(string source, string destination)
        => Assert.Throws<IOException>(() => FavoriteTransfer.ValidateSelection(new() { Items = [new(source, true)] }, destination));
    [Fact]
    public void ValidPhysicalSelectionDoesNotProbeTheDestinationDuringValidation()
    {
        FavoriteTransfer.ValidateSelection(new() { Items = [new(@"C:\Source\a.txt", false)] }, @"\\offline\share\archive");
        Assert.Throws<InvalidOperationException>(() => FavoriteTransfer.ValidateSelection(new() { CurrentDirectory = @"C:\Source", ShellBackground = true }, @"C:\Archive"));
    }
    [Fact]
    public void ParentAndChildSelectionIsRejected()
        => Assert.Throws<IOException>(() => FavoriteTransfer.ValidateSelection(new() { Items = [new(@"C:\Source", true), new(@"C:\Source\a.txt", false)] }, @"C:\Archive"));
}
