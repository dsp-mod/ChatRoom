using BepInEx;
using UnityEngine;
using System;
using System.Text;
using System.Reflection;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using HarmonyLib;

namespace ChatRoom
{
    [BepInPlugin("sky.plugins.dsp.ChatRoom", "ChatRoom", "1.0")]
    class ChatRoom:BaseUnityPlugin
    {
        GameObject prefab_BasePanel;//聊天框
        GameObject ui_BasePanel;
        InputField inputText;//输入框
        InputField showText;//消息框
        bool showGUI = false;//是否显示聊天框

        GameObject prefab_MsgText;//提示消息
        GameObject ui_MsgText;
        InputField MsgText;//消息提示框
        string Msg;//消息
        bool MsgOpen = false;//是否展开消息

        GameObject prefab_OpenPosition;//open定位点
        GameObject ui_OpenPosition;

        GameObject prefab_ClosePosition;//close定位点
        GameObject ui_ClosePosition;

        GameObject prefab_ServerStateText;//服务器信息框
        GameObject ui_ServerStateText;
        Text ServerStateText;//服务器信息

        bool keyLock = false;//按键锁
        bool dataLoadOver = false;//是否加载完数据
        bool connected = false;//是否已经连接到聊天服务器
        bool connecting = false;//是否正在连接
        string IPAddress;//服务器地址
        int Port;//服务器端口
        
        Socket Client;//客户端的socket
        static bool GameRunning=false;//游戏是否在运行
        void Start()
        {
            Harmony.CreateAndPatchAll(typeof(ChatRoom), null);
            //加载资源
            var ab = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("ChatRoom.chatroomgui"));
            prefab_BasePanel = ab.LoadAsset<GameObject>("BasePanel");
            prefab_MsgText = ab.LoadAsset<GameObject>("MsgText");
            prefab_OpenPosition= ab.LoadAsset<GameObject>("OpenPosition");
            prefab_ClosePosition = ab.LoadAsset<GameObject>("ClosePosition");
            prefab_ServerStateText = ab.LoadAsset<GameObject>("ServerStateText");
            //加载服务器配置
            IPAddress =Config.Bind<string>("config", "IPAddress", "dsp.sky9527.top", "服务器地址").Value;
            Port = Config.Bind<int>("config", "Port", 19730, "服务器端口").Value;
        }
        void Update()
        {
            if (dataLoadOver && GameMain.instance!=null)
            {
                //初始化客户端
                if (!connecting && GameRunning)
                {
                    Console.WriteLine("正在连接服务器");
                    connecting = true;
                    Thread conn = new Thread(Conn);
                    conn.Start();
                }
                //检测按键,判断是否显示输入框
                if(!keyLock && Input.GetKeyDown(KeyCode.Return))
                {
                    keyLock = true;
                    showGUI = !showGUI;
                }
                else if(keyLock && Input.GetKeyUp(KeyCode.Return))
                {
                    keyLock = false;
                }
                //根据showGUI切换BasePanel的GUI的显示情况
                if (showGUI && !ui_BasePanel.activeSelf)
                {
                    ui_BasePanel.SetActive(true);
                    ui_ServerStateText.SetActive(true);
                    inputText.ActivateInputField();
                    ui_MsgText.SetActive(false);
                    MsgOpen = false;
                }
                else if (!showGUI && ui_BasePanel.activeSelf)
                {
                    ui_BasePanel.SetActive(false);
                    ui_ServerStateText.SetActive(false);
                    inputText.text = "";//是否在关闭界面的时候清空用户的输入框
                }
                //更新消息框和消息提示框内容
                if(showGUI && showText.text != Msg)
                {
                    showText.text = Msg;
                }else if(ui_MsgText.activeSelf && MsgText.text != Msg)
                {
                    MsgText.text = Msg;
                }
                //清理客户端和消息提示框
                if (!GameRunning)
                {
                    showGUI = false;
                    ui_MsgText.SetActive(false);
                    if (Client != null)
                    {
                        Client.Close();
                    }
                }
                //更新消息提示框位置
                else
                {
                    if(MsgOpen && ui_MsgText.transform.localPosition!=ui_OpenPosition.transform.localPosition)
                    {
                        ui_MsgText.transform.localPosition = ui_OpenPosition.transform.localPosition;
                    }
                    else if(!MsgOpen && ui_MsgText.transform.localPosition != ui_ClosePosition.transform.localPosition)
                    {
                        ui_MsgText.transform.localPosition = ui_ClosePosition.transform.localPosition;
                    }
                }
            }
            //载入数据
            else if(!dataLoadOver && UIRoot.instance.overlayCanvas.transform!=null && GameMain.instance != null)
            {
                //加载UI
                ui_BasePanel = GameObject.Instantiate(prefab_BasePanel, UIRoot.instance.overlayCanvas.transform);
                ui_BasePanel.SetActive(false);

                ui_MsgText = GameObject.Instantiate(prefab_MsgText, UIRoot.instance.overlayCanvas.transform);
                ui_MsgText.SetActive(false);

                ui_OpenPosition = GameObject.Instantiate(prefab_OpenPosition, UIRoot.instance.overlayCanvas.transform);
                ui_OpenPosition.SetActive(false);

                ui_ClosePosition = GameObject.Instantiate(prefab_ClosePosition, UIRoot.instance.overlayCanvas.transform);
                ui_ClosePosition.SetActive(false);

                ui_ServerStateText = GameObject.Instantiate(prefab_ServerStateText, UIRoot.instance.overlayCanvas.transform);
                ui_ServerStateText.SetActive(false);

                inputText = ui_BasePanel.transform.Find("inputText").gameObject.GetComponent<InputField>();
                showText = ui_BasePanel.transform.Find("showText").gameObject.GetComponent<InputField>();
                MsgText = ui_MsgText.GetComponent<InputField>();
                MsgText.transform.Find("Button").gameObject.GetComponent<Button>().onClick.AddListener(delegate {
                    MsgOpen = !MsgOpen;
                });

                ServerStateText = ui_ServerStateText.GetComponent<Text>();

                
                inputText.onEndEdit.AddListener(delegate {
                    bool press = Input.GetKeyDown(KeyCode.Return);
                    if (press && inputText.text!="")
                    {
                        keyLock = true;
                        SendMsg(inputText.text);
                        inputText.text = "";
                        inputText.ActivateInputField();
                    }
                });
                dataLoadOver = true;
                GameRunning = true;
            }
        }
        void Conn()
        {
            while (GameRunning)
            {
                ServerStateText.text = "正在连接服务器";
                try
                {
                    IPHostEntry host = Dns.GetHostEntry(IPAddress);
                    IPEndPoint point = new IPEndPoint(host.AddressList[0], Port);
                    Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    Client.Connect(point);

                    ServerStateText.text = "连接成功";
                    connected = true;
                    ListenServerMsg();
                }
                catch
                {
                    ServerStateText.text = "连接失败,请检查服务器地址和端口是否正确";
                    Thread.Sleep(5000);
                    ServerStateText.text = "准备重连服务器";
                    Thread.Sleep(5000);
                    connected = false;
                }
            }
        }
        void SendMsg(string msg)
        {
            if (connected)
            {
                string playerName;
                if (STEAMX.username != "")
                {
                    playerName = STEAMX.username;
                }
                else if (RAILX.username != "")
                {
                    playerName = RAILX.username;
                }
                else
                {
                    playerName = "请支持正版-游客";
                }
                string fullMsg = playerName + ":" + msg+ "\n";
                byte[] buffer = Encoding.UTF8.GetBytes(fullMsg);
                Client.Send(buffer);
            } 
        }
        void ListenServerMsg()
        {
            while (connected)
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    int n = Client.Receive(buffer);
                    string s = Encoding.UTF8.GetString(buffer, 0, n);
                    if (!s.Contains(":"))
                    {
                        ServerStateText.text = "在线人数:" + s;
                    }
                    else
                    {
                        Msg = s+ Msg;
                        if (!showGUI && !ui_MsgText.activeSelf)
                        {
                            ui_MsgText.SetActive(true);
                        }
                    }
                }
                catch
                {
                    ServerStateText.text = "与服务器断开连接";
                    connected = false;
                    break;
                }
            }
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DSPGame), "ExitProgram")]
        public static bool ExitGame()
        {
            GameRunning = false;
            return true;
        }
    }
}
