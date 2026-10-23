using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class NativePasteMenuTests
{
    private static ShellItem Paste(string title = "粘贴") => new(4, title, "", "paste", "paste", true);
    [Fact]
    public void MnemonicVariantReplacesPlaceholderInPlaceAndPreservesPresentation()
    {
        List<ShellMenuNode> nodes = [new("刷新", 1), new("粘贴(P)", 9, false, Mnemonic: "P", Shortcut: "Ctrl+V"), new("属性", 2)];
        List<ShellItem> items = [new(9, "粘贴(P)", "", "unknown", "", false)];
        NativePasteMenu.Merge(nodes, items, [Paste()], [], 65536);
        Assert.Equal(3, nodes.Count); Assert.Equal(65540, nodes[1].Command); Assert.True(nodes[1].Enabled);
        Assert.Equal("粘贴(P)", nodes[1].Title); Assert.Equal("P", nodes[1].Mnemonic); Assert.Equal("Ctrl+V", nodes[1].Shortcut);
        Assert.Single(items); Assert.Equal("paste", items[0].Verb); Assert.Equal(nodes[1].Command, items[0].Command);
    }
    [Fact]
    public void MissingPlaceholderIsInsertedBeforeFinalPropertiesBlock()
    {
        List<ShellMenuNode> nodes = [new("打开", 1), new("", IsSeparator: true), new("属性", 2)]; List<ShellItem> items = [];
        NativePasteMenu.Merge(nodes, items, [Paste()], [], 65536);
        Assert.Equal("粘贴", nodes[1].Title); Assert.True(nodes[2].IsSeparator); Assert.Equal("属性", nodes[3].Title);
    }
    [Fact]
    public void CanonicalCommandAndNestedSameTitleAreNotDuplicatedOrOverwritten()
    {
        var paste = Paste(); List<ShellMenuNode> nodes = [new("扩展", Children: [new("粘贴", 7)]), new("原生粘贴", 4)]; List<ShellItem> items = [paste];
        NativePasteMenu.Merge(nodes, items, [paste], [], 65536); Assert.Equal(2, nodes.Count); Assert.Single(items); Assert.Equal(7, nodes[0].Children![0].Command);
    }
    [Theory]
    [InlineData("粘贴（&P）", "粘贴")]
    [InlineData("Paste (&P)", "Paste")]
    [InlineData("动作(2)", "动作")]
    public void CaptionNormalizesOnlyAccessKeySuffix(string source, string expected) => Assert.Equal(expected, MenuComposer.Caption(source));
    [Fact]
    public void SubmenuItemSharingPlaceholderCommandIdIsPreserved()
    {
        List<ShellMenuNode> nodes = [new("扩展", Children: [new("子项", 9)]), new("粘贴(P)", 9, false)];
        List<ShellItem> items = [new(9, "子项", "扩展", "扩展|sub", "", true), new(9, "粘贴(P)", "", "|粘贴(P)", "", false)];
        NativePasteMenu.Merge(nodes, items, [Paste()], [], 65536);
        Assert.Equal(2, items.Count);
        Assert.Equal("扩展", items[0].Group); Assert.Equal(9, items[0].Command);
        Assert.Equal("", items[1].Group); Assert.Equal(65540, items[1].Command); Assert.Equal("paste", items[1].Verb);
    }
    [Fact]
    public void ViewHitRecoversWhenCenterIsCoveredByUpperFlyoutAndRejectsFullyCoveredTab()
    {
        var bounds = new NativeMethods.RECT { Left = 0, Top = 0, Right = 400, Bottom = 400 };
        Assert.True(ShellViewBinding.IsViewHit(bounds, pt => pt.Y > 250));
        Assert.False(ShellViewBinding.IsViewHit(bounds, _ => false));
    }
}
