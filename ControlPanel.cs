
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
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
      base.OnFormClosed(e);
      parent.controlPanel = null;
    }
  }
}