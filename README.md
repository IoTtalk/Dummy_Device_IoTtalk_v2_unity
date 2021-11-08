# Dummy_Device_IoTtalk_v2 (unity)

* Unity Version >= 2018.1
* Unity Version >= 2018.2 (TLS support)

## how to use
1. Create an Unity Project.
2. Set API compatibility level to .Net 4.x ( in **Edit > Project Settings > Player** ).
3. Import two packages, **JsonNet** and **unity_mqtt**, from packages folder.
4. Create two **Empty Gameobjects** ( recommended to be named **DA** and **SA** ).
5. Add `IoTTalkV2SA.cs` as a component to SA Gameobject.
6. Add `IoTTalkV2DAI.cs` as a component to DA Gameobject.
7. Assign SA GameObject to `SAobject`, which is public variable of `IoTTalkV2DAI` in DA Gameobject. 
8. Press **Play** button to start program.

## TLS support
* TLS 1.2 support via .Net45 APIs (SslStream, HttpRequest, ...) in 2018.2 or newer version ( recommend to use LTS version ). For more information, please refer to the [Unity official website](https://unity3d.com/unity/whats-new/unity-2018.2.0).

## Function Format
* odf & idf:
``` C#
public object <function_name>(params object[] args)
{
    // do someting
    // if no return value, return null
}
```
