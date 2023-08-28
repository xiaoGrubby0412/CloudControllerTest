using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Baidu.VR.Zion.Utils;
using Com.Baidu.Zion.Proto.Lib;
using Grpc.Core;
using UnityEngine;
using UnityEngine.Networking;
using static Com.Baidu.Zion.Proto.Lib.ZionRpcService;

namespace Baidu.VR.Zion
{
    public class CloudControlManager : Singleton2<CloudControlManager>
    {
        private const string STATUS_ISSUEDING = "status_issueding";
        private const string STATUS_PAUSE = "status_pause";
        private const float RESPONSE_TIMEOUT = 2f;

        private Channel channel;
        private AsyncDuplexStreamingCall<TaskRequest, TaskResponse> zionRpcServiceStream;
        private Action<TaskResponse> onRpcResponse;
        private bool isGrpcServiceStarted;
        private bool isInitialized;  //初始数据是否接收完成
        private float responseTimer;
        private DeviceInfo deviceInfo;
        //private RunEnvironment startEnv = RunEnvironment.Unknown;

        private Dictionary<string, string> taskUrls = new Dictionary<string, string>();
        private Dictionary<string, string> taskContent = new Dictionary<string, string>();
        private Dictionary<string, string> loadingTaskUrls = new Dictionary<string, string>();
        private readonly Dictionary<string, Action<string>> _registryCallbacks = new Dictionary<string, Action<string>>();
        private readonly Dictionary<string, Action<string>> _oneshotCallbacks = new Dictionary<string, Action<string>>();
        private Dictionary<Action<string>, string> _loadingCallbacks = new Dictionary<Action<string>, string>();

        private IEnumerator getTaskInfo;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            ResetData();

            onRpcResponse += OnRpcResponce;

            // Channel == 1
            // ClientVersion == 3.2.0.100
            // DeviceId == cea0b8bc858fa583f8177f918752032fb2dd7b60
            // OsVersion == 
            //     Platform == Windows
            // Stage == online
            // ChannelAddress == 180.76.107.56:8083
                
            deviceInfo = new DeviceInfo
            {
                Channel = "1",
                ClientVersion = "3.2.0.100",
                DeviceId = "cea0b8bc858fa583f8177f918752032fb2dd7b60",
                OsVersion = "",
                Platform = "Windows",
                Stage = "online"
                // ClientHotfixVersion = "3.2.0.1"
            };
            //startEnv = ServiceManager.Get<IEnvironmentManager>().Environment;
            Task.Run(ConnectZionRpcService);
            isGrpcServiceStarted = true;
        }

        private void OnDestroy()
        {
            StopCoroutine(getTaskInfo);
            taskUrls.Clear();
            taskContent.Clear();
            loadingTaskUrls.Clear();
            _registryCallbacks.Clear();
            _oneshotCallbacks.Clear();

            if (zionRpcServiceStream != null) Task.Run(Disconnect);
            onRpcResponse -= OnRpcResponce;
            isGrpcServiceStarted = false;
        }

        private void Update()
        {
            if (taskUrls.Count > 0 && loadingTaskUrls.Count == 0)
            {
                foreach (var item in taskUrls)
                {
                    loadingTaskUrls.Add(item.Key, item.Value);
                }
                taskUrls.Clear();
                getTaskInfo = GetTaskInfo();
                StartCoroutine(getTaskInfo);
            }
            if (_loadingCallbacks.Count > 0)
            {
                foreach (var item in _loadingCallbacks)
                {
                    item.Key?.Invoke(item.Value);
                }
                _loadingCallbacks.Clear();
            }

            if (isGrpcServiceStarted)
            {
                if (responseTimer < RESPONSE_TIMEOUT)
                {
                    responseTimer += Time.deltaTime;
                }
                else if (!isInitialized)
                {
                    OnTaskDataInitialized();
                    isInitialized = true;
                }
            }
        }

        public void Subscribe(string configName, Action<string> callback)
        {
            LoggerUtils.Log($"Subscribe configName {configName}", "CloudControlManager");
            if (taskContent.TryGetValue(configName, out var value))
            {
                LoggerUtils.Log($"Subscribe configName {configName} value {value}", "CloudControlManager");
                callback?.Invoke(value);
            }
            else if (isInitialized)
            {
                LoggerUtils.LogError($"Subscribe: initial data doesn't contain configName {configName}, invoke empty callback ", "CloudControlManager");
                callback?.Invoke("");
            }

            Action<string> cb;
            if (_registryCallbacks.TryGetValue(configName, out cb))
            {
                cb += callback;
                _registryCallbacks[configName] = cb;
            }
            else
            {
                _registryCallbacks.Add(configName, callback);
            }
        }

        public void Unsubscribe(string configName, Action<string> callback)
        {
            //LoggerUtils.Log($"Subscribe configName {configName}", "CloudControlManager");
            Action<string> cb;
            if (_registryCallbacks.TryGetValue(configName, out cb))
            {
                cb -= callback;
                _registryCallbacks[configName] = cb;
            }
        }

        public void GetCloudControl(string configName, Action<string> callback)
        {
            //LoggerUtils.Log($"GetCloudControl configName {configName}", "CloudControlManager");
            if (taskContent.TryGetValue(configName, out var value))
            {
                //LoggerUtils.Log($"GetCloudControl configName {configName} value {value}", "CloudControlManager");
                callback?.Invoke(value);
                return;
            }
            else if (isInitialized)
            {
                //LoggerUtils.LogError($"GetCloudControl: initial data doesn't contain configName {configName}, invoke empty callback", "CloudControlManager");
                callback?.Invoke("");
            }

            if (_oneshotCallbacks.TryGetValue(configName, out var cb))
            {
                cb += callback;
                _oneshotCallbacks[configName] = cb;
            }
            else
            {
                _oneshotCallbacks.Add(configName, callback);
            }
        }

        public async void ConnectZionRpcService()
        {
            LoggerUtils.Log($"Start connect to: {ChannelAddress}, enviroment: {"123"}", "CloudControlManager");
            channel = new Channel(ChannelAddress, ChannelCredentials.Insecure);
            ZionRpcServiceClient client = new ZionRpcServiceClient(channel);
            Metadata mData = new Metadata();
            mData.Add("token", GenerateAuth());
            mData.Add("domain", "vr.baidu.com");
            zionRpcServiceStream = client.Connect(mData);

            MetaInfo metaInfo = new MetaInfo
            {
                //ReqId = "",
                //Timestamp = (ulong)0
            };

            Task _task = Task.Run(async () =>
            {
                try
                {
                    while (isGrpcServiceStarted && await zionRpcServiceStream.ResponseStream.MoveNext())
                    {
                        responseTimer = 0;
                        var responce = zionRpcServiceStream.ResponseStream.Current;
                        onRpcResponse?.Invoke(responce);
                    }
                }
                catch (Exception e)
                {
                    LoggerUtils.LogError(e.Message, "CloudControlManager");
                }
                LoggerUtils.Log($"Grpc disconnected", "CloudControlManager");
            });

            TaskRequest request = new TaskRequest { DeviceInfo = deviceInfo, MetaInfo = metaInfo };
            await zionRpcServiceStream.RequestStream.WriteAsync(request);
            //await zionRpcServiceStream.RequestStream.CompleteAsync();
        }

        private void OnRpcResponce(TaskResponse response)
        {
            //LoggerUtils.Log($"Zion Rpc Responce: ResId: {response.ResId} Result: {response.Result}", "CloudControlManager");
            if (response.TaskInfo != null)
            {
                if (response.TaskInfo.TaskType == 0)
                {
                    LoggerUtils.Log($"Zion Rpc Responce: ConfigName: {response.TaskInfo.ConfigName} Url: {response.TaskInfo.Url} status: {response.TaskInfo.TaskStatus}", "CloudControlManager");
                    if (response.TaskInfo.TaskStatus == STATUS_ISSUEDING)
                    {
                        if (!taskUrls.ContainsKey(response.TaskInfo.ConfigName))
                        {
                            taskUrls.Add(response.TaskInfo.ConfigName, response.TaskInfo.Url);
                        }
                        else
                        {
                            taskUrls[response.TaskInfo.ConfigName] = response.TaskInfo.Url;
                        }
                    }
                    else if (response.TaskInfo.TaskStatus == STATUS_PAUSE)
                    {
                        OnTaskContentLoaded(response.TaskInfo.ConfigName, "");
                    }
                }
                else if (response.TaskInfo.TaskType == 1)
                {
                    if (!taskContent.ContainsKey(response.TaskInfo.ConfigName))
                    {
                        taskContent.Add(response.TaskInfo.ConfigName, response.TaskInfo.Content);
                    }
                    else
                    {
                        taskContent[response.TaskInfo.ConfigName] = response.TaskInfo.Content;
                    }

                    if (response.TaskInfo.TaskStatus == STATUS_ISSUEDING)
                    {
                        OnTaskContentLoaded(response.TaskInfo.ConfigName, response.TaskInfo.Content);
                    }
                    else if (response.TaskInfo.TaskStatus == STATUS_PAUSE)
                    {
                        OnTaskContentLoaded(response.TaskInfo.ConfigName, "");
                    }
                    LoggerUtils.Log($"Zion Rpc Responce: ConfigName: {response.TaskInfo.ConfigName} Content: {response.TaskInfo.Content} status: {response.TaskInfo.TaskStatus}", "CloudControlManager");
                }
            }
            else
            {
                LoggerUtils.Log($"Zion Rpc Responce: TaskInfo is null", "CloudControlManager");
            }
        }

        private IEnumerator GetTaskInfo()
        {
            foreach (var item in loadingTaskUrls)
            {
                UnityWebRequest request = UnityWebRequest.Get(item.Value);
                yield return request.SendWebRequest();
                if (request.isDone)
                {
                    LoggerUtils.Log($"Zion Rpc Service taskname: {item.Key} taskinfo: " + request.downloadHandler.text, "CloudControlManager");
                    if (!taskContent.ContainsKey(item.Key))
                    {
                        taskContent.Add(item.Key, request.downloadHandler.text);
                    }
                    else
                    {
                        taskContent[item.Key] = request.downloadHandler.text;
                    }
                    OnTaskContentLoaded(item.Key, request.downloadHandler.text);
                }
                else
                {
                    LoggerUtils.LogError($"Get task fail: {item.Key} Task url: {item.Value} error: {request.error}", "CloudControlManager");
                }
            }
            loadingTaskUrls.Clear();
        }

        private void OnTaskContentLoaded(string taskName, string content)
        {
            if (_registryCallbacks.TryGetValue(taskName, out var cb) && cb != null)
            {
                LoggerUtils.Log($"OnTaskContentLoaded _registryCallbacks taskName {taskName} content {content}", "CloudControlManager");
                _loadingCallbacks[cb] = content;
                //cb.Invoke(content);
            }
            if (_oneshotCallbacks.TryGetValue(taskName, out cb) && cb != null)
            {
                LoggerUtils.Log($"OnTaskContentLoaded _oneshotCallbacks taskName {taskName} content {content}", "CloudControlManager");
                //cb.Invoke(content);
                _loadingCallbacks[cb] = content;
            }
            _oneshotCallbacks.Remove(taskName);
        }

        /// <summary>
        /// 首次初始化完成，调用所有无配置数据的回调，参数为空
        /// </summary>
        private void OnTaskDataInitialized()
        {
            LoggerUtils.Log($"Task data initialized", "CloudControlManager");
            foreach (var item in _registryCallbacks)
            {
                if (!taskContent.ContainsKey(item.Key))
                {
                    item.Value?.Invoke("");
                    LoggerUtils.LogError($"_registryCallback: initial data doesn't contain configName {item.Key}, invoke empty callback", "CloudControlManager");
                }
            }
            List<string> _callbacksToDelete = new List<string>();
            foreach (var item in _oneshotCallbacks)
            {
                if (!taskContent.ContainsKey(item.Key))
                {
                    _callbacksToDelete.Add(item.Key);
                    item.Value?.Invoke("");
                    LoggerUtils.LogError($"_oneshotCallback: initial data doesn't contain configName {item.Key}, invoke empty callback", "CloudControlManager");
                }
            }
            if (_callbacksToDelete.Count > 0)
            {
                for (int i = 0; i < _callbacksToDelete.Count; i++)
                {
                    _oneshotCallbacks.Remove(_callbacksToDelete[i]);
                }
            }
            _callbacksToDelete.Clear();
        }

        private string GenerateAuth()
        {
            //xirang
             // string ak = "eaa4510ddce04c258b767ce55e69e3d1";
             // string sk = "1d99291bb0ff4b65a33362c4738804b8";
             //私有化
            string ak = "127a036d0eeb4e33bae281073819df5e";
            string sk = "f17549f6f52a46d1903de01d159fb1b3";
            //测试环境内网
            //string ak = "eaa4510ddce04c258b767ce55e69e3d1";
            //string sk = "1d99291bb0ff4b65a33362c4738804b8";
            
            string utcDate = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            string expire = "1800";
            string canonicalRequest = "POST\n/connect\n\nhost:vr.baidu.com";
            string authStringPrefix = $"bce-auth-v1/{ak}/{utcDate}/{expire}";
            string signingKey = HmacSha256Hex(sk, authStringPrefix);
            string signature = HmacSha256Hex(signingKey, canonicalRequest);
            string authSignature = $"{authStringPrefix}/host/{signature}";
            return authSignature;
        }

        private string HmacSha256Hex(string signKey, string secret)
        {
            string signRet = string.Empty;
            using (HMACSHA256 mac = new HMACSHA256(Encoding.UTF8.GetBytes(signKey)))
            {
                byte[] hash = mac.ComputeHash(Encoding.UTF8.GetBytes(secret));
                //signRet = Convert.ToBase64String(hash);
                signRet = BitConverter.ToString(hash, 0).Replace("-", string.Empty).ToLower();
            }
            return signRet;
        }

        public async void Disconnect()
        {
            isGrpcServiceStarted = false;
            await zionRpcServiceStream.RequestStream.CompleteAsync();
            await channel.ShutdownAsync();
            zionRpcServiceStream?.Dispose();
        }

        /// <summary>
        /// 重新连接云控
        /// </summary>
        public void Reconnect()
        {
            ResetData();
            zionRpcServiceStream?.Dispose();
            isGrpcServiceStarted = true;
            Task.Run(ConnectZionRpcService);
        }

        /// <summary>
        /// 从环境选择窗口更改环境，需要清除旧环境云控数据并重新连接云控
        /// </summary>
        // public async void OnSwitchEnviroment()
        // {
        //     if (ServiceManager.Get<IEnvironmentManager>().Environment != startEnv)
        //     {
        //         LoggerUtils.Log($"OnSwitchEnviroment {startEnv}=>{ServiceManager.Get<IEnvironmentManager>().Environment}", "CloudControlManager");
        //         isGrpcServiceStarted = false;
        //         await zionRpcServiceStream.RequestStream.CompleteAsync();
        //         await channel.ShutdownAsync();
        //         Reconnect();
        //     }
        // }

        /// <summary>
        /// 清空云控数据
        /// </summary>
        private void ResetData()
        {
            taskUrls.Clear();
            taskContent.Clear();
            isInitialized = false;
            responseTimer = 0;
        }

        private string ChannelAddress
        {
            get
            {
                //xirang
                //return "180.76.107.56:8083";
                //私有化
                return "10.45.68.4:8888";
                //测试环境内网
                //return "10.45.62.148:8083";
            }
        }
    }
}