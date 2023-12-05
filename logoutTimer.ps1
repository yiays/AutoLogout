[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.VisualBasic')
Add-Type -AssemblyName System.Windows.Forms

# Set the timeout in minutes
$input = [Microsoft.VisualBasic.Interaction]::InputBox("Please specify allowed time in hours.", "Logout timer")
$input = [double]::Parse($input)
if (($input -isnot [double])) {
	Start-Job -ScriptBlock {
		Add-Type -AssemblyName System.Windows.Forms

		# Display a custom message box and start the countdown
		$message = "You did not provide a number as input. Assuming 2 hours."
		$caption = "Log Out Timer"
		$icon = [System.Windows.Forms.MessageBoxIcon]::Warning
		[System.Windows.Forms.MessageBox]::Show($message, $caption, [System.Windows.Forms.MessageBoxButtons]::OK, $icon)
	}
	$input = 2
}

$timeoutMinutes = $input * 60
$timeoutSeconds = $timeoutMinutes * 60

# Create and show the timer window
Start-Process RunHidden.exe "timerWindow.ps1 $($timeoutSeconds - 2)"

if ($timeoutSeconds -gt 600) {
	Start-Sleep -Seconds $($timeoutSeconds - 600)
}

# Display the message box
Start-Job -ScriptBlock {
    Add-Type -AssemblyName System.Windows.Forms
	
	$soundPlayer = New-Object System.Media.SoundPlayer
    $soundPlayer.SoundLocation = "alarm.wav"
    $soundPlayer.Play()

    # Display a custom message box and start the countdown
    $message = "You will be logged out in 10 minutes or less. Please save your work."
    $caption = "Log Out Warning"
    $icon = [System.Windows.Forms.MessageBoxIcon]::Warning
    [System.Windows.Forms.MessageBox]::Show($message, $caption, [System.Windows.Forms.MessageBoxButtons]::OK, $icon)
}

if ($timeoutSeconds -gt 600) {
	Start-Sleep -Seconds 600
} else {
	Start-Sleep -Seconds $timeoutSeconds
}

# Log out the user
shutdown.exe /l
#[System.Windows.Forms.MessageBox]::Show(":3")
