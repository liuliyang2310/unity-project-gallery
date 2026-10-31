using UnityProjectGallery.Core.Mvvm;

namespace UnityProjectGallery.Tests.Mvvm;

public sealed class RelayCommandTests
{
    [Fact]
    public void Execute_InvokesAction()
    {
        var executed = false;
        var command = new RelayCommand(() => executed = true);

        command.Execute(null);

        Assert.True(executed);
    }

    [Fact]
    public void CanExecute_ReturnsFalseWhenPredicateFails()
    {
        var command = new RelayCommand(() => { }, () => false);

        Assert.False(command.CanExecute(null));
    }

    [Fact]
    public void RaiseCanExecuteChanged_RaisesEvent()
    {
        var command = new RelayCommand(() => { });
        var raised = false;
        command.CanExecuteChanged += (_, _) => raised = true;

        command.RaiseCanExecuteChanged();

        Assert.True(raised);
    }

    [Fact]
    public void GenericCommand_PassesParameterToAction()
    {
        string? received = null;
        var command = new RelayCommand<string>(value => received = value);

        command.Execute("demo");

        Assert.Equal("demo", received);
    }
}
