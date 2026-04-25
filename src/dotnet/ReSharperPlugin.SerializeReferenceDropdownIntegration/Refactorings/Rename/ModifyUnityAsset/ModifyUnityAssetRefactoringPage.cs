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
    private readonly IProperty<bool> hidePageOnThisSession;
    private readonly PluginSessionSettings sessionSettings;

    private readonly Action fetchReferencesCount;


    // TODO Replace strings with resourceManager?
    public ModifyUnityAssetRefactoringPage(Lifetime lifetime, ModifyUnityAssetModel model,
        PluginSessionSettings sessionSettings) : base(lifetime)
    {
        this.sessionSettings = sessionSettings;
        //TODO Need implement UNDO
        myContent = BeControls.BeLabel("This feature without UNDO! Keep modifications in VCS!").InAutoGrid();

        myContent.AddElement(new BeSpacer());

        var filesCountLabel = BeControls.BeLabel("?");
        var previewLabel = BeControls.BeLabel("Preview: not loaded");
        var referencesCount = new Property<int>("References count in assets folder", -1);
        var fetchingFilesCount = new Property<bool>("Fetching files count", false);
        referencesCount.PropertyChanged += (sender, args) => filesCountLabel.SetText(GetFilesCountText());
        fetchingFilesCount.PropertyChanged += (sender, args) => filesCountLabel.SetText(GetFilesCountText());
        myContent.AddElement(filesCountLabel);
        myContent.AddElement(previewLabel);

        var fetchFilesCountVisibility = new ViewableProperty<bool>(true);
        var modifyFilesVisibility = new ViewableProperty<bool>(false);
        var fetchFilesCountButton = BeControls.GetButton("Fetch references count in Assets folder", lifetime,
            () => fetchReferencesCount.Invoke());
        
        fetchFilesCountButton.EnableWhen(lifetime, fetchFilesCountVisibility);
        fetchReferencesCount = async void () => await FetchCountAsync();

        myContent.AddElement(fetchFilesCountButton);
        myContent.AddElement(new BeSpacer());

        var modifiedFilesLabel = BeControls.BeLabel("Modified files: PENDING");
        var modifiedFilesLabelVisibility = new ViewableProperty<bool>(false);
        modifiedFilesLabel.EnableWhen(lifetime, modifiedFilesLabelVisibility);
        var modifyAllFilesButton =
            BeControls.GetButton("Modify all files", lifetime, async void () => await ModifyFilesAsync());
        modifyAllFilesButton.EnableWhen(lifetime, modifyFilesVisibility);
        myContent.AddElement(modifyAllFilesButton);
        myContent.AddElement(modifiedFilesLabel);

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
            fetchFilesCountVisibility.Value = false;

            var result = await Task.Run(() =>
                model.FetchSerializeReferenceCountInAssetsFolderAsync(lifetime.ToCancellationToken()));

            referencesCount.Value = result;
            previewLabel.SetText(model.BuildPreviewText());
            fetchingFilesCount.Value = false;
            modifyFilesVisibility.Value = result > 0;
        }

        async Task ModifyFilesAsync()
        {
            ContinueEnabled.Value = false;
            fetchFilesCountVisibility.Value = false;
            modifyFilesVisibility.Value = false;
            modifiedFilesLabelVisibility.Value = true;

            try
            {
                await Task.Run(model.ModifyAllFilesAsync);
            }
            catch (Exception e)
            {
                model.LogModificationFailure(e);
                modifiedFilesLabel.SetText("Modified files: FAILED");
                MessageBox.ShowError("Failed to modify Unity assets. Check the log for details.", Names.SRDShort);
                ContinueEnabled.Value = true;
                return;
            }

            ContinueEnabled.Value = true;
            modifiedFilesLabel.SetText("Modified files: COMPLETE");
        }
    }

    public override BeControl GetPageContent() => myContent;

    public override void Commit()
    {
        sessionSettings.ModifyYamlShowBehaviour = hidePageOnThisSession.Value
            ? ModifyYamlShowBehaviour.DontShow
            : ModifyYamlShowBehaviour.ShowAlways;
    }


    public override bool DoNotShow => sessionSettings.ModifyYamlShowBehaviour == ModifyYamlShowBehaviour.DontShow;

    public override string Title => "Modify YAML content in Assets directory";

    public override string Description => "Modify assets content in Unity Project";
}
