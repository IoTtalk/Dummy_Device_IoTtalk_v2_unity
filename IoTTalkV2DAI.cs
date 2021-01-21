using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

using UnityEngine.Networking;
using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using IoTTalkUnity.Dan;

public class DeviceFeature
{
    public string df_name;
    public List<Type> df_type;
    public System.Object push_data;
    public System.Object on_data;

    public DeviceFeature(string df_name, List<Type> df_type=null){
        this.df_name = df_name;
        this.df_type = new List<Type>();

        if( df_type != null ){
            this.df_type = df_type;
        } else {
            this.df_type.Add(null); 
        }        
        push_data = null;
        on_data = null;
    }

    public JArray profile(){
        return new JArray{this.df_name, JArray.FromObject(this.df_type)};
    }
}

static public class Global
{
    static public Dictionary<string, bool> _flags = new Dictionary<string, bool>();
    static public Dictionary<string, DeviceFeature> _devices = new Dictionary<string, DeviceFeature>();
    static public Dictionary<string, int> _interval = new Dictionary<string, int>();
}

public class IoTTalkV2DAI : MonoBehaviour
{

    private Client _default_client;
    public GameObject sa_object;

    private List<Thread> thread_pool = new List<Thread>();

    public delegate object TestFunc(params object[] args);
    
    void Start(){
        _default_client = new Client();
        main();
    }
    
    public void push_data(System.Object df_name_object){

        Type df_type =  df_name_object.GetType();
        dynamic df_name = Convert.ChangeType( df_name_object, df_type);
        
        if ( Global._devices[df_name].push_data == null){
            return;
        }
            
        while (Global._flags[df_name]){
            Type f_type =  Global._devices[df_name].push_data.GetType();
            dynamic f = Convert.ChangeType(Global._devices[df_name].push_data, f_type);
            dynamic _data = f.Invoke(new System.Object[]{});
            _default_client.push(df_name, _data);
            Thread.Sleep(Global._interval[df_name]*1000);
        }
        Global._flags.Remove(df_name);
    }

    public bool on_data(string df_name, System.Object data){
        Type f_type =  Global._devices[df_name].on_data.GetType();
        dynamic f = Convert.ChangeType(Global._devices[df_name].on_data, f_type);
        f.Invoke(new System.Object[]{data});
        return true;
    }

    public bool on_signal(string signal, List<string> df_list){
        if( "CONNECT" == signal){
            foreach(string df_name in df_list){
                if(!Global._flags.ContainsKey(df_name)){
                    Global._flags.Add(df_name, true);
                    Thread t = new Thread(push_data);
                    t.Start((System.Object)df_name); // 限定一個參數，多參數虛為 list 或 class
                    thread_pool.Add(t);
                }
            }
        } else if ("DISCONNECT" == signal){
            foreach(string df_name in df_list){
                Global._flags[df_name] = false;
            }
        } else if ("SUSPEND" == signal) {
            // Not use
        } else if ("RESUME" == signal) {
            // Not use
        }
        return true;
    }

    public void main(){
        string csmapi = sa_object.GetComponent<IoTTalkV2SA>().api_url;
        string device_name = sa_object.GetComponent<IoTTalkV2SA>().device_name;
        string device_model = sa_object.GetComponent<IoTTalkV2SA>().device_model;
        int _push_interval = sa_object.GetComponent<IoTTalkV2SA>().push_interval;
        string username = sa_object.GetComponent<IoTTalkV2SA>().username;
        string extra_setup_webpage = sa_object.GetComponent<IoTTalkV2SA>().extra_setup_webpage;       
        string device_webpage = sa_object.GetComponent<IoTTalkV2SA>().device_webpage;
        Global._interval = sa_object.GetComponent<IoTTalkV2SA>().interval;
        System.Action on_register = sa_object.GetComponent<IoTTalkV2SA>().on_register;

        JArray idf_list = new JArray();
        JArray odf_list = new JArray();

        foreach (string df_profile in sa_object.GetComponent<IoTTalkV2SA>().idf_list){
            Global._devices.Add(df_profile, new DeviceFeature(df_name:df_profile));
            Global._devices[df_profile].push_data = sa_object.GetComponent<IoTTalkV2SA>().df[df_profile];
            idf_list.Add(Global._devices[df_profile].profile());

            if( !Global._interval.ContainsKey(df_profile)){
                Global._interval.Add(df_profile, sa_object.GetComponent<IoTTalkV2SA>().push_interval);
            }
        }

        foreach (string df_profile in sa_object.GetComponent<IoTTalkV2SA>().odf_list){
            Global._devices.Add(df_profile, new DeviceFeature(df_name:df_profile));
            Global._devices[df_profile].on_data = sa_object.GetComponent<IoTTalkV2SA>().df[df_profile];
            odf_list.Add(Global._devices[df_profile].profile());
        }

        JObject profile = new JObject();
        profile.Add("model", device_model);
        profile.Add("uname", username);
        profile.Add("extra_setup_webpage", extra_setup_webpage);
        profile.Add("device_webpage", device_webpage);

        StartCoroutine(
            _default_client.Asyncregister(
                url:csmapi,
                on_signal:this.on_signal,
                on_data:this.on_data,
                id_:null, 
                name:device_name,
                idf_list:idf_list, 
                odf_list:odf_list,
                accept_protos: new List<string>(new string[] {"mqtt"}),
                profile:profile,
                on_register: on_register,
                on_deregister: null
            )
        );
    }

    void OnApplicationQuit(){
        foreach (var t in thread_pool){
            t.Abort();
        }
        var de = _default_client.Asyncderegister();

        while(de.MoveNext()){}
        Debug.Log("Exit");
    }
}
