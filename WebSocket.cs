using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;

public class WebSocket
{
	private Uri mUrl;

	public WebSocket(Uri url)
	{
		mUrl = url;

		string protocol = mUrl.Scheme;
		if (!protocol.Equals("ws") && !protocol.Equals("wss"))
			throw new ArgumentException("Unsupported protocol: " + protocol);
	}

	public void SendString(string str)
	{
#if UNITY_WEBGL && !UNITY_EDITOR
		Send(Encoding.UTF8.GetBytes (str));
#else
		Send(str);
#endif
	}

	public string RecvString()
	{
		byte[] retval = Recv();
		if (retval == null)
			return null;
		return Encoding.UTF8.GetString (retval);
	}

	public string GetState()
	{
		int? readyState = null;
		//WebSocketSharp.WebSocketState.TryParse()
#if UNITY_WEBGL && !UNITY_EDITOR
		if (SocketCnt() > 0)
		{  
		  readyState = SocketState(m_NativeRef);
		}
#else
		if (m_Socket != null)
		{
			readyState = (int)m_Socket.ReadyState;
		}
#endif
		if (readyState != null)
		{
			WebSocketSharp.WebSocketState readyStateEnum =
				(WebSocketSharp.WebSocketState) Enum.ToObject(typeof(WebSocketSharp.WebSocketState), readyState.Value);
			return readyStateEnum.ToString().ToLower();
		}
		return null;
	}

	public void PostMessage(string str)
	{
#if UNITY_WEBGL && !UNITY_EDITOR
		if (SocketCnt() > 0)
		{  
		  byte[] buffer = Encoding.UTF8.GetBytes (str);
		  PostMessageToWindow (m_NativeRef, buffer, buffer.Length);
		}
#endif
	}

#if UNITY_WEBGL && !UNITY_EDITOR
	[DllImport("__Internal")]
	private static extern void PostMessageToWindow (int socketInstance, byte[] ptr, int length);

	[DllImport("__Internal")]
	private static extern int SocketCnt ();

	[DllImport("__Internal")]
	private static extern int SocketCreate (string url);

	[DllImport("__Internal")]
	private static extern int SocketState (int socketInstance);

	[DllImport("__Internal")]
	private static extern void SocketSend (int socketInstance, byte[] ptr, int length);

	[DllImport("__Internal")]
	private static extern void SocketRecv (int socketInstance, byte[] ptr, int length);

	[DllImport("__Internal")]
	private static extern int SocketRecvLength (int socketInstance);

	[DllImport("__Internal")]
	private static extern void SocketClose (int socketInstance);

	[DllImport("__Internal")]
	private static extern int SocketError (int socketInstance, byte[] ptr, int length);

	int m_NativeRef = 0;

	public void Send(byte[] buffer)
	{
		SocketSend (m_NativeRef, buffer, buffer.Length);
	}

	public byte[] Recv()
	{
		int length = SocketRecvLength (m_NativeRef);
		if (length == 0)
			return null;
		byte[] buffer = new byte[length];
		SocketRecv (m_NativeRef, buffer, length);
		return buffer;
	}

	public IEnumerator Connect()
	{
		m_NativeRef = SocketCreate (mUrl.ToString());

		while (SocketState(m_NativeRef) == 0)
			yield return 0;
	}
 
	public void Close()
	{
		SocketClose(m_NativeRef);
	}

	public string error
	{
		get {
			const int bufsize = 1024;
			byte[] buffer = new byte[bufsize];
			int result = SocketError (m_NativeRef, buffer, bufsize);

			if (result == 0)
				return null;

			return Encoding.UTF8.GetString (buffer);				
		}
	}
#else
	WebSocketSharp.WebSocket m_Socket;
	Queue<byte[]> m_Messages = new Queue<byte[]>();
	bool m_IsConnected = false;
	string m_Error = null;

	public IEnumerator Connect()
	{
		m_Socket = new WebSocketSharp.WebSocket(mUrl.ToString());
		m_Socket.OnMessage += (sender, e) => { m_Messages.Enqueue(e.RawData); ActionCable.Instance.OnMessage(); };
        m_Socket.OnOpen += (sender, e) =>
        {
	        m_IsConnected = true;
	        ActionCable.Instance.OnOpen();
        };
		m_Socket.OnError += (sender, e) => m_Error = e.Message;
		m_Socket.OnClose += (sender, e) =>
		{
			ActionCable.Instance.OnClose();
		};
			
		m_Socket.ConnectAsync();
		while (!m_IsConnected && m_Error == null)
			yield return 0;
	}

	public void Send(byte[] buffer)
	{
		m_Socket.Send(buffer);
	}
	
	public void Send(string str)
	{
		m_Socket.Send(str);
	}

	public byte[] Recv()
	{
		if (m_Messages.Count == 0)
			return null;
		return m_Messages.Dequeue();
	}

	public void Close()
	{
		m_Socket.Close();
	}

	public string error
	{
		get {
			return m_Error;
		}
	}
#endif 
}