namespace PicklePorts;

public partial class NewPage1 : ContentPage
{
    public NewPage1()
    {
        InitializeComponent();
    }
    public async void GoBack(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new MainPage());
    }
}