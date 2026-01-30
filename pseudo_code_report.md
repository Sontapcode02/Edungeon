# Pseudo Code - Dự án Edungeon

Tài liệu này mô tả giải thuật tổng quan của hệ thống Game Real-time Multiplayer (Client-Server).

## 1. Server Side (Máy chủ)

### 1.1. Khởi động & Lắng nghe
```text
Function StartServer(port):
    Khởi tạo WebSocket Listener tại port (cho WebGL)
    Khởi tạo TCP Listener tại port+1 (cho Editor/Native)
    
    While ServerIsRunning:
        If NewClientConnected:
            Tạo ClientHandler mới cho client đó
            Chạy Thread riêng để xử lý client đó
```

### 1.2. Xử lý Gói tin (Packet Handling)
```text
Function ProcessPacket(packet, session):
    Switch packet.Type:
        Case "CREATE_ROOM":
            Tạo Room mới với ID ngẫu nhiên
            Gán session làm Host
            Lưu cấu hình (MaxPlayers, Câu hỏi) vào Room
            Gửi phản hồi "ROOM_CREATED"

        Case "JOIN_ROOM":
            Tìm Room theo ID
            If Room tồn tại AND (Room.PlayerCount < Room.MaxPlayers):
                Gửi "JOIN_SUCCESS" + ID mới cho Client
                Thêm session vào Room.Players
                Gửi "SYNC_PLAYERS" (danh sách người chơi hiện có) cho người mới
                Broadcast "PLAYER_JOINED" cho tất cả người cũ
                Broadcast "PROGRESS_UPDATE" (Leaderboard) cập nhật
            Else:
                Gửi Lỗi "Room Full" hoặc "Room Not Found"

        Case "MOVE":
            Cập nhật vị trí (x, y) vào session
            Broadcast "MOVE" (ID, x, y) cho các người chơi khác trong phòng

        Case "ANSWER":
            Kiểm tra đáp án (đúng/sai)
            If Đúng:
                Cộng điểm cho session
            Gửi kết quả "ANSWER_RESULT" về Client
            Broadcast "PROGRESS_UPDATE" để cập nhật Leaderboard

        Case "REACHED_FINISH":
            Ghi nhận thời gian hoàn thành
            If Tất cả Players đã về đích:
                Gửi "GAME_OVER_SUMMARY" (Tổng kết)
                Đóng phòng sau 10 giây
```

---

## 2. Client Side (Người chơi)

### 2.1. Kết nối & Vào phòng
```text
Function ConnectAndJoin(name, roomId):
    Kết nối tới Server IP:Port (TCP hoặc WebSocket)
    Gửi gói tin "JOIN_ROOM" { name, roomId }
    
    Wait cho phản hồi:
        If nhận "JOIN_SUCCESS":
            Lưu MyPlayerID
            Load Scene "Game"
        Else:
            Hiển thị lỗi
```

### 2.2. Vòng lặp Game (Game Loop)
```text
Function Update():
    If IsLocalPlayer:
        // Xử lý di chuyển
        Input = GetArrowKeys()
        Calculate NewPosition
        MoveCharacter(NewPosition)
        
        // Gửi toạ độ lên Server (tối ưu 10 lần/giây)
        If Time > NextSendTime:
            SendPacket("MOVE", {x, y})
            NextSendTime = Time + 0.1s

    Else (Remote Player):
        // Nội suy vị trí để mượt mà
        InterpolatePosition(CurrentPos, TargetPosFromServer)
```

### 2.3. Xử lý Va chạm Quái vật (Quiz Logic)
```text
Function OnTriggerEnter(collider):
    If collider là Monster AND chưa trả lời:
        Gửi "REQUEST_QUESTION" { monsterId } lên Server
        Dừng di chuyển (Pause)

Function OnReceivePacket(packet):
    Case "NEW_QUESTION":
        Hiển thị UI Câu hỏi
        Wait User chọn đáp án
        Gửi "ANSWER" { questionId, answerIndex } lên Server
    
    Case "ANSWER_RESULT":
        Hiển thị thông báo (Đúng/Sai)
        Làm mờ Monster (đã diệt)
        Tiếp tục di chuyển (Unpause)
```

### 2.4. Đồng bộ & Leaderboard
```text
Function OnReceivePacket(packet):
    Case "SYNC_PLAYERS":
        For each player in List:
            If player.ID != MyID:
                Spawn hoặc Cập nhật vị trí người chơi khác
    
    Case "PROGRESS_UPDATE":
        // Leaderboard
        Danh sách = Deserialize(packet.Payload)
        // Server đã sắp xếp sẵn (Cao -> Thấp)
        For i from 0 to Count-1:
            UpdateRowUI(Danh sách[i])
            SetSiblingIndex(i) // Đảm bảo hiển thị đúng thứ tự
```
