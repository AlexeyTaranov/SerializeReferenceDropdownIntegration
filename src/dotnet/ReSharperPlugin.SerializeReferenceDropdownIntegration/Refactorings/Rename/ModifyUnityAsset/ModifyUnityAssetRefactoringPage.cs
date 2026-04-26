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

    private readonly Action fetchReferencesCount;


    // TODO Replace strings with resourceManager?
    public ModifyUnityAssetRefactoringPage(Lifetime lifetime, ModifyUnityAssetModel model,
        PluginSessionSettings sessionSettings) : base(lifetime)
    {
        this.model = model;
        this.sessionSettings = sessionSettings;
        myContent = BeControls.BeLabel("Rider can update Unity YAML assets that still point to the old SerializeReference type.").InAutoGrid();
        myContent.AddElement(BeControls.BeLabel("Scan the project, review affected files, then choose whether to write YAML changes to disk."));

        AddSection("Scan result", "Only Unity asset files containing the renamed managed-reference type are counted.");
        var filesCountLabel = BeControls.BeLabel("Scan has not run yet.");
        var previewLabel = BeControls.BeLabel("Affected files will appear here after scanning.");
        var referencesCount = new Property<int>("References count in assets folder", -1);
        var fetchingFilesCount = new Property<bool>("Fetching files count", false);
        var applyModifiedFilesEnabled = new ViewableProperty<bool>(false);
        referencesCount.PropertyChanged += (sender, args) => filesCountLabel.SetText(GetFilesCountText());
        fetchingFilesCount.PropertyChanged += (sender, args) => filesCountLabel.SetText(GetFilesCountText());
        myContent.AddElement(filesCountLabel);

        var fetchFilesCountButton = BeControls.GetButton("Scan Unity assets again", lifetime,
            () => fetchReferencesCount.Invoke());
        fetchReferencesCount = async void () => await FetchCountAsync();

        myContent.AddElement(fetchFilesCountButton);

        AddSection("Affected files", "This list is intentionally short; use VCS diff for exact YAML changes.");
        myContent.AddElement(previewLabel);

        applyModifiedFiles = new Property<bool>("Apply modified Unity asset files",
            model.ShouldApplyModifiedFiles || sessionSettings.DefaultApplyModifiedUnityAssetFiles);

        AddSection("Apply changes", "Keep this disabled if you only want to rename code and inspect assets manually.");
        var applyModifiedFilesCheckBox = applyModifiedFiles.GetBeCheckBox(lifetime, "Apply these Unity asset file changes");
        applyModifiedFilesCheckBox.EnableWhen(lifetime, applyModifiedFilesEnabled);
        myContent.AddElement(applyModifiedFilesCheckBox);
        if (sessionSettings.ShowApplyModifiedUnityAssetFilesWarning)
        {
            myContent.AddElement(BeControls.BeLabel("This writes YAML assets to disk. Review the resulting diff before committing."));
        }

        AddSection("Preference");
        hidePageOnThisSession = new Property<bool>("Hide modify file on this session",
            sessionSettings.ModifyYamlShowBehaviour == ModifyYamlShowBehaviour.DontShow);
        myContent.AddElement(hidePageOnThisSession.GetBeCheckBox(lifetime, "Do not show this step again"));

        void AddSection(string title, string description = null)
        {
            myContent.AddElement(new BeSpacer());
            myContent.AddElement(BeControls.BeLabel(title));
            if (!string.IsNullOrEmpty(description))
            {
                myContent.AddElement(BeControls.BeLabel(description));
            }
        }

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
            previewLabel.SetText("Scanning files...");

            var result = await Task.Run(() =>
                model.FetchSerializeReferenceCountInAssetsFolderAsync(lifetime.ToCancellationToken()));

            referencesCount.Value = result;
            previewLabel.SetText(model.BuildPreviewText());
            fetchingFilesCount.Value = false;
            applyModifiedFilesEnabled.Value = result > 0;
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
            previewLabel.SetText(model.BuildPreviewText());
        }
    }

    public override BeControl GetPageContent() => myContent;

    public override void Commit()
    {
        model.ShouldApplyModifiedFiles = applyModifiedFiles.Value;
        sessionSettings.ModifyYamlShowBehaviour = hidePageOnThisSession.Value
            ? ModifyYamlShowBehaviour.DontShow
            : ModifyYamlShowBehaviour.ShowAlways;
    }


    public override bool DoNotShow => sessionSettings.ModifyYamlShowBehaviour == ModifyYamlShowBehaviour.DontShow;

    public override string Title => "Update Unity YAML assets";

    public override string Description => "Review SerializeReference asset changes before applying rename";
}
