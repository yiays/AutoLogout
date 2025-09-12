using NAudio.CoreAudioApi;

namespace AutoLogout
{
	public class AudioControl
	{
		private readonly MMDeviceEnumerator deviceEnumerator;
		private MMDevice? defaultDevice;
		private bool? previousState = null;
		public readonly System.Windows.Forms.Timer timer;

		public AudioControl() {
			// Initialize the CoreAudio components
			deviceEnumerator = new MMDeviceEnumerator();
			timer = new System.Windows.Forms.Timer
			{
				Interval = 1000
			};
			timer.Tick += Mute;
		}

		public void Mute(object? sender, EventArgs? e)
		{
			Mute();
		}
		public void Mute()
		{
			try
			{
				defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
			}
			catch
			{
				Console.WriteLine("Failed to get default audio output device.");
				return;
			}
			if(previousState == null) {
				defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
				previousState = defaultDevice.AudioEndpointVolume.Mute;
			}
			foreach(var device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) {
				device.AudioEndpointVolume.Mute = true;
			}

			if(!timer.Enabled) {
				timer.Start();
			}
		}

		public void Unmute()
		{
			if (previousState == false && defaultDevice is not null)
			{
				foreach (var device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
				{
					device.AudioEndpointVolume.Mute = false;
				}
			}
			previousState = null;
			timer.Stop();
		}
	}
}