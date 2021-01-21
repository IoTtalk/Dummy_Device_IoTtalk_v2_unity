using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

using UnityEngine.Networking;
using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using uPLibrary.Networking.M2Mqtt.Utility;
using uPLibrary.Networking.M2Mqtt.Exceptions;

namespace IoTTalkUnity.Dan
{

    public delegate bool OnData(string df_name, System.Object data);
    public delegate bool OnSignal(string signal, List<string> df_list);

    public class Context {

        public string url;
        public string app_id;
        public string name;
        public string mqtt_host;
        public int mqtt_port;
        public MqttClient mqtt_client;
        public Dictionary<string, string> i_chans;
        public Dictionary<string, string> o_chans;
        public OnData on_data;
        public OnSignal on_signal;
        public string rev;
        public Action on_deregister;
        
        public Context(){
            url = null;
            app_id = null;
            name = null;
            mqtt_host = null;
            mqtt_port = -1;
            mqtt_client = null;
            i_chans = new Dictionary<string, string>();
            o_chans = new Dictionary<string, string>();
            rev = null;
            on_data = null;
            on_signal = null;
            on_deregister = null;
        }
    }

    class RegistrationErrorException : Exception, ISerializable
    {
        public RegistrationErrorException()
            : base() { }
        public RegistrationErrorException(string message) 
            : base(message) { }
        public RegistrationErrorException(string message, Exception inner) 
            : base(message, inner) { }
        protected RegistrationErrorException(SerializationInfo info, StreamingContext context) 
            : base(info, context) { }
    }

    class ApplicationNotFoundErrorException : Exception, ISerializable
    {
        public ApplicationNotFoundErrorException()
            : base() { }
        public ApplicationNotFoundErrorException(string message) 
            : base(message) { }
        public ApplicationNotFoundErrorException(string message, Exception inner) 
            : base(message, inner) { }
        protected ApplicationNotFoundErrorException(SerializationInfo info, StreamingContext context) 
            : base(info, context) { }
    }

    class AttributeNotFoundErrorException : Exception, ISerializable
    {
        public AttributeNotFoundErrorException()
            : base() { }
        public AttributeNotFoundErrorException(string message) 
            : base(message) { }
        public AttributeNotFoundErrorException(string message, Exception inner) 
            : base(message, inner) { }
        protected AttributeNotFoundErrorException(SerializationInfo info, StreamingContext context) 
            : base(info, context) { }
    }

    public class Client
    {
        public Context context = new Context();
        
        static bool IsGuid(string guid) {
            Guid g = Guid.Empty;
            return Guid.TryParse(guid, out g);
        }


        private void on_message(object sender, MqttMsgPublishEventArgs e){
            if(this.context.mqtt_client == null){
                return;
            }

            string payload = System.Text.Encoding.UTF8.GetString(e.Message);
            if(e.Topic == this.context.o_chans["ctrl"]) {
                JObject signal = JObject.Parse(payload);
                if( signal["command"].ToString() == "CONNECT") {
                    if( signal["idf"] != null ){
                        string idf = signal["idf"].ToString();
                        this.context.i_chans.Add(idf, signal["topic"].ToString());
                        
                        bool handling_result = this.context.on_signal(
                            signal["command"].ToString(), new List<string>(){idf}
                        );
                    } else if ( signal["odf"] != null ) {
                        string odf = signal["odf"].ToString();
                        this.context.o_chans.Add(odf, signal["topic"].ToString());
                        
                        bool handling_result = this.context.on_signal(
                            signal["command"].ToString(), new List<string>(){odf}
                        );

                        this.context.mqtt_client.Subscribe(new string[]{this.context.o_chans[odf]}, new byte[] {  MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE});
                    }
                } else if ( signal["command"].ToString() == "DISCONNECT" ) {
                    if( signal["idf"] != null ){
                        string idf = signal["idf"].ToString();
                        this.context.i_chans.Remove(idf);

                        bool handling_result = this.context.on_signal(
                            signal["command"].ToString(), new List<string>(){idf}
                        );

                    } else if ( signal["odf"] != null ){
                        string odf = signal["odf"].ToString();
                        this.context.mqtt_client.Unsubscribe(new string[]{this.context.o_chans[odf]});
                        this.context.o_chans.Remove(odf);
                        
                        bool handling_result = this.context.on_signal(
                            signal["command"].ToString(), new List<string>(){odf}
                        );
                    }
                }
            } else {
                if(!this.context.o_chans.ContainsValue(e.Topic)){
                    return;
                }
                string df = this.context.o_chans.FirstOrDefault(x => x.Value == e.Topic).Key;
                this.context.on_data(df, JArray.Parse(payload)[0]);
            }

        }

        public IEnumerator Asyncregister(
            string url, OnSignal on_signal, OnData on_data,
            string id_ = null, string name = null,
            JArray idf_list = null, JArray odf_list = null,
            List<string> accept_protos = null,
            JObject profile = null,
            System.Action on_register = null,
            System.Action on_deregister = null
            ) {
            // string test_url = "https://httpbin.org/post";
            // string logindataJsonString = "{ \"Name\":\"John\",\"Occupation\":\"gardener\"}";
            // string profile = "{\"name\": \"Dummy_Test\", \"idf_list\": [[\"Dummy_Sensor\", [null]]], \"odf_list\": [[\"Dummy_Control\", [null]]], \"accept_protos\": [\"mqtt\"], \"profile\": {\"model\": \"Dummy_Device\", \"u_name\": null, \"extra_setup_webpage\": \"\", \"device_webpage\": \"\"}}";
            // string url = "https://iottalk2.tw/csm/"+Guid.NewGuid().ToString();

            Context ctx = this.context;

            if ( ctx.mqtt_client != null){
                throw new RegistrationErrorException("Already registered");
            }

            ctx.url = url;

            if (String.IsNullOrEmpty(ctx.url)){
                throw new RegistrationErrorException("Invalid url");
            }

            ctx.app_id = IsGuid(id_) ? id_ : Guid.NewGuid().ToString();

            JObject body = new JObject();
            if(name != null){
                body.Add("name", name);
            }

            if(idf_list.Any()){
                body.Add("idf_list", idf_list);
            }

            if(odf_list.Any()){
                body.Add("odf_list", odf_list);
            }

            if(accept_protos != null){
                body.Add("accept_protos", JArray.FromObject(accept_protos));
            } else {
                body.Add("accept_protos", new JArray(){"mqtt"});
            }

            if(profile != null){
                body.Add("profile", profile);
            }

            byte[] data = System.Text.Encoding.UTF8.GetBytes(body.ToString());
            
            using (UnityWebRequest request = UnityWebRequest.Put(String.Format("{0}/{1}", ctx.url, ctx.app_id), data))
            {
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();

                if (request.isNetworkError || request.isHttpError)
                {
                    Debug.Log(request.error);
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (System.Collections.Generic.KeyValuePair<string, string> dict in request.GetResponseHeaders())
                    {
                        sb.Append(dict.Key).Append(": \t[").Append(dict.Value).Append("]\n");
                    }

                    // Headers : sb.ToString()
                    // Body : request.downloadHandler.text

                    JObject metadata = JObject.Parse(request.downloadHandler.text);
                    JObject metedata_url = JObject.Parse(metadata["url"].ToString());
                    JArray metadata_ctrl_chans = JArray.Parse(metadata["ctrl_chans"].ToString());

                    ctx.name = metadata["name"].ToString();
                    ctx.mqtt_host = metedata_url["host"].ToString();
                    ctx.mqtt_port = int.Parse(metedata_url["port"].ToString());
                    ctx.i_chans.Add("ctrl", metadata_ctrl_chans[0].ToString());
                    ctx.o_chans.Add("ctrl", metadata_ctrl_chans[1].ToString());
                    ctx.rev = metadata["rev"].ToString();
                    ctx.url = url;
                    ctx.on_signal = on_signal;
                    ctx.on_data = on_data;

                    ctx.mqtt_client = new MqttClient(
                            this.context.mqtt_host, 
                            this.context.mqtt_port,
                            false,
                            null
                        );

                    string client_id = String.Format("iottalk-py-{0}", Guid.NewGuid().ToString("N"));
                    string will_msg = "{\"state\": \"offline\", \"rev\":" + metadata["rev"].ToString() + "}";

                    ctx.mqtt_client.MqttMsgPublished += client_MqttMsgPublished;
                    ctx.mqtt_client.MqttMsgPublishReceived += on_message;
                    ctx.mqtt_client.MqttMsgSubscribed += Client_MqttMsgSubscribed;

                    byte code = ctx.mqtt_client.Connect(
                            clientId:client_id,
                            username:null,
                            password:null,
                            willRetain:true,
                            willQosLevel:0,
                            willFlag:true,
                            willTopic:metadata_ctrl_chans[0].ToString(),
                            willMessage:will_msg,
                            cleanSession:false,
                            keepAlivePeriod:60
                        );

                    string content = "{\"state\":\"online\", \"rev\":\""+ this.context.rev + "\"}";
                    ctx.mqtt_client.Publish(ctx.i_chans["ctrl"] , System.Text.Encoding.UTF8.GetBytes(content), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);

                    ctx.mqtt_client.Subscribe(new string[]{ctx.o_chans["ctrl"]}, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE});
                    if( on_register != null){
                        on_register();
                    }
                    yield return null;
                }   
            }
        }

        public IEnumerator Asyncderegister()
        {   
            if( this.context.mqtt_client == null ){
                throw new RegistrationErrorException("Not registered");
            }

            string content = "{\"state\":\"offline\", \"rev\":\""+ this.context.rev + "\"}";
            this.context.mqtt_client.Publish(this.context.i_chans["ctrl"] , System.Text.Encoding.UTF8.GetBytes(content), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
            
            string DData = "{\"rev\":\""+this.context.rev+"\"}";
            byte[] bs=Encoding.UTF8.GetBytes(DData);

            UnityWebRequest request=new UnityWebRequest(String.Format("{0}/{1}", context.url, context.app_id), UnityWebRequest.kHttpVerbDELETE);

            var uploader = new UploadHandlerRaw(bs);

            uploader.contentType="application/json";

            request.uploadHandler = uploader;

            var downloader = new DownloadHandlerBuffer();

            request.downloadHandler = downloader;

            yield return request.SendWebRequest();

            while(!request.isDone){
                yield return null;
            }
            
            if(request.responseCode != 200){
                JObject response;
                try {
                    response = JObject.Parse(System.Text.Encoding.UTF8.GetString(downloader.data));
                } catch (JsonReaderException ex){
                    throw new RegistrationErrorException("Invalid response from server");
                }
                throw new RegistrationErrorException(response["reason"].ToString());
            }
            
            this.context.mqtt_client.Disconnect();

            if( this.context.on_deregister != null){
                this.context.on_deregister();
            }

            Debug.Log("Deregister Success");
        }

        private void Client_MqttMsgSubscribed(object sender, MqttMsgSubscribedEventArgs e) {

        }

        private void client_MqttMsgPublished(object sender, MqttMsgPublishedEventArgs e) {

        }

        // 使用時 Swap< class name >(p1, p2, ... );
        public bool push<T>(string idf, T data){
            
            if( this.context.mqtt_client == null ){
                throw new RegistrationErrorException("Not registered");
            }

            if( this.context.o_chans.ContainsKey(idf) == true){
                return false;
            }

            string content;
            if(data.GetType().IsGenericType && data.GetType().GetGenericTypeDefinition() == typeof(List<>)){
                content = JArray.FromObject(data).ToString();
            } else {
                content = JArray.FromObject(new List<T>(){data}).ToString();
            }

            this.context.mqtt_client.Publish(this.context.i_chans[idf] , System.Text.Encoding.UTF8.GetBytes(content), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
            return true;
        }
    }
}
