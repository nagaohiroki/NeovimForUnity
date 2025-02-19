using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace NeovimEditor
{
	[InitializeOnLoad]
	public class NeovimIntegration
	{
		class Client
		{
			public IPEndPoint EndPoint { get; set; }
			public double LastMessage { get; set; }
		}
		static Messager messager;
		static readonly Queue<Message> incoming = new();
		static readonly Dictionary<IPEndPoint, Client> clients = new();
		static readonly object incomingLock = new();
		static readonly object clientsLock = new();
		static NeovimIntegration()
		{
			if(!NeovimEditor.IsEnabled)
			{
				return;
			}
			RunOnceOnUpdate(() =>
			{
				var messagingPort = MessagingPort();
				try
				{
					messager = Messager.BindTo(messagingPort);
					messager.ReceiveMessage += ReceiveMessage;
				}
				catch(SocketException)
				{
					Debug.LogWarning($"Unable to use UDP port {messagingPort} for VS/Unity messaging. You should check if another process is already bound to this port or if your firewall settings are compatible.");
				}
				RunOnShutdown(Shutdown);
			});
			EditorApplication.update += OnUpdate;
		}
		static void RunOnceOnUpdate(Action action)
		{
			var callback = null as EditorApplication.CallbackFunction;
			callback = () =>
			{
				EditorApplication.update -= callback;
				action();
			};
			EditorApplication.update += callback;
		}
		static int DebuggingPort()
		{
			return 56000 + (System.Diagnostics.Process.GetCurrentProcess().Id % 1000);
		}
		static int MessagingPort()
		{
			return DebuggingPort() + 2;
		}
		static void ReceiveMessage(object sender, MessageEventArgs args)
		{
			OnMessage(args.Message);
		}
		static void OnMessage(Message message)
		{
			AddMessage(message);
		}
		static void AddMessage(Message message)
		{
			lock(incomingLock)
			{
				incoming.Enqueue(message);
			}
		}
		static void RunOnShutdown(Action action)
		{
#if UNITY_EDITOR_WIN
			AppDomain.CurrentDomain.DomainUnload += (_, __) => action();
#endif
		}
		static void Shutdown()
		{
			if(messager == null)
			{
				return;
			}
			messager.ReceiveMessage -= ReceiveMessage;
			messager.Dispose();
			messager = null;
		}
		/*
		static void BroadcastMessage(MessageType type, string value)
		{
			lock (clientsLock)
			{
				foreach (var client in clients.Values.ToArray())
				{
					Answer(client, type, value);
				}
			}
		}
		static void Answer(Client client, MessageType answerType, string answerValue)
		{
			Answer(client.EndPoint, answerType, answerValue);
		}
		*/
		static void Answer(Message message, MessageType answerType, string answerValue = "")
		{
			var targetEndPoint = message.Origin;
			Answer(targetEndPoint, answerType, answerValue);
		}
		static void Answer(IPEndPoint targetEndPoint, MessageType answerType, string answerValue)
		{
			messager?.SendMessage(targetEndPoint, answerType, answerValue);
		}
		private static void OnUpdate()
		{
			lock(incomingLock)
			{
				while(incoming.Count > 0)
				{
					ProcessIncoming(incoming.Dequeue());
				}
			}
			lock(clientsLock)
			{
				foreach(var client in clients.Values.ToArray())
				{
					if(EditorApplication.timeSinceStartup - client.LastMessage > 4)
					{
						clients.Remove(client.EndPoint);
					}
				}
			}
		}
		static void ProcessIncoming(Message message)
		{
			lock(clientsLock)
			{
				CheckClient(message);
			}
			switch(message.Type)
			{
				case MessageType.Ping:
					Answer(message, MessageType.Pong);
					break;
				case MessageType.Play:
				case MessageType.PlayToggle:
					{
						if(!EditorApplication.isPlaying)
						{
							Shutdown();
							EditorApplication.isPlaying = true;
						}
						else
						{
							EditorApplication.isPlaying = false;
						}
						break;
					}

				case MessageType.Stop:
					{
						EditorApplication.isPlaying = false;
						break;
					}
				case MessageType.Pause:
				case MessageType.PauseToggle:
					{
						EditorApplication.isPaused = !EditorApplication.isPaused;
						break;
					}
				case MessageType.Unpause:
					{
						EditorApplication.isPaused = false;
						break;
					}
				case MessageType.Build:
					// Not used anymore
					break;
				case MessageType.Refresh:
					Refresh();
					break;
				case MessageType.Version:
					//		Answer(message, MessageType.Version, PackageVersion());
					break;
				case MessageType.UpdatePackage:
					break;
				case MessageType.ProjectPath:
					Answer(message, MessageType.ProjectPath, Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
					break;
				case MessageType.ExecuteTests:
					//	TestRunnerApiListener.ExecuteTests(message.Value);
					break;
				case MessageType.RetrieveTestList:
					//	TestRunnerApiListener.RetrieveTestList(message.Value);
					break;
				case MessageType.ShowUsage:
					//	UsageUtility.ShowUsage(message.Value);
					break;
				case MessageType.PingObject:
					PingObject(message.Value);
					break;
			}
		}
		static void CheckClient(Message message)
		{
			var endPoint = message.Origin;
			if(clients.TryGetValue(endPoint, out var client))
			{
				client.LastMessage = EditorApplication.timeSinceStartup;
				return;
			}
			client = new Client
			{
				EndPoint = endPoint,
				LastMessage = EditorApplication.timeSinceStartup
			};
			clients.Add(endPoint, client);
		}
		static void Refresh()
		{
			if(!EditorPrefs.GetBool("kAutoRefresh", true))
			{
				return;
			}
			RunOnceOnUpdate(AssetDatabase.Refresh);
		}
		static void PingObject(string objectPath)
		{
			var relativePath = Path.GetRelativePath(Application.dataPath, objectPath);
			var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);
			EditorGUIUtility.PingObject(obj);
			Selection.objects = new[] { obj };
		}
	}
}
