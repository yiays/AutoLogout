
using System.Runtime.InteropServices.Swift;

namespace AutoLogout
{
  public partial class ControlPanel : Form
  {
    private readonly CountdownTimer parent;
    public ControlPanel(CountdownTimer parent)
    {
      this.parent = parent;
      Text = "AutoLogout Settings";
      FormBorderStyle = FormBorderStyle.FixedToolWindow;
      ShowInTaskbar = true;
      StartPosition = FormStartPosition.CenterScreen;
      AutoScaleMode = AutoScaleMode.Dpi;
      AutoScaleDimensions = new SizeF(125F, 125F);
      MaximizeBox = false;
      Width = 1200;
      Height = 800;

      
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      e.Cancel = true;  // Prevents window from closing
      Hide();  // Hides the form instead of closing it
    }
  }
}