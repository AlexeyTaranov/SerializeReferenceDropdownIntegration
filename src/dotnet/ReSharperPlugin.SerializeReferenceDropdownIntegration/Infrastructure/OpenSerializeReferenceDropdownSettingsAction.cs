using System;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.Application.UI.Options.OptionsDialog;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

#pragma warning disable 612
[Action("OpenSerializeReferenceDropdownSettingsAction", "Serialize Reference Dropdown: Open Settings")]
#pragma warning restore 612
public class OpenSerializeReferenceDropdownSettingsAction : IExecutableAction
{
    private readonly OptionsManager myOptionsManager;

    public OpenSerializeReferenceDropdownSettingsAction(OptionsManager optionsManager)
    {
        myOptionsManager = optionsManager;
        SerializeReferenceDropdownOptions.Log("Open settings action created.");
    }

    public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
        presentation.Text = "Serialize Reference Dropdown: Open Settings";
        return true;
    }

    public void Execute(IDataContext context, DelegateExecute nextExecute)
    {
        try
        {
            SerializeReferenceDropdownOptions.Log("Open settings action executed. Calling BeginShowOptions.");
            myOptionsManager.BeginShowOptions(SerializeReferenceDropdownOptions.Id);
            SerializeReferenceDropdownOptions.Log("Open settings action BeginShowOptions returned without exception.");
        }
        catch (Exception exception)
        {
            SerializeReferenceDropdownOptions.Log($"Open settings action failed.{Environment.NewLine}{exception}");
            throw;
        }
    }
}
