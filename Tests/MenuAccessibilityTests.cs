using System.Windows.Automation;
using System.Windows.Automation.Peers;
using BetterCM.Models;
using BetterCM.Views;
using Xunit;
namespace BetterCM.Tests;

public sealed class MenuAccessibilityTests
{
    [Fact] public async Task MenuPeerReportsRoleNameCheckedAndDisabledState()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var button = new MenuItemButton { Tag = new MenuEntry { Title = "测试动作", Checked = true }, IsEnabled = false };
                AutomationProperties.SetName(button, "测试动作");
                var peer = UIElementAutomationPeer.CreatePeerForElement(button);
                Assert.NotNull(peer);
                Assert.Equal(AutomationControlType.MenuItem, peer.GetAutomationControlType());
                Assert.Equal("测试动作", peer.GetName());
                Assert.False(peer.IsEnabled());
                Assert.Contains("已勾选", peer.GetItemStatus());
                Assert.Contains("不可用", peer.GetItemStatus());
                done.SetResult();
            }
            catch (Exception ex) { done.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
