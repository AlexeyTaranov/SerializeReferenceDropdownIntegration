using System;
using System.Threading.Tasks;
using JetBrains.Collections.Viewable;
using JetBrains.DataFlow;
using JetBrains.IDE.UI.Extensions;
using JetBrains.Lifetimes;
using JetBrains.ReSharper.Feature.Services.Refactorings;
using JetBrains.Rider.Model.UIAutomation;
using JetBrains.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

public enum ModifyYamlShowBehaviour
{
    ShowAlways,
    DontShow
}

public class ModifyUnityAssetRefactoringPage : SingleBeRefactoringPage
{
    private readonly BeGrid myContent;
    private readonly ModifyUnityAssetModel model;
    private readonly IProperty<bool> hidePageOnThisSession;
    private readonly IProperty<bool> applyModifiedFiles;
    private readonly PluginSessionSettings sessionSettings;
    private readonly PluginDiagnostics diagnostics;

    private readonly Action fetchReferencesCount;


    // TODO Replace strings with resourceManager?
    public ModifyUnityAssetRefactoringPage(Lifetime lifetime, ModifyUnityAssetModel model,
        PluginSessionSettings sessionSettings, PluginDiagnostics diagnostics = null) : base(lifetime)
    {
        this.model = model;
        this.sessionSettings = sessionSettings;
        this.diagnostics = diagnostics;
        diagnostics?.Info("Unity YAML refactoring page constructed.");
        myContent = BeControls.GetEmptyGrid(GridOrientation.Vertical, BeMarginType.ReducedIntercellSpacing);

        var filesCountLabel = BeControls.BeLabel("Scan has not run yet.");
        var referencesCount = new Property<int>("References count in assets folder", -1);
        var fetchingFilesCount = new Property<bool>("Fetching files count", false);
        var applyModifiedFilesEnabled = new ViewableProperty<bool>(false);
        referencesCount.PropertyChanged += (sender, args) => filesCountLabel.SetText(GetFilesCountText());
        fetchingFilesCount.PropertyChanged += (sender, args) => filesCountLabel.SetText(GetFilesCountText());

        myContent.AddElement(new BeSpacer());
        var fetchFilesCountButton = BeControls.GetButton("Scan / Rescan", lifetime,
            () => fetchReferencesCount.Invoke());
        fetchReferencesCount = async void () => await FetchCountAsync();
        myContent.AddElement(fetchFilesCountButton);
        myContent.AddElement(filesCountLabel);

        applyModifiedFiles = new Property<bool>("Apply modified Unity asset files",
            model.ShouldApplyModifiedFiles || sessionSettings.DefaultApplyModifiedUnityAssetFiles);

        myContent.AddElement(new BeSpacer());
        var applyModifiedFilesRadioGroup = applyModifiedFiles.GetBeRadioGroup(lifetime,
            "YAML changes",
            new[] { false, true },
            present: (apply, _) => apply ? "Write YAML changes to disk" : "No changes",
            horizontal: false);
        applyModifiedFilesRadioGroup.EnableWhen(lifetime, applyModifiedFilesEnabled);
        myContent.AddElement(applyModifiedFilesRadioGroup);

        myContent.AddElement(new BeSpacer());
        hidePageOnThisSession = new Property<bool>("Hide modify file on this session",
            sessionSettings.ModifyYamlShowBehaviour == ModifyYamlShowBehaviour.DontShow);
        myContent.AddElement(hidePageOnThisSession.GetBeCheckBox(lifetime, "Do not show this step again"));

        string GetFilesCountText()
        {
            if (fetchingFilesCount.Value == true)
            {
                return "Scanning Unity assets for old type references...";
            }

            if (referencesCount.Value < 0)
            {
                return "Scan has not run yet.";
            }

            if (referencesCount.Value == 0)
            {
                return "No Unity YAML files need to be changed.";
            }

            return $"{referencesCount.Value} reference {Pluralize(referencesCount.Value, "update", "updates")} across " +
                   $"{model.PreviewFilesCount} asset {Pluralize(model.PreviewFilesCount, "file", "files")}.";
        }

        static string Pluralize(int count, string singular, string plural)
        {
            return count == 1 ? singular : plural;
        }

        async Task FetchCountAsync()
        {
            fetchingFilesCount.Value = true;
            applyModifiedFilesEnabled.Value = false;
            applyModifiedFiles.Value = false;

            var result = await Task.Run(() =>
                model.FetchSerializeReferenceCountInAssetsFolderAsync(lifetime.ToCancellationToken()));

            referencesCount.Value = result;
            fetchingFilesCount.Value = false;
            applyModifiedFilesEnabled.Value = result > 0;
            diagnostics?.Info($"Unity YAML refactoring page scan completed, referencesCount={result}, previewFiles={model.PreviewFilesCount}.");
            if (result > 0)
            {
                applyModifiedFiles.Value = sessionSettings.DefaultApplyModifiedUnityAssetFiles;
            }
            else
            {
                applyModifiedFiles.Value = false;
            }
        }

        if (sessionSettings.AutoCheckModifiedUnityAssetFiles)
        {
            fetchReferencesCount.Invoke();
        }
        else
        {
            filesCountLabel.SetText(GetFilesCountText());
        }
    }

    public override BeControl GetPageContent() => myContent;

    public override void Commit()
    {
        model.ShouldApplyModifiedFiles = applyModifiedFiles.Value;
        sessionSettings.ModifyYamlShowBehaviour = hidePageOnThisSession.Value
            ? ModifyYamlShowBehaviour.DontShow
            : ModifyYamlShowBehaviour.ShowAlways;
        diagnostics?.Info($"Unity YAML refactoring page committed, shouldApply={model.ShouldApplyModifiedFiles}, hideThisSession={hidePageOnThisSession.Value}.");
    }


    public override bool DoNotShow => sessionSettings.ModifyYamlShowBehaviour == ModifyYamlShowBehaviour.DontShow;

    public override string Title => "Modify Serialize Reference";

    public override string Description => string.Empty;
}
