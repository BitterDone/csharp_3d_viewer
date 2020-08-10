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

namespace Csharp_3d_viewer
{
	class Program
	{
		static IPHostEntry ipHost;
		static IPAddress ipAddr;
		static IPEndPoint localEndPoint;
		static Socket sender;

		static void Main()
		{
			azureKinect();
			//socket();
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

		public static void azureKinect()
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
						//Debug.WriteLine("test0");
						using (Capture sensorCapture = device.GetCapture())
						{
							// Queue latest frame from the sensor. thros System.FieldAccessException
							tracker.EnqueueCapture(sensorCapture);
						}
						Debug.WriteLine("test1");

						ConsoleKey key;
						string fileOfSkeletons = "";
						//int x = 0;
						while (!Console.KeyAvailable)
						{
							string stringifiedSkeleton = "";

							//using (Frame frame = tracker.PopResult(TimeSpan.FromMilliseconds(500), throwOnTimeout: false))
							using (Frame frame = tracker.PopResult())
							{
								if (frame != null)
								{
									Debug.WriteLine("{0} bodies found.", frame.NumberOfBodies);
									//visualizerData.Frame = frame.Reference();

									if (frame.NumberOfBodies > 0)
									{
										Debug.WriteLine("body id: " + frame.GetBodyId(0));
										Skeleton skeleton = frame.GetBodySkeleton(0);
										//Joint head = skeleton.GetJoint(JointId.Head);
										//string msg = "pos: head " + head.Position.X + " " + head.Position.Y + " " + head.Position.Z + " " + head.Quaternion.W + " " + head.Quaternion.X + " " + head.Quaternion.Y +  " " + head.Quaternion.Z;

										//string stringifiedSkeleton = "";

										for (var i = 0; i < (int)JointId.Count; i++)
										{
											Joint joint = skeleton.GetJoint(i);
											float posX = joint.Position.X;
											float posY = joint.Position.Y;
											float posZ = joint.Position.Z;

											//float quatW = joint.Quaternion.W;
											//float quatX = joint.Quaternion.X;
											//float quatY = joint.Quaternion.Y;
											//float quatZ = joint.Quaternion.Z;

											//JointId jointName = (JointId)i;
											//string stringifiedJoint = String.Format("{0}#{1}#{2}#{3}#{4}#{5}{6}#{7}", jointName, posX, posY, posZ, quatW, quatX, quatY, quatZ); // 8 components -000 = 32 + 7 = 39 // for kafka
											string stringifiedJoint = String.Format("{0},{1},{2},", posX, posY, posZ); // for training data
											stringifiedSkeleton = String.Format("{0},{1},", stringifiedSkeleton, stringifiedJoint); // 32*7=224 32*8=256 components, 224*33=7392 256*39=9984
										}

										//stringifiedSkeleton = String.Format("{0}!{1}", DateTime.UtcNow.ToString(), stringifiedSkeleton); // x5/x6/2005 09:34:42 PM = 22 char + 2, 7418 vs 10,008 chars passed for every skeleton
										Debug.WriteLine(stringifiedSkeleton);
										fileOfSkeletons = fileOfSkeletons + stringifiedSkeleton + "\n";
										//produce(stringifiedSkeleton);
									}
									else
									{
										Debug.WriteLine("no bodies");
									}
								}
								else
								{
									Debug.WriteLine("frame was null");
								}
							}

							key = Console.ReadKey(true).Key;

							if (key == ConsoleKey.Enter)
							{
								string formattedDateTime = DateTime.Now.ToString("yyyyMMdd_HH.mm.ss"); // -tt
								writeToFile(formattedDateTime, fileOfSkeletons);
								fileOfSkeletons = "";
							}
							else
							{
								Debug.WriteLine("Key not recognized");
							}
						}
					
					}
				}
			}
		}

		static String formatCoordsFromSkeleton(Skeleton s)
		{
			String stringifiedSkeleton = "";

			for (var i = 0; i < (int)JointId.Count; i++)
			{
				Joint joint = s.GetJoint(i);
				float posX = joint.Position.X;
				float posY = joint.Position.Y;
				float posZ = joint.Position.Z;


				string stringifiedJoint = String.Format("{0},{1},{2},", posX, posY, posZ); // for training data
				stringifiedSkeleton = String.Format("{0},{1},", stringifiedSkeleton, stringifiedJoint); // 32*7=224 32*8=256 components, 224*33=7392 256*39=9984
			}

			return stringifiedSkeleton;
		}

		public static void writeToFile(string filename, string skeleton)
		{
			File.AppendAllText(@"D:\path\" + filename + ".txt", skeleton + Environment.NewLine);
		}

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
	}
}