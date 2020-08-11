using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;
using System;
using System.Diagnostics;
using Confluent.Kafka;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Collections.Generic;

namespace Csharp_3d_viewer
{
	class Program
	{

		static void Main()
		{
			Debug.WriteLine("start main");

			Device device = null;
			Calibration deviceCalibration = new Calibration();

			try {
				device = Device.Open();

				device.StartCameras(new DeviceConfiguration()
				{
					CameraFPS = FPS.FPS30,
					ColorResolution = ColorResolution.Off,
					DepthMode = DepthMode.NFOV_Unbinned,
					WiredSyncMode = WiredSyncMode.Standalone,
				});

				deviceCalibration = device.GetCalibration();
				PointCloud.ComputePointCloudCache(deviceCalibration);
				//small difference with PointCloud enabled
				//pos: head -0.2916188 -178.0469 853.1077
				//pos: head -5.753897 -183.444 856.1947

				Debug.WriteLine("started camera");
			} catch (Exception e) {
				Debug.Write("exception starting camera: " + e.ToString());
				return;
			}

			Tracker tracker = null;

			try {
				TrackerConfiguration trackerConfiguration = new TrackerConfiguration() {
					ProcessingMode = TrackerProcessingMode.Gpu,
					SensorOrientation = SensorOrientation.Default
				};
				tracker = Tracker.Create(deviceCalibration, trackerConfiguration);
				Debug.WriteLine("tracker created");
			}
			catch (Exception e) {
				Debug.Write("exception starting camera: " + e.ToString());
				return;
			}

			List<string> listOfSkeletons = new List<string>();
			while (true) {
				while (!Console.KeyAvailable) {
					using (Capture sensorCapture = device.GetCapture()) { tracker.EnqueueCapture(sensorCapture); } // Queue latest frame from the sensor. thros System.FieldAccessException

					Frame frame;

					try {
						frame = tracker.PopResult(); // (TimeSpan.FromMilliseconds(500), throwOnTimeout: false))
						if (frame == null) {
							Debug.WriteLine("frame was null"); 
							continue;
						}

						uint numBodies = frame.NumberOfBodies;
						Debug.WriteLine($"{numBodies} bodies found.");
						if (numBodies < 1) { continue; }

						Debug.WriteLine("body id: " + frame.GetBodyId(0));
						Skeleton skeleton = frame.GetBodySkeleton(0);

						string oneSetOfSkeletonJoints = formatCoordsFromSkeleton(skeleton);

						Debug.WriteLine($"{oneSetOfSkeletonJoints}");
						listOfSkeletons.Add(oneSetOfSkeletonJoints);
					}
					catch (Exception e) {
						Debug.Write("exception with frame data: " + e.ToString());
					}
				}

				switch (Console.ReadKey(true).Key) {
					case ConsoleKey.Enter:
						Debug.WriteLine("Enter pressed, writing skeleton to file");
						formatAndWriteSkeletons(listOfSkeletons);
						break;
					default:
						Debug.WriteLine("Key not recognized");
						break;
				}

			}
		}

		static String formatCoordsFromSkeleton(Skeleton s)
		{
			string[] joints = new string[(int)JointId.Count];

			for (var i = 0; i < (int)JointId.Count; i++)
			{
				JointId jointName = (JointId)i;
				Joint joint = s.GetJoint(i);
				float posX = joint.Position.X;
				float posY = joint.Position.Y;
				float posZ = joint.Position.Z;

				// https://stackoverflow.com/questions/202813/adding-values-to-a-c-sharp-array
				joints[i] = String.Format("Joint,{0},{1},{2},{3}", jointName, posX, posY, posZ);
			}

			return string.Join(",", joints);
		}

		public static void formatAndWriteSkeletons(List<string> skeletons) {
			string fileLines = string.Join("\n", skeletons.ToArray());
			// skeletons.ForEach(skeletonString => {});
			Debug.WriteLine(fileLines);
			writeToFile(DateTime.Now.ToString("yyyyMMdd_HH.mm.ss"), fileLines);
		}
		public static void writeToFile(string filename, string fileLine)
		{	
			File.AppendAllText(@"D:\path\" + filename + ".txt", fileLine + Environment.NewLine);
		}

		static IPHostEntry ipHost;
		static IPAddress ipAddr;
		static IPEndPoint localEndPoint;
		static Socket sender;

		public static async Task produce(string message)
		{
			Console.WriteLine("produce ");
			var config = new ProducerConfig { BootstrapServers = "localhost:9092" };

			// If serializers are not specified, default serializers from
			// `Confluent.Kafka.Serializers` will be automatically used where
			// available. Note: by default strings are encoded as UTF8.
			using (var p = new ProducerBuilder<Null, string>(config).Build())
			{
				Console.WriteLine("using");
				try
				{
					Console.WriteLine("try");
					var dr = await p.ProduceAsync("testTopicName", new Message<Null, string> { Value = message }).ConfigureAwait(false);
					Console.WriteLine($"Delivered '{dr.Value}' to '{dr.TopicPartitionOffset}'");
				}
				catch (ProduceException<Null, string> e)
				{
					Console.WriteLine("catch");
					Console.WriteLine($"Delivery failed: {e.Error.Reason}");
				}
				Console.WriteLine("did try");
			}
		}

		public static void socket()
		{

			try
			{
				//IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
				//IPAddress ipAddr = ipHost.AddressList[0];
				//IPEndPoint localEndPoint = new IPEndPoint(ipAddr, 11111);

				//Socket sender = new Socket(ipAddr.AddressFamily,
				//		SocketType.Stream, ProtocolType.Tcp);
				initSocket();

				string msg = "Test Client<EOF>";
				//sendSocket(localEndPoint, sender, msg);
				sendSocket(msg);

				//closeSocket(sender);
				closeSocket();
			}

			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}

		public static void initSocket()
		{
			ipHost = Dns.GetHostEntry(Dns.GetHostName());
			ipAddr = ipHost.AddressList[0];
			localEndPoint = new IPEndPoint(ipAddr, 11111);

			sender = new Socket(ipAddr.AddressFamily,
					SocketType.Stream, ProtocolType.Tcp);
		}

		//public static void sendSocket(IPEndPoint localEndPoint, Socket sender, string msg)
		public static void sendSocket(string msg)
		{
			try
			{
				sender.Connect(localEndPoint);

				Console.WriteLine("Socket connected to -> {0} ",
							sender.RemoteEndPoint.ToString());

				byte[] messageSent = Encoding.ASCII.GetBytes(msg);
				int byteSent = sender.Send(messageSent);

				// Data buffer 
				byte[] messageReceived = new byte[1024];

				int byteRecv = sender.Receive(messageReceived);
				Console.WriteLine("Message from Server -> {0}",
					Encoding.ASCII.GetString(messageReceived,
												0, byteRecv));
			}

			catch (ArgumentNullException ane)
			{
				Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
			}

			catch (SocketException se)
			{
				Console.WriteLine("SocketException : {0}", se.ToString());
			}

			catch (Exception e)
			{
				Console.WriteLine("Unexpected exception : {0}", e.ToString());
			}
		}

		//public static void closeSocket(Socket sender)
		public static void closeSocket()
		{
			sender.Shutdown(SocketShutdown.Both);
			sender.Close();
		}

	}
}