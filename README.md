# Dummy_Device_IoTtalk_v2 (unity)

* Unity Version >= 2018.1.0f2

## how to use
1. 建立一個 Unity Project
2. set API compatibility level to .Net 4.x (in **Edit > Project Settings > Player**)
3. 從 packages folder 引入 **JsonNet** 和 **unity_mqtt** 
4. 生成兩個 empty gameobject ( 建議 )命名為 **Client** 和 **SA**
5. 將 `IoTTalkV2SA.cs` 這個 script 掛到 SA 物件底下，`IoTTalkV2DAI.cs` 掛到 Client 底下
6. 將 SA 物件指定給 `IoTTalkV2DAI.cs` 的 SAobject
7. 按 Play 執行程式

## 須知
* odf 和 idf function 格式須為
``` C#
public object <function_name>(params object[] args)
{
    // do someting
    // if no return value, return null
}
```
