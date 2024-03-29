using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using UnityEngine.Networking;
using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public class IoTTalkV2SA :  MonoBehaviour {
    public string api_url = "https://iottalk2.tw/csm";
    public string device_name = "DummyTest";
    public string device_model = "Dummy_Device";
    public int push_interval = 1000; // millisecond
    public string extra_setup_webpage = "";
    public string device_webpage = "";
    public string username = null;
    public string id = null;
    public List<string> idf_list;
    public List<string> odf_list;
    public Dictionary<string, int> interval;

    public Dictionary<string, System.Object> df;
    
    public delegate object DeviceFeatureFunc(params object[] args);

    private List<Vector3> position;

    System.Random r = new System.Random();

    void Awake(){
        // init
        df = new Dictionary<string, System.Object>();
        idf_list = new List<string>();
        odf_list = new List<string>();
        interval = new Dictionary<string, int>();

        // IDF push interval
        interval.Add("Dummy_Sensor", 1000);
        //---------------------------------------
        // device feature function
        //---------------------------------------
        df.Add("DummySensor-I", ConvertFunctionToObject(this.Dummy_Sensor));
        df.Add("DummyControl-O", ConvertFunctionToObject(this.Dummy_Control));

        // idf name list
        idf_list.Add("DummySensor-I");

        // odf name list
        odf_list.Add("DummyControl-O");        
    }

    void Update(){

    }

    public System.Object ConvertFunctionToObject(DeviceFeatureFunc f){
        return (System.Object)f;
    }

    public void on_register(){
        Debug.Log("register successfully");
    }

    public object Dummy_Sensor(params object[] args){
        return r.NextDouble()*10;
    }

    public object Dummy_Control(params object[] args){
        Debug.Log(args[0]);
        return null;
    }
}