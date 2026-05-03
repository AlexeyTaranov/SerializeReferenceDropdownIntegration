using JetBrains.DataFlow;
using JetBrains.IDE.UI;
using JetBrains.IDE.UI.Extensions;
using JetBrains.Lifetimes;
using JetBrains.Rider.Model.UIAutomation;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

public sealed class MissingUnityBridgePackageDialog
{
    private const string DialogId = "SerializeReferenceDropdown.MissingUnityBridgePackage";

    private readonly IDialogHost dialogHost;
    private readonly Lifetime lifetime;
    private readonly PluginSessionSettings sessionSettings;

    public MissingUnityBridgePackageDialog(IDialogHost dialogHost, Lifetime lifetime,
        PluginSessionSettings sessionSettings)
    {
        this.dialogHost = dialogHost;
        this.lifetime = lifetime;
        this.sessionSettings = sessionSettings;
    }

    public void Show(string manifestPath)
    {
        dialogHost.Show(dialogLifetime => BuildDialog(dialogLifetime, manifestPath), lifetime, () => { });
    }

    private BeDialog BuildDialog(Lifetime dialogLifetime, string manifestPath)
    {
        var dontShowAgain = new Property<bool>($"{DialogId}::DontShowAgain", false);
        var content = BeControls.BeLabel("Unity integration package is not installed for this project.").InAutoGrid();
        content.AddElement(BeControls.BeLabel(
            $"Install '{UnityBridgePackageManifest.PackageId}' into the Unity project packages."));
        content.AddElement(BeControls.BeLabel($"Checked manifest: {manifestPath}"));
        content.AddElement(new BeSpacer());
        content.AddElement(dontShowAgain.GetBeCheckBox(dialogLifetime, "Don't show this warning again"));

        return content.InDialog(
                $"{Names.SRDShort}: Unity integration missing",
                DialogId,
                DialogModality.MODAL,
                new BeControlSizeCustom(620, 280),
                DialogLayoutPersistenceMode.SIZE_AND_POSITION,
                BeDialogControlBox.CLOSE,
                BeDialogStyle.THEMED,
                true)
            .WithCustomButton("OK", dialogLifetime,
                () =>
                {
                    if (dontShowAgain.Value)
                    {
                        sessionSettings.ShowMissingUnityBridgePackageWarning = false;
                    }
                },
                BeCommonBehavior.DIALOG_ACCEPT_BUTTON,
                new Property<bool>($"{DialogId}::OkEnabled", true),
                DialogResult.OK,
                false,
                () => { });
    }
}
