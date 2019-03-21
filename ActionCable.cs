using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

public class ActionCable : ChyLib.SingletonMonoBehaviour<ActionCable>
{

    public JObject INTERNAL = JObject.Parse(
        @"{
          'message_types': {
            'welcome': 'welcome',
            'ping': 'ping',
            'confirmation': 'confirm_subscription',
            'rejection': 'reject_subscription'
          },
          'default_mount_path': '/cable',
          'protocols': ['actioncable-v1-json', 'actioncable-unsupported']
        }"
    );
    //public WebSocket WebSocket;
    public class logger : Debug { }
    public Consumer createConsumer(string url)
    {
        string refv = null;
        if (url == null)
        {
            refv = getConfig("url");
            if (refv != null)
            {
                url = refv;
            }
            else {
                url = this.INTERNAL["default_mount_path"].ToString();
            }
        }
        this.consumer = new Consumer(this.createWebSocketURL(url));
        return this.consumer;
    }
    public Consumer consumer;
    public string getConfig(string name = "url")
    {
        if (name == "url")
        {
            return "/cable";
        }
        return null;
    }

    public string createWebSocketURL(string url)
    {
        Regex reg = new Regex("^wss?:", RegexOptions.IgnoreCase);
        if (reg.IsMatch(url) == false)
        {
            return url.Replace("http", "ws");
        }
        else {
            return url;
        }
    }

    public class Consumer
    {
        public string url;

        public Connection connection;
        public Subscriptions subscriptions;
        public Consumer(string url)
        {
            this.url = url;
            this.subscriptions = new Subscriptions(this);
            this.connection = new Connection(this);

        }
        public bool send(Dictionary<string, object> data)
        {
            return this.connection.send(data);
        }

        public bool ensureActiveConnection()
        {
            if (this.connection.isActive() == false) {
                return this.connection.open();
            }
            return true;
        }
    }

    public class Connection
    {
        public Consumer consumer;
        public WebSocket webSocket;
        public Dictionary<string, object> events = new Dictionary<string, object>();
        public Subscriptions subscriptions;
        public Connection(Consumer consumer)
        {
            this.consumer = consumer;
            this.subscriptions = this.consumer.subscriptions;
            //this.events.Add("message", (Func<int, bool>)(n => { return n > 10; }));
        }

        public void Message(string data)
        {
            Dictionary<string, object> ref1 = JsonConvert.DeserializeObject<Dictionary<string, object>>(data);
            if (ref1.ContainsKey("type"))
            {
                Dictionary<string, string> message_types = ActionCable.Instance.INTERNAL["message_types"].ToObject<Dictionary<string, string>>();
                string type = ref1["type"].ToString();
                if (type == message_types["welcome"])
                {
                    //this.monitor.recordConnect();
                    //return this.subscriptions.reload();
                    this.subscriptions.reload();
                    Debug.Log("welcome");
                }
                else if (type == message_types["ping"])
                {
                    //return this.monitor.recordPing();
                    Debug.Log("ping");
                }
                else if (type == message_types["confirmation"])
                {
                    //return this.subscriptions.notify(identifier, "connected");
                    this.subscriptions.notify(ref1["identifier"].ToString(), "connected");
                    Debug.Log("confirmation");
                }
                else if (type == message_types["rejection"])
                {
                    //return this.subscriptions.reject(identifier);
                    Debug.Log("rejection");
                }
                else
                {
                    //this.subscriptions.notify(ref1["identifier"].ToString(), "received", message);
                    Debug.Log("received");
                }
            }
            else
            {
                this.subscriptions.notify(ref1["identifier"].ToString(), "received",ref1["message"].ToString());
                Debug.Log("received");
            }
        }

        public bool open()
        {
            if (this.isActive())
            {
                Debug.Log("Attempted to open WebSocket, but existing socket is " + this.getState());
                return false;
            }
            else
            {
                this.webSocket = new WebSocket(new Uri(this.consumer.url));
                ActionCable.Instance.StartCoroutine(this.webSocket.Connect());                
                return true;
            }
        }
        public bool send(Dictionary<string, object> data)
        {
            if (this.isOpen())
            {
                this.webSocket.SendString(JsonConvert.SerializeObject(data));
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool isOpen()
        {
            return this.isState("open");
        }
        
        public bool isActive()
        {
            return this.isState("open", "connecting");
        }

        public bool isState(params string[] arguments)
        {
            if (arguments.Length >= 1)
            {
                string m_state = this.getState();
                if (m_state != null)
                {
                    return Array.IndexOf(arguments, m_state) >= 0;
                }
            }
            return false;
        }

        public string getState()
        {
            if (this.webSocket != null)
            {
                return this.webSocket.GetState();
            }
            else
            {
                return null;
            }
        }
    }

    public void OnOpen()
    {
        Debug.Log("Opened: ");
    }
    
    public void OnClose()
    {
        Debug.Log("closed: ");
    }

    public void OnMessage()
    {
        string reply = this.consumer.connection.webSocket.RecvString();
        this.consumer.connection.Message(reply);
    }



    public static WebSocket WebSocket(Uri url)
    {
        return WebSocket(url);
    }

    public class Subscription
    {
        public string identifier;
        public Consumer consumer;
        public Dictionary<string, Dictionary<string, object>> mixin;
        public Subscription(Consumer consumer, Dictionary<string, object> params_, Dictionary<string, Dictionary<string, object>> mixin)
        {
            if (params_ == null) {
                params_ = new Dictionary<string, object>();
            }
            this.identifier = JsonConvert.SerializeObject(params_);
            this.consumer = consumer;
            this.mixin = mixin;
        }
        public bool perform(string action, Dictionary<string, object> data = null)
        {
            if (data == null)
            {
                data = new Dictionary<string, object>();
            }
            data.Add("action",action);
            return this.send(data);
        }
        public bool send(Dictionary<string, object> data)
        {
            Dictionary<string, object> send_data = new Dictionary<string, object>()
            {
                {"command","message"},
                {"identifier",this.identifier},
                {"data",JsonConvert.SerializeObject(data)}
            };
            return this.consumer.send(send_data);
        }
    }

    public class Subscriptions
    {
        public Consumer consumer;
        public Stack<Subscription> subscriptions;
        public Subscriptions(Consumer consumer)
        {
            this.consumer = consumer;
            this.subscriptions = new Stack<Subscription>();
        }
        public Subscription create<T>(object channelName,Dictionary<string, Dictionary<string, object>> mixin){
        
            var params_ = new Dictionary<string, object>();
            if (typeof(T) == typeof(string))
            {
                params_["channel"] = Convert.ToString(channelName);
            } else if (typeof(T)  == typeof(Dictionary<string, object>))
            {
                params_ = (Dictionary<string, object>)channelName;
            }
            var subscription = new Subscription(this.consumer, params_, mixin);
            return this.add(subscription);
        }
        public Subscription add(Subscription subscription)
        {
            this.subscriptions.Push(subscription);
            this.consumer.ensureActiveConnection();
            //this.notify(subscription, "initialized");
            //this.sendCommand(subscription, "subscribe");
            return subscription;
        }

        public void reload()
        {
            foreach (Subscription subscription in this.subscriptions)
            {
                this.sendCommand(subscription, "subscribe");
            }
        }
        
        public bool sendCommand(Subscription subscription, string command) {
            return this.consumer.send(new Dictionary<string, object>() {
                {"command",command},
                {"identifier",subscription.identifier} 
            });
        }

        public void notify(params string[] arguments)
        {
            string m_subscription = arguments[0];
            string callbackName = arguments[1];
            object parameter = null;
            if (arguments.Length > 2)
            {
                parameter = arguments[2];
            }
            foreach (Subscription subscription in this.subscriptions)
            {
                if (subscription.identifier == m_subscription)
                {
                    if (subscription.mixin.ContainsKey(callbackName))
                    {
                        Dictionary<string,object> callback = subscription.mixin[callbackName];
                        Type type = (Type) callback["type"];
                        var func = Convert.ChangeType(callback["func"], type);
                        var InvokeMethod = type.GetMethod("Invoke");
                        if (parameter != null)
                        {
                            parameter = JsonConvert.DeserializeObject(parameter.ToString(),type.GetGenericArguments()[1]);
                        }
                        object[] parameters = new object[]{subscription,parameter};
                        InvokeMethod.Invoke(func,parameters);
                        //Debug.Log("tt");
                    }
                }
            }
        }
    }



    /*
    public static partial class INTERNAL {
        public static Dictionary<string, string> message_types = new Dictionary<string, string> {
            { "welcome", "welcome" },
            { "ping", "ping" },
            { "confirmation", "confirm_subscription" },
            { "rejection", "reject_subscription" }
        };
        public static string default_mount_path = "/cable";
        public static string[] protocols = ["actioncable-v1-json", "actioncable-unsupported"];
    }
    public static partial class WebSocket {
    }
    public static partial class logger {
    }
    */




}


