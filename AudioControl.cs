#if WINDOWS
using NAudio.CoreAudioApi;
#endif

namespace AutoLogout
{
	public class AudioControl
	{
		public readonly System.Timers.Timer timer;

		public AudioControl() {
			timer = new(1000);
			timer.Elapsed += Mute;
		}

		public void Mute(object? sender, EventArgs? e)
		{
			Mute();
		}
		public void Mute()
		{
#if WINDOWS
			// Windows-specific volume control using NAudio
			var deviceEnumerator = new MMDeviceEnumerator();
			foreach(var device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) {
				device.AudioEndpointVolume.Mute = true;
			}
#else
			//TODO: No solution for linux or mac yet
#endif

			if(!timer.Enabled) {
				timer.Start();
			}
		}

		public void Unmute()
		{
#if WINDOWS
			// Windows-specific unmute
			var deviceEnumerator = new MMDeviceEnumerator();
			foreach (var device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
			{
				device.AudioEndpointVolume.Mute = false;
			}
#else
			//TODO: No solution for linux or mac yet
#endif
			timer.Stop();
		}
	}
}