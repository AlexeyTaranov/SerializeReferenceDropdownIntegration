using System;
using System.Linq;
using JetBrains.IDE.UI;
using JetBrains.IDE.UI.Extensions;
using JetBrains.Lifetimes;
using JetBrains.Rider.Model.UIAutomation;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

public sealed class UnityAssetUsagePreviewDialog
{
    private const string DialogId = "SerializeReferenceDropdown.UnityAssetUsagePreview";
    private const int MaxFilesInDialog = 30;

    private readonly IDialogHost dialogHost;
    private readonly Lifetime lifetime;
    private readonly Action<string> openAsset;

    public UnityAssetUsagePreviewDialog(IDialogHost dialogHost, Lifetime lifetime, Action<string> openAsset)
    {
        this.dialogHost = dialogHost;
        this.lifetime = lifetime;
        this.openAsset = openAsset;
    }

    public void Show(UnityAssetUsagePreview preview)
    {
        dialogHost.Show(dialogLifetime => BuildDialog(dialogLifetime, preview), lifetime, () => { });
    }

    private BeDialog BuildDialog(Lifetime dialogLifetime, UnityAssetUsagePreview preview)
    {
        var content = BuildContent(dialogLifetime, preview)
            .InScrollablePanel(new BeControlSizeCustom(500, 360), BeScrollbarPolicy.VERTICAL);

        return content.InDialog(
            $"{Names.SRDShort}: Unity asset usages",
            DialogId,
            DialogModality.MODAL,
            new BeControlSizeCustom(540, 430),
            DialogLayoutPersistenceMode.SIZE_AND_POSITION,
            BeDialogControlBox.CLOSE,
            BeDialogStyle.THEMED,
            true);
    }

    private BeControl BuildContent(Lifetime dialogLifetime, UnityAssetUsagePreview preview)
    {
        var content = BeControls.BeLabel(preview.Summary).InAutoGrid();

        if (preview.Files.Count == 0)
        {
            content.AddElement(BeControls.BeLabel("Nothing to open in Unity for this type."));
            return content;
        }

        content.AddElement(BeControls.BeLabel("Click an asset to open it in Unity."));
        content.AddElement(new BeSpacer());

        foreach (var file in preview.Files.Take(MaxFilesInDialog))
        {
            var row = BeControls.GetEmptyGrid(GridOrientation.Horizontal, BeMarginType.ReducedIntercellSpacing);
            row.AddElement(BeControls.GetButton(file.AssetPath, dialogLifetime, () => openAsset(file.AssetPath)));
            row.AddElement(BeControls.BeLabel($"{file.ReferencesCount} refs"));
            content.AddElement(row);
        }

        var hiddenFilesCount = Math.Max(preview.Files.Count - MaxFilesInDialog, preview.HiddenFilesCount);
        if (hiddenFilesCount > 0)
        {
            content.AddElement(new BeSpacer());
            content.AddElement(BeControls.BeLabel($"...and {hiddenFilesCount} more files."));
        }

        return content;
    }

}
