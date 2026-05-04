using System;
using System.Linq;
using JetBrains.IDE.UI;
using JetBrains.IDE.UI.Extensions;
using JetBrains.Lifetimes;
using JetBrains.ReSharper.Daemon.CodeInsights;
using JetBrains.Rider.Model.UIAutomation;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

public sealed class UnityAssetUsagePreviewDialog
{
    private const string DialogId = "SerializeReferenceDropdown.UnityAssetUsagePreview";
    private const int MaxFilesInDialog = 30;
    private static readonly BeControlSizeCustom ContentSize = new(360, 170);
    private static readonly BeControlSizeCustom DialogSize = new(380, 205);

    private readonly IDialogHost dialogHost;
    private readonly PluginDiagnostics diagnostics;
    private readonly Lifetime lifetime;
    private readonly Action<string> openAsset;

    public UnityAssetUsagePreviewDialog(IDialogHost dialogHost, Lifetime lifetime,
        PluginDiagnostics diagnostics, Action<string> openAsset)
    {
        this.dialogHost = dialogHost;
        this.diagnostics = diagnostics;
        this.lifetime = lifetime;
        this.openAsset = openAsset;
    }

    public void Show(UnityAssetUsagePreview preview, CodeInsightsClickInfo clickInfo)
    {
        ShowDialog(preview);
    }

    private void ShowDialog(UnityAssetUsagePreview preview)
    {
        dialogHost.Show(dialogLifetime => BuildDialog(dialogLifetime, preview), lifetime, () => { });
    }

    private BeDialog BuildDialog(Lifetime dialogLifetime, UnityAssetUsagePreview preview)
    {
        var content = BuildContent(dialogLifetime, preview)
            .InScrollablePanel(ContentSize, BeScrollbarPolicy.VERTICAL);

        return content.InDialog(
            $"{Names.SRDShort}: Unity asset usages",
            DialogId,
            DialogModality.MODAL,
            DialogSize,
            DialogLayoutPersistenceMode.SIZE_AND_POSITION,
            BeDialogControlBox.CLOSE,
            BeDialogStyle.THEMED,
            true);
    }

    private BeControl BuildContent(Lifetime dialogLifetime, UnityAssetUsagePreview preview)
    {
        var content = BeControls.GetEmptyGrid(GridOrientation.Vertical, BeMarginType.ReducedIntercellSpacing);

        if (preview.Files.Count == 0)
        {
            content.AddElement(BeControls.BeLabel("No Unity asset rows are available for this type."));
            return content;
        }

        foreach (var file in preview.Files.Take(MaxFilesInDialog))
        {
            content.AddElement(BuildFileRow(dialogLifetime, file), BeSizingType.Fit, BeAlignment.Left);
        }

        var hiddenFilesCount = Math.Max(preview.Files.Count - MaxFilesInDialog, preview.HiddenFilesCount);
        if (hiddenFilesCount > 0)
        {
            content.AddElement(new BeSpacer());
            content.AddElement(BeControls.BeLabel($"...and {hiddenFilesCount} more files. Refresh or narrow the target type if the list is too broad."));
        }

        return content;
    }

    private BeControl BuildFileRow(Lifetime dialogLifetime, UnityAssetUsagePreviewFile file)
    {
        var icon = UnityAssetUsageIconProvider.GetIcon(file.Kind);
        var text = BeControls.GetRichText(BuildAssetRowText(file), null, null, icon, false);
        text.HorizontalTextPosition.Value = BeTextHorizontalPosition.RIGHT;
        return BeControls.GetButton(text, dialogLifetime, () =>
        {
            diagnostics.Info($"Unity asset usage row clicked: {file.AssetPath}");
            openAsset(file.AssetPath);
        }, BeButtonStyle.DEFAULT);
    }

    private static string BuildAssetRowText(UnityAssetUsagePreviewFile file)
    {
        var directory = string.IsNullOrWhiteSpace(file.AssetDirectory)
            ? string.Empty
            : $"    {file.AssetDirectory}";
        return $"{file.AssetName}{directory}";
    }
}
