using Avalonia.Controls;
using Avalonia.Media;

namespace AutoLogout;

public partial class UpdateNotice : UserControl
{
    public bool UpdateAvailable { get => State.Current.Update > UpdateUrgency.None; }
    public string UpdateUrl { get => State.Current.UpdateUrl; }

    public UpdateNotice()
    {
        InitializeComponent();
        DataContext = this;

        State.Current.UpdateAvailable += Update;
        Update();
    }

    public void Update()
    {
        if (UpdateAvailable)
        {
            Notice.IsVisible = true;
            if(State.Current.Update == UpdateUrgency.Feature)
                UpdateNoticeText.IsVisible = true;
            else
            {
                UrgentUpdateNoticeText.IsVisible = true;
                Notice.Background = SolidColorBrush.Parse("#df4d28");
            }
            InvalidateVisual();
        }
    }
}