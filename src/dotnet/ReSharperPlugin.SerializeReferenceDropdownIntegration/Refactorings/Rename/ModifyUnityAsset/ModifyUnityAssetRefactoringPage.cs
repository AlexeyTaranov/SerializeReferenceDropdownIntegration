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
        myContent = BeControls.BeLabel("Unity assets can be updated after this rename.").InAutoGrid();

        myContent.AddElement(new BeSpacer());

        var filesCountLabel = BeControls.BeLabel("?");
        var previewLabel = BeControls.BeLabel("Modified files: not loaded");
        var referencesCount = new Property<int>("References count in assets folder", -1);
        var fetchingFilesCount = new Property<bool>("Fetching files count", false);
        var applyModifiedFilesEnabled = new ViewableProperty<bool>(false);
        referencesCount.PropertyChanged += (sender, args) => filesCountLabel.SetText(GetFilesCountText());
        fetchingFilesCount.PropertyChanged += (sender, args) => filesCountLabel.SetText(GetFilesCountText());
        myContent.AddElement(filesCountLabel);
        myContent.AddElement(previewLabel);

        var fetchFilesCountButton = BeControls.GetButton("Show modified files", lifetime,
            () => fetchReferencesCount.Invoke());
        fetchReferencesCount = async void () => await FetchCountAsync();

        myContent.AddElement(fetchFilesCountButton);
        myContent.AddElement(new BeSpacer());

        applyModifiedFiles = new Property<bool>("Apply modified Unity asset files", model.ShouldApplyModifiedFiles);
        var applyModifiedFilesCheckBox = applyModifiedFiles.GetBeCheckBox(lifetime, "Apply modified files");
        applyModifiedFilesCheckBox.EnableWhen(lifetime, applyModifiedFilesEnabled);
        myContent.AddElement(applyModifiedFilesCheckBox);

        myContent.AddElement(new BeSpacer());
        
        
        hidePageOnThisSession = new Property<bool>("Hide modify file on this session",
            sessionSettings.ModifyYamlShowBehaviour == ModifyYamlShowBehaviour.DontShow);
        myContent.AddElement(hidePageOnThisSession.GetBeCheckBox(lifetime, "Hide this window"));

        string GetFilesCountText()
        {
            var mainText = "Reference count in assets folder: ";
            if (fetchingFilesCount.Value == true)
            {
                return mainText + "PENDING";
            }

            if (referencesCount.Value < 0)
            {
                return mainText + "NEED COUNT";
            }

            return $"{mainText}{referencesCount.Value} changes in {model.PreviewFilesCount} files";
        }

        async Task FetchCountAsync()
        {
            fetchingFilesCount.Value = true;

            var result = await Task.Run(() =>
                model.FetchSerializeReferenceCountInAssetsFolderAsync(lifetime.ToCancellationToken()));

            referencesCount.Value = result;
            previewLabel.SetText(model.BuildPreviewText());
            fetchingFilesCount.Value = false;
            applyModifiedFilesEnabled.Value = result > 0;
            if (result <= 0)
            {
                applyModifiedFiles.Value = false;
            }
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

    public override string Title => "Modify YAML content in Assets directory";

    public override string Description => "Modify assets content in Unity Project";
}
