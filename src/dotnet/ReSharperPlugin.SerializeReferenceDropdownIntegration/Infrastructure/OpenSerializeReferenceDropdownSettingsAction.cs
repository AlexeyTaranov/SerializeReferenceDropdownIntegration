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
            myOptionsManager.BeginShowOptions(SerializeReferenceDropdownOptions.Id);
        }
        catch (Exception exception)
        {
            SerializeReferenceDropdownOptions.Log($"Open settings action failed.{Environment.NewLine}{exception}");
            throw;
        }
    }
}
