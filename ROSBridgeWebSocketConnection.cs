using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using System;
using WebSocketSharp;
using SimpleJSON;
using UnityEngine;

/**
 * This class handles the connection with the external ROS world, deserializing
 * json messages into appropriate instances of packets and messages.
 * 
 * This class also provides a mechanism for having the callback's exectued on the rendering thread.
 * (Remember, Unity has a single rendering thread, so we want to do all of the communications stuff away
 * from that. 
 * 
 * The one other clever thing that is done here is that we only keep 1 (the most recent!) copy of each message type
 * that comes along.
 * 
 * Version History
 * 3.1 - changed methods to start with an upper case letter to be more consistent with c#
 * style.
 * 3.0 - modification from hand crafted version 2.0
 * 
 * @author Michael Jenkin, Robert Codd-Downey and Andrew Speers
 * @version 3.1
 */

 namespace ROSBridgeLib {

    public interface ROSTopicSubscriber
    {
        ROSBridgeMsg OnReceiveMessage(string topic, JSONNode raw_msg, ROSBridgeMsg parsed = null);
        string GetMessageType(string topic);
    }

 	public class ROSBridgeWebSocketConnection {
        // <Changed>
        public delegate void JSONServiceResponseHandler(JSONNode node);
        // </Changed>
        // <Changed>
        private class RenderTask {
            //private Type _subscriber;
 			public string topic;
 			public JSONNode msg;

 			public RenderTask(string topic, JSONNode msg) {
 				this.topic = topic;
 				this.msg = msg;
 			}
 		};
        // </Changed>

 		private string _host;
 		private int _port;
 		private WebSocket _ws;
 		private System.Threading.Thread _myThread;

        // <Changed>
        //private List<Type> _subscribers; // our subscribers
        //private List<Type> _publishers; //our publishers
        private Dictionary<string, List<Type>> static_subscribers;
        private Dictionary<string, List<Type>> static_publishers;
        private Dictionary<string, List<ROSTopicSubscriber>> subscribers;
        private Dictionary<string, string> publishTopic_to_messageType;
        //private Dictionary<string, List<Type>> publishers;
        // </Changed>

        private Type _serviceResponse; // to deal with service responses
		private string _serviceName = null;
        private JSONNode _rawServiceValues = null;
		private string _serviceValues = null;
		private Queue<RenderTask> _taskQ = new Queue<RenderTask>();
		// <Changed>
		private List<Type> _jsonResponseListeners;
        private Dictionary<string, JSONServiceResponseHandler> _pendingServiceResponses;
        private Queue<JSONNode> _responsesQ;
        // </Changed>

        private object _queueLock = new object ();

		private static string GetMessageType(Type t) {
			return (string) t.GetMethod ("GetMessageType", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Invoke (null, null);
		}

		private static string GetMessageTopic(Type t) {
			return (string) t.GetMethod ("GetMessageTopic", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Invoke (null, null);
		}

		private static ROSBridgeMsg ParseMessage(Type t, JSONNode node) {
			return (ROSBridgeMsg) t.GetMethod ("ParseMessage", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Invoke (null, new object[] {node});
		}

		private static void Update(Type t, ROSBridgeMsg msg) {
			t.GetMethod ("CallBack", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Invoke (null, new object[] {msg});
		}

		private static void ServiceResponse(Type t, string service, string yaml) {
			t.GetMethod ("ServiceCallBack", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Invoke (null, new object[] {service, yaml});
		}

		private static void IsValidServiceResponse(Type t) {
			if (t.GetMethod ("ServiceCallBack", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) == null)
			throw new Exception ("invalid service response handler");
		}

		// <Changed>
		private static string GetJSONServiceName(Type t) {
			return (string) t.GetMethod ("GetServiceName", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Invoke (null, null);
		}

		private static void JSONServiceResponse(Type t, JSONNode node) {
			t.GetMethod ("JSONServiceCallBack", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Invoke (null, new object[] {node});
		}

		private static void IsValidJSONServiceResponse(Type t) {
			if (t.GetMethod ("JSONServiceCallBack", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) == null)
			throw new Exception ("invalid JSON service response handler");
			if (t.GetMethod ("GetServiceName", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) == null)
			throw new Exception ("missing GetServiceName method");
		}
		// </Changed>

		private static void IsValidStaticSubscriber(Type t) {
			if(t.GetMethod ("CallBack", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) == null)
                throw new Exception ("missing Callback method");
			if (t.GetMethod ("GetMessageType", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) == null)
                throw new Exception ("missing GetMessageType method");
			if(t.GetMethod ("GetMessageTopic", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) == null)
                throw new Exception ("missing GetMessageTopic method");
			if(t.GetMethod ("ParseMessage", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) == null)
                throw new Exception ("missing ParseMessage method");
		}

		private static void IsValidStaticPublisher(Type t) {
			if (t.GetMethod ("GetMessageType", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) == null)
                throw new Exception ("missing GetMessageType method");
			if(t.GetMethod ("GetMessageTopic", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) == null)
                throw new Exception ("missing GetMessageTopic method");
		}

		/**
		 * Make a connection to a host/port. 
		 * This does not actually start the connection, use Connect to do that.
		 */
		 public ROSBridgeWebSocketConnection(string host, int port) {
		 	_host = host;
		 	_port = port;
		 	_myThread = null;

            // <Changed>
            //_subscribers = new List<Type> ();
            //_publishers = new List<Type> ();
            static_subscribers = new Dictionary<string, List<Type>>();
            static_publishers = new Dictionary<string, List<Type>>();
            subscribers = new Dictionary<string, List<ROSTopicSubscriber>>();
            publishTopic_to_messageType = new Dictionary<string, string>();
            //publishers = new Dictionary<string, List<Type>>();
            // </Changed>

            // <Changed>
            _jsonResponseListeners = new List<Type> ();
            _pendingServiceResponses = new Dictionary<string, JSONServiceResponseHandler>();
            _responsesQ = new Queue<JSONNode>();
            // </Changed>
        }

		/**
		 * Add a service response callback to this connection.
		 */
		 public void AddServiceResponse(Type serviceResponse) {
		 	IsValidServiceResponse (serviceResponse);
		 	_serviceResponse = serviceResponse;
		 }

		 // <Changed>
		 public void AddJSONServiceResponse(Type jsonServiceResponse) {
		 	IsValidJSONServiceResponse (jsonServiceResponse);
		 	_jsonResponseListeners.Add (jsonServiceResponse);
		 }
        // </Changed>

        // <Changed>
        /**
		 * Add a subscriber callback to this connection. There can be many subscribers.
		 */
        public void AddSubscriber(Type subscriber) {
		 	IsValidStaticSubscriber(subscriber);
            string topicName = (string) subscriber.GetMethod("GetMessageTopic", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Invoke(null, new object[] { });
            List<Type> topic_subs;
            if (static_subscribers.TryGetValue(topicName, out topic_subs))
            {
                if (!topic_subs.Contains(subscriber))
                {
                    topic_subs.Add(subscriber);
                }
            }
            else
            {
                topic_subs = new List<Type>(2);
                topic_subs.Add(subscriber);
                static_subscribers.Add(topicName, topic_subs);
            }
        }

        public void AddSubscriber(string topicName, ROSTopicSubscriber subscriber)
        {
            List<ROSTopicSubscriber> topic_subs;
            if (subscribers.TryGetValue(topicName, out topic_subs))
            {
                if (!topic_subs.Contains(subscriber))
                {
                    topic_subs.Add(subscriber);
                }
            } else
            {
                topic_subs = new List<ROSTopicSubscriber>(2);
                topic_subs.Add(subscriber);
                subscribers.Add(topicName, topic_subs);
            }
        }

		/**
		 * Add a publisher to this connection. There can be many publishers.
		 */
		 public void AddPublisher(Type publisher) {
		 	IsValidStaticPublisher(publisher);
            string topicName = (string)publisher.GetMethod("GetMessageTopic", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Invoke(null, new object[] { });
            List<Type> topic_pubs;
            if (static_publishers.TryGetValue(topicName, out topic_pubs))
            {
                if (!topic_pubs.Contains(publisher))
                {
                    topic_pubs.Add(publisher);
                }
            }
            else
            {
                topic_pubs = new List<Type>(2);
                topic_pubs.Add(publisher);
                static_publishers.Add(topicName, topic_pubs);
            }
        }

        public void AddPublisher(string topic, string message_type)
        {
            if (publishTopic_to_messageType.ContainsKey(topic))
            {
                return;
            }
            publishTopic_to_messageType[topic] = message_type;
        }
        // </Changed>

        /**
		 * Connect to the remote ros environment.
		 */
        public void Connect() {
		 	_myThread = new System.Threading.Thread (Run);
		 	_myThread.Start ();
		 }

		/**
		 * Disconnect from the remote ros environment.
		 */
		 public void Disconnect() {
		 	_myThread.Abort ();
		 	foreach(string topicName in static_subscribers.Keys) {
		 		_ws.Send(ROSBridgeMsg.UnSubscribe(topicName));
		 		Debug.Log ("Sending " + ROSBridgeMsg.UnSubscribe(topicName));
		 	}
            foreach(string topicName in subscribers.Keys)
            {
                if (!static_subscribers.ContainsKey(topicName))
                {
                    _ws.Send(ROSBridgeMsg.UnSubscribe(topicName));
                    Debug.Log("Sending " + ROSBridgeMsg.UnSubscribe(topicName));
                }
            }
		 	foreach(string topicName in static_publishers.Keys) {
		 		_ws.Send(ROSBridgeMsg.UnAdvertise (topicName));
		 		Debug.Log ("Sending " + ROSBridgeMsg.UnAdvertise(topicName));
		 	}
		 	_ws.Close ();
            static_subscribers.Clear();
            static_publishers.Clear();
            subscribers.Clear();
		 }

		 private void Run() {
		 	_ws = new WebSocket(_host + ":" + _port);
		 	_ws.OnMessage += (sender, e) => this.OnMessage(e.Data);
		 	_ws.Connect();

		 	foreach(KeyValuePair<string, List<Type>> topic in static_subscribers) {
		 		_ws.Send(ROSBridgeMsg.Subscribe (topic.Key, GetMessageType (topic.Value[0])));
		 		Debug.Log ("Sending " + ROSBridgeMsg.Subscribe(topic.Key, GetMessageType(topic.Value[0])));
		 	}
            foreach(KeyValuePair<string, List<ROSTopicSubscriber>> topic in subscribers)
            {
                if (!static_subscribers.ContainsKey(topic.Key))
                {
                    _ws.Send(ROSBridgeMsg.Subscribe(topic.Key, topic.Value[0].GetMessageType(topic.Key)));
                    Debug.Log("Sending " + ROSBridgeMsg.Subscribe(topic.Key, topic.Value[0].GetMessageType(topic.Key)));
                }
            }

		 	foreach(KeyValuePair<string, List<Type>> topic in static_publishers) {
		 		_ws.Send(ROSBridgeMsg.Advertise (topic.Key, GetMessageType(topic.Value[0])));
		 		Debug.Log ("Sending " + ROSBridgeMsg.Advertise(topic.Key, GetMessageType(topic.Value[0])));
		 	}
            foreach(KeyValuePair<string, string> topic in publishTopic_to_messageType)
            {
                if (!static_publishers.ContainsKey(topic.Key))
                {
                    _ws.Send(ROSBridgeMsg.Advertise(topic.Key, topic.Value));
                    Debug.Log("Sending " + ROSBridgeMsg.Advertise(topic.Key, topic.Value));
                }
            }
		 	//while(true) {
		 	//	Thread.Sleep (1000);
		 	//}
		 }

		 private void OnMessage(string s) {
		 	//Debug.Log ("Got a message " + s);
		 	if((s != null) && !s.Equals ("")) {
		 		JSONNode node = JSONNode.Parse(s);
                //Debug.Log ("Parsed it");
		 		string op = node["op"];
                //Debug.Log ("Operation is " + op);
		 		if ("publish".Equals (op)) {
                    //Debug.Log("Socket: " + _ws.IsAlive + ", " + _ws.ReadyState);
                    string topic = node["topic"];
                    //Debug.Log ("Got a message on " + topic);
                    // <Changed>
                    lock (_queueLock)
                    {
                        _taskQ.Enqueue(new RenderTask(topic, node["msg"]));
                    }
                    //foreach(Type p in _subscribers) {
                    //	if(topic.Equals (GetMessageTopic (p))) {
                    //      //Debug.Log ("And will parse it " + GetMessageTopic (p));
                    //		ROSBridgeMsg msg = ParseMessage(p, node["msg"]);
                    //		RenderTask newTask = new RenderTask(p, topic, msg);
                    //		lock(_queueLock) {
                    //			bool found = false;
                    //			for(int i=0;i<_taskQ.Count;i++) {
                    //				if(_taskQ[i].getTopic().Equals (topic)) {
                    //					_taskQ.RemoveAt (i);
                    //					_taskQ.Insert (i, newTask);
                    //					found = true;
                    //					break;
                    //				}
                    //			}
                    //			if(!found)
                    //			_taskQ.Add (newTask);
                    //		}

                    //	}
                    //}
                    // </Changed>
                }
                else if("service_response".Equals (op)) {
                    // <Changed>
                    //Debug.Log ("Got service response " + node.ToString ());
                    // </Changed>
                    string service_name = node["service"];
                    // <Changed>
                    Debug.Log(service_name);
                    Type targetListener = null;
                    if (!(node["id"] is JSONLazyCreator))
                    {
                        _responsesQ.Enqueue(node);
                    }
                    else
                    {
                        foreach (Type p in _jsonResponseListeners)
                        {
                            if (GetJSONServiceName(p).Equals(service_name))
                            {
                                targetListener = p;
                                break;
                            }
                        }
                        if (targetListener != null)
                        {
                            //Debug.Log("Socket: " + _ws.IsAlive + ", " + _ws.ReadyState);
                            //JSONServiceResponse(targetListener, node["values"]);
                            _serviceName = service_name;
                            _rawServiceValues = node["values"];
                            _serviceResponse = targetListener;

                        }
                        else
                        {
                            _serviceName = service_name;
                            _serviceValues = (node["values"] == null) ? "" : node["values"].ToString();
                        }
                        // _serviceValues = (node["values"] == null) ? "" : node["values"].ToString ();
                    }
                    // </Changed>
                }
                else
 					Debug.Log ("Must write code here for other messages");
			} else
				Debug.Log ("Got an empty message from the web socket");
		}

		public void Render() {
			RenderTask newTask = null;
            lock (_queueLock)
            {
                while (_taskQ.Count > 0)
                {
                    newTask = _taskQ.Dequeue();

                    // <Changed>
                    if (newTask != null)
                    {
                        List<Type> static_subs;
                        if (static_subscribers.TryGetValue(newTask.topic, out static_subs) && static_subs.Count > 0)
                        {
                            ROSBridgeMsg parsed_msg = ParseMessage(static_subs[0], newTask.msg);
                            foreach (Type curr_sub in static_subs)
                            {
                                Update(curr_sub, parsed_msg);
                            }
                        }
                        List<ROSTopicSubscriber> subs;
                        if (subscribers.TryGetValue(newTask.topic, out subs) && subs.Count > 0)
                        {
                            ROSBridgeMsg parsed_msg = null;
                            foreach (ROSTopicSubscriber curr_sub in subs)
                            {
                                if (parsed_msg == null)
                                {
                                    parsed_msg = curr_sub.OnReceiveMessage(newTask.topic, newTask.msg);
                                }
                                else
                                {
                                    curr_sub.OnReceiveMessage(newTask.topic, newTask.msg, parsed: parsed_msg);
                                }
                            }
                        }
                    }
                    // </Changed>

                    // <Changed>
                    while (_responsesQ.Count > 0)
                    {
                        JSONNode curr_response = _responsesQ.Dequeue();
                        JSONServiceResponseHandler handler;
                        if (!_pendingServiceResponses.TryGetValue(curr_response["id"].Value, out handler))
                        {
                            continue;
                        }
                        handler(curr_response["values"]);
                    }
                    // </Changed>

                    // <Changed>
                    if (_serviceName != null && _serviceResponse != null)
                    {
                        if (_serviceValues != null)
                        {
                            ServiceResponse(_serviceResponse, _serviceName, _serviceValues);
                        }
                        else if (_rawServiceValues != null)
                        {
                            JSONServiceResponse(_serviceResponse, _rawServiceValues);
                        }
                        _serviceValues = null;
                        _rawServiceValues = null;

                        _serviceName = null;
                        _serviceResponse = null;
                    }
                    // </Changed>
                }
            }
        }

        public void Publish(String topic, ROSBridgeMsg msg) {
			if(_ws != null) {
				string s = ROSBridgeMsg.Publish (topic, msg.ToYAMLString ());
				//Debug.Log ("Sending " + s);
				_ws.Send (s);
			}
		}

		public void CallService(string service, string args) {
			if (_ws != null) {
				string s = ROSBridgeMsg.CallService (service, args);
				Debug.Log ("Sending " + s);
				_ws.Send (s);
			}
		}

        // <Changed>
        public void CallService(JSONServiceResponseHandler handler, string service_name, string id, string args = "[]")
        {
            if (_ws != null)
            {
                _pendingServiceResponses[id] = handler;
                string s = ROSBridgeMsg.CallService(service_name, id, args);
                Debug.Log("Sending " + s);
                _ws.Send(s);
            }
        }
    }
}
