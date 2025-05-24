namespace AutoLogout
{
  public static class Prompt
  {
    public static string? ShowDialog(string text, string caption)
    {
      Form prompt = new()
      {
        AutoScaleDimensions = new SizeF(125F, 125F),
        Width = 424,
        Height = 180,
        FormBorderStyle = FormBorderStyle.FixedToolWindow,
        Text = caption,
        StartPosition = FormStartPosition.CenterScreen,
        MaximizeBox = false,
      };
      Label textLabel = new() { Top=12, Left=12, Width=400, Text=text };
      TextBox textBox = new() { Top=42, Left=12, Width=372, PasswordChar = '*' };
      Button confirmation = new() { Text = "Ok", Top=84, Left=176, Width=100, Height=32, DialogResult = DialogResult.OK };
      Button cancel = new() { Text = "Cancel", Top=84, Left=288, Width=100, Height=32, DialogResult = DialogResult.Cancel };
      confirmation.Click += (sender, e) => { prompt.Close(); };
      prompt.Controls.Add(textBox);
      prompt.Controls.Add(confirmation);
      prompt.Controls.Add(cancel);
      prompt.Controls.Add(textLabel);
      prompt.AcceptButton = confirmation;

      return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }
  }
}