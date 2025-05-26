namespace AutoLogout
{
  public static class Prompt
  {
    public static string? ShowDialog(string text, string caption, bool sensitive = false)
    {
      Form prompt = new()
      {
        AutoScaleDimensions = new SizeF(125F, 125F),
        Width = 600,
        Height = 220,
        FormBorderStyle = FormBorderStyle.FixedDialog,
        Text = caption,
        StartPosition = FormStartPosition.CenterScreen,
        MinimizeBox = false,
        MaximizeBox = false,
        BackColor = Color.White,
      };

      FlowLayoutPanel mainPanel = new()
      {
        Dock = DockStyle.Top,
        FlowDirection = FlowDirection.TopDown,
        Padding = new Padding(12),
      };
      FlowLayoutPanel buttonPanel = new()
      {
        Dock = DockStyle.Bottom,
        FlowDirection = FlowDirection.RightToLeft,
        AutoSize = true,
        BackColor = SystemColors.Control,
        Padding = new Padding(8),
      };


      Label textLabel = new() { MaximumSize=new Size(576, 100), AutoSize=true, Text=text, Padding=new() { Bottom=10 } };
      TextBox textBox = new() { Width=372 };
      if (sensitive) textBox.PasswordChar = '*';

      Button confirmation = new() { Text = "Ok", Width=100, Height=32, DialogResult = DialogResult.OK };
      Button cancel = new() { Text = "Cancel", Width=100, Height=32, DialogResult = DialogResult.Cancel };
      confirmation.Click += (sender, e) => { prompt.Close(); };

      mainPanel.Controls.Add(textLabel);
      mainPanel.Controls.Add(textBox);

      buttonPanel.Controls.Add(cancel);
      buttonPanel.Controls.Add(confirmation);
      
      prompt.Controls.Add(mainPanel);
      prompt.Controls.Add(buttonPanel);

      prompt.AcceptButton = confirmation;

      return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }
  }
}