using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;
using System;
using System.Diagnostics;

namespace Csharp_3d_viewer
{
	class Program
	{
		static void Main()
		{
			//using (var visualizerData = new VisualizerData())
			//{
				//var renderer = new Renderer(visualizerData);

				//renderer.StartVisualizationThread();

				Debug.WriteLine("start main");
				using (Device device = Device.Open())
				{
					Debug.WriteLine("opened device");
					device.StartCameras(new DeviceConfiguration()
					{
						CameraFPS = FPS.FPS30,
						ColorResolution = ColorResolution.Off,
						DepthMode = DepthMode.NFOV_Unbinned,
						WiredSyncMode = WiredSyncMode.Standalone,
					});

					Debug.WriteLine("started camera");
					var deviceCalibration = device.GetCalibration();

					//small difference with PointCloud enabled
					//pos: head -0.2916188 -178.0469 853.1077
					//pos: head -5.753897 -183.444 856.1947
					PointCloud.ComputePointCloudCache(deviceCalibration);

					using (Tracker tracker = Tracker.Create(deviceCalibration, new TrackerConfiguration() { ProcessingMode = TrackerProcessingMode.Gpu, SensorOrientation = SensorOrientation.Default }))
					{
						Debug.WriteLine("tracker created");
						//while (renderer.IsActive)
						while (true)
						{
							using (Capture sensorCapture = device.GetCapture())
							{
								// Queue latest frame from the sensor.
								tracker.EnqueueCapture(sensorCapture);
							}

							Debug.WriteLine("while");
							using (Frame frame = tracker.PopResult(TimeSpan.FromMilliseconds(500), throwOnTimeout: false))
							{
								Debug.WriteLine("popped result");
								if (frame != null)
								{
									Debug.WriteLine("{0} bodies found.", frame.NumberOfBodies);
									//visualizerData.Frame = frame.Reference();

									if (frame.NumberOfBodies > 0)
									{
										Debug.WriteLine("body id: " + frame.GetBodyId(0));
										Skeleton skeleton = frame.GetBodySkeleton(0);
										Joint head = skeleton.GetJoint(JointId.Head);
										Debug.WriteLine("pos: head " + head.Position.X + " " + head.Position.Y + " " + head.Position.Z);
									}
								}
							}
						}
					}
				}
			//}
		}
	}
}