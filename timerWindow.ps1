param (
	[int]$localSeconds
)

Add-Type -AssemblyName System.Windows.Forms

$form = New-Object System.Windows.Forms.Form
$form.Text = "Logout Timer"
$form.Size = New-Object System.Drawing.Size(128, 80)
$form.StartPosition = "Manual"

$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedToolWindow
$form.ShowInTaskbar = $false
$form.ControlBox = $false

# Position window on the bottom right
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$locationX = $screen.Right - $form.Width - 10
$locationY = $screen.Bottom - $form.Height - 48
$form.Location = New-Object System.Drawing.Point($locationX, $locationY)

function TimeString {
	param([int]$seconds)
	$timeSpan = New-Object TimeSpan 0,0,$seconds
	$formattedTime = '{0:D2}:{1:D2}:{2:D2}' -f $timeSpan.Hours, $timeSpan.Minutes, $timeSpan.Seconds
	return $formattedTime
}

$label = New-Object System.Windows.Forms.Label
$label.Location = New-Object System.Drawing.Point(10, 10)
$label.Size = New-Object System.Drawing.Size(108, 40)
$label.Font = New-Object System.Drawing.Font("Arial", 16, [System.Drawing.FontStyle]::Bold)
$label.Text = $(TimeString($localSeconds))
$form.Controls.Add($label)

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 1000  # 1 second interval
$timerScript = {
	$remainingSeconds = $form.Tag
	$remainingSeconds--
	$form.Tag = $remainingSeconds
	$label.Text = $(TimeString($remainingSeconds))

	if ($remainingSeconds -le 0) {
		echo sd
		$timer.Stop()
		$form.Close()
	}
}
# Use Add_Shown event to start the timer after the form is shown
$form.Add_Shown({
	$form.Tag = $localSeconds
	$timer.Add_Tick($timerScript)
	$timer.Start()
})

$form.ShowDialog()