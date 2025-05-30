namespace AutoLogout
{
  public static class Prompt
  {
    private partial class PromptForm : Form
    {
      public TextBox textBox;
      public PromptForm(string text, string caption, bool sensitive = false)
      {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Text = caption;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = Color.White;
        Width = 350;
        Height = 200;

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new(96F, 96F);

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


        Label textLabel = new() { MaximumSize = new Size(576, 100), AutoSize = true, Text = text, Padding = new() { Bottom = 10 } };
        textBox = new() { Width = 300 };
        if (sensitive) textBox.PasswordChar = '*';

        Button confirmation = new() { Text = "Ok", Width = 100, Height = 32, DialogResult = DialogResult.OK };
        Button cancel = new() { Text = "Cancel", Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        confirmation.Click += (sender, e) => { Close(); };

        mainPanel.Controls.Add(textLabel);
        mainPanel.Controls.Add(textBox);

        buttonPanel.Controls.Add(cancel);
        buttonPanel.Controls.Add(confirmation);

        Controls.Add(mainPanel);
        Controls.Add(buttonPanel);

        AcceptButton = confirmation;
      }
    }

    public static string? ShowDialog(string text, string caption, bool sensitive = false)
    {
      PromptForm prompt = new(text, caption, sensitive);

      return prompt.ShowDialog() == DialogResult.OK ? prompt.textBox.Text : null;
    }
  }
}