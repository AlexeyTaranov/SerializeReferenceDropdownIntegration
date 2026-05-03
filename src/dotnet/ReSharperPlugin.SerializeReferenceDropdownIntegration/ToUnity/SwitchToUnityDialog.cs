using System;
using JetBrains.DataFlow;
using JetBrains.IDE.UI;
using JetBrains.IDE.UI.Extensions;
using JetBrains.Lifetimes;
using JetBrains.Rider.Model.UIAutomation;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

public sealed class SwitchToUnityDialog
{
    private const string DialogId = "SerializeReferenceDropdown.SwitchToUnity";

    private readonly IDialogHost dialogHost;
    private readonly Lifetime lifetime;
    private readonly Action<bool> switchToUnity;
    private readonly Action<bool> doNotSwitchToUnity;

    public SwitchToUnityDialog(IDialogHost dialogHost, Lifetime lifetime,
        Action<bool> switchToUnity, Action<bool> doNotSwitchToUnity)
    {
        this.dialogHost = dialogHost;
        this.lifetime = lifetime;
        this.switchToUnity = switchToUnity;
        this.doNotSwitchToUnity = doNotSwitchToUnity;
    }

    public void Show(Action onDialogDispose)
    {
        dialogHost.Show(BuildDialog, lifetime, onDialogDispose);
    }

    private BeDialog BuildDialog(Lifetime dialogLifetime)
    {
        var rememberSettings = new Property<bool>($"{DialogId}::RememberSettings", false);
        var content = BeControls.BeLabel("Switch focus to Unity after sending this command?").InAutoGrid();
        content.AddElement(BeControls.BeLabel(
            "Rider sent a command to the Unity bridge. Switching focus helps you see the selected asset or opened tool window immediately."));
        content.AddElement(new BeSpacer());
        content.AddElement(rememberSettings.GetBeCheckBox(dialogLifetime, "Remember settings"));

        var enabled = new Property<bool>($"{DialogId}::ButtonsEnabled", true);
        return content.InDialog(
                "Switch to Unity",
                DialogId,
                DialogModality.MODAL,
                new BeControlSizeCustom(560, 260),
                DialogLayoutPersistenceMode.SIZE_AND_POSITION,
                BeDialogControlBox.CLOSE,
                BeDialogStyle.THEMED,
                true)
            .WithCustomButton("Switch to Unity", dialogLifetime,
                () => switchToUnity(rememberSettings.Value),
                BeCommonBehavior.DIALOG_ACCEPT_BUTTON,
                enabled,
                DialogResult.OK,
                false,
                () => { })
            .WithCustomButton("Don't switch", dialogLifetime,
                () => doNotSwitchToUnity(rememberSettings.Value),
                BeCommonBehavior.DIALOG_CANCEL_BUTTON,
                enabled,
                DialogResult.CANCEL,
                false,
                () => { });
    }
}
