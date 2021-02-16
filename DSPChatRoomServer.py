import socket
import threading


class Server:
    def __init__(self):
        self.canRun = True
        self.host = "127.0.0.1"#此处填服务器的公网IP
        self.port = 19730
        self.ClientList = []
        self.ServerSocket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.ServerSocket.bind((self.host, self.port))
        self.ServerSocket.listen(5)
        self.Start()

    def Start(self):
        ListenClientConnThread = threading.Thread(target=self.ListenClientConn)
        ListenClientConnThread.setDaemon(True)
        print("开始监听客户端连接")
        ListenClientConnThread.start()

    def ListenClientConn(self):
        while True:
            ClientSocket, ClientAddress = self.ServerSocket.accept()
            print(str(ClientAddress) + "进入连接")
            self.ClientList.append(ClientSocket)
            ListenClientMsgThread = threading.Thread(target=self.ListenClientMsg, args=(ClientSocket,))
            ListenClientMsgThread.setDaemon(True)
            print("现有连接数:" + str(len(self.ClientList)))
            self.SendMsg(str(len(self.ClientList)))
            print("开始监听" + str(ClientAddress) + "的消息")
            ListenClientMsgThread.start()

    def ListenClientMsg(self, ClientSocket):
        while True:
            try:
                msg = ClientSocket.recv(1024).decode()
                if msg == "":
                    self.ClientList.remove(ClientSocket)
                    print(str(ClientSocket) + "断开连接")
                    print("现有连接数:" + str(len(self.ClientList)))
                    self.SendMsg(str(len(self.ClientList)))
                    break
                else:
                    print(msg)
                    self.SendMsg(msg)
            except:
                self.ClientList.remove(ClientSocket)
                print(str(ClientSocket) + "断开连接")
                print("现有连接数:" + str(len(self.ClientList)))
                self.SendMsg(str(len(self.ClientList)))
                break

    def SendMsg(self, msg):
        BroadcastMsgThread = threading.Thread(target=self.BroadcastMsg, args=(msg,))
        BroadcastMsgThread.setDaemon(True)
        BroadcastMsgThread.start()

    def BroadcastMsg(self, msg):
        for i in self.ClientList:
            i.send(msg.encode())


if __name__ == "__main__":
    server = Server()
    while True:
        if input() == "stop":
            server.ServerSocket.close()
            break
